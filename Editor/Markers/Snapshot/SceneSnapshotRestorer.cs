#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;
using SceneBlueprint.Runtime.Snapshot;
using SceneBlueprint.Editor.Markers.Definitions;
using SceneBlueprint.Editor.Markers.Annotations;
using SceneBlueprint.Editor.Logging;

namespace SceneBlueprint.Editor.Markers.Snapshot
{
    /// <summary>
    /// 场景快照恢复器 — 从 <see cref="BindingSnapshot"/> 列表重建场景中的 Marker + Annotation。
    /// <para>
    /// 恢复流程：
    /// 1. 遍历快照列表
    /// 2. 通过 MarkerDefinitionRegistry 查找 markerTypeId → ComponentType
    /// 3. 通过 MarkerHierarchyManager 在正确的 Hierarchy 分组下创建 GameObject
    /// 4. 恢复 SceneMarker 标识字段（MarkerId/MarkerName/Tag/SubGraphId）
    /// 5. 恢复 Transform（position/rotation/localScale）
    /// 6. 恢复 ShapeData（SerializeShapeData/DeserializeShapeData）
    /// 7. 通过 AnnotationDefinitionRegistry 创建 Annotation 组件并恢复数据
    /// 8. 递归恢复子 Marker
    /// 9. 恢复完成后由 BindingContext 通过 MarkerId 反查建立绑定
    /// </para>
    /// </summary>
    public static class SceneSnapshotRestorer
    {
        /// <summary>
        /// 恢复结果统计。
        /// </summary>
        public struct RestoreResult
        {
            /// <summary>成功恢复的 Marker 数量</summary>
            public int restoredCount;
            /// <summary>跳过的快照数量（场景中已存在或定义缺失）</summary>
            public int skippedCount;
            /// <summary>恢复失败的快照数量</summary>
            public int failedCount;
        }

        /// <summary>
        /// 从快照列表恢复场景中缺失的 Marker。
        /// <para>
        /// 仅恢复场景中不存在的 Marker（通过 MarkerId 检测）。
        /// 已存在的 Marker 跳过，避免重复创建。
        /// </para>
        /// </summary>
        /// <param name="snapshots">快照列表（来自 BlueprintAsset.SceneSnapshots）</param>
        /// <returns>恢复结果统计</returns>
        public static RestoreResult Restore(List<BindingSnapshot> snapshots)
        {
            var result = new RestoreResult();

            if (snapshots == null || snapshots.Count == 0)
            {
                SBLog.Info(SBLogTags.Snapshot, "快照列表为空，无需恢复");
                return result;
            }

            // 确保注册表已初始化
            MarkerDefinitionRegistry.EnsureDiscovered();
            AnnotationDefinitionRegistry.EnsureDiscovered();

            // 收集场景中已存在的 MarkerId
            var existingMarkerIds = new HashSet<string>();
            foreach (var existing in MarkerHierarchyManager.FindAllMarkers())
            {
                if (!string.IsNullOrEmpty(existing.MarkerId))
                    existingMarkerIds.Add(existing.MarkerId);
            }

            // 使用 Undo 组合，便于一次性撤销
            Undo.SetCurrentGroupName("快照恢复 Marker");

            foreach (var snapshot in snapshots)
            {
                if (string.IsNullOrEmpty(snapshot.markerId))
                {
                    SBLog.Warn(SBLogTags.Snapshot, "快照中 markerId 为空，跳过");
                    result.skippedCount++;
                    continue;
                }

                // 场景中已存在该 Marker，跳过
                if (existingMarkerIds.Contains(snapshot.markerId))
                {
                    result.skippedCount++;
                    continue;
                }

                var marker = RestoreSingleMarker(snapshot, null);
                if (marker == null)
                {
                    result.failedCount++;
                    continue;
                }

                existingMarkerIds.Add(snapshot.markerId);
                result.restoredCount++;
            }

            SBLog.Info(SBLogTags.Snapshot,
                "快照恢复完成：恢复 {0} / 跳过 {1} / 失败 {2}",
                result.restoredCount, result.skippedCount, result.failedCount);

            return result;
        }

        /// <summary>
        /// 恢复单个 Marker（含 Annotation 和子 Marker）。
        /// </summary>
        /// <param name="snapshot">快照数据</param>
        /// <param name="parentTransform">父 Transform（子 Marker 使用，顶层传 null）</param>
        /// <returns>恢复的 SceneMarker，失败返回 null</returns>
        private static SceneMarker? RestoreSingleMarker(BindingSnapshot snapshot, Transform? parentTransform)
        {
            // 1. 查找 Marker 定义
            var markerDef = MarkerDefinitionRegistry.Get(snapshot.markerTypeId);
            if (markerDef == null)
            {
                SBLog.Warn(SBLogTags.Snapshot,
                    "未找到 MarkerTypeId={0} 的定义，跳过 MarkerId={1}",
                    snapshot.markerTypeId, snapshot.markerId);
                return null;
            }

            // 2. 创建 GameObject
            GameObject go;
            if (parentTransform != null)
            {
                // 子 Marker：直接在 parent 下创建
                go = new GameObject(snapshot.gameObjectName);
                Undo.RegisterCreatedObjectUndo(go, $"恢复子标记 {snapshot.gameObjectName}");
                go.transform.SetParent(parentTransform);
            }
            else
            {
                // 顶层 Marker：放入 Hierarchy 分组
                var groupTransform = MarkerHierarchyManager.GetOrCreateGroup(
                    string.IsNullOrEmpty(snapshot.hierarchyGroup) ? null : snapshot.hierarchyGroup);
                go = new GameObject(snapshot.gameObjectName);
                Undo.RegisterCreatedObjectUndo(go, $"恢复标记 {snapshot.gameObjectName}");
                go.transform.SetParent(groupTransform);
            }

            // 3. 添加 SceneMarker 组件
            var marker = (SceneMarker)go.AddComponent(markerDef.ComponentType);

            // 4. 恢复标识字段
            // 必须通过 SerializedObject API 修改序列化字段。
            // Undo.RegisterCreatedObjectUndo 会重建 C# wrapper 对象，
            // 导致直接赋值公共字段被 Undo 快照覆盖为默认值。
            // 详见文档：Knowledge/Developer/D4_UndoFieldOverwrite.md
            {
                var so = new SerializedObject(marker);
                so.FindProperty("_markerId").stringValue = snapshot.markerId;
                so.FindProperty("MarkerName").stringValue = snapshot.markerName;
                so.FindProperty("Tag").stringValue = snapshot.tag;
                so.FindProperty("_subGraphId").stringValue = snapshot.subGraphId;
                so.ApplyModifiedProperties();
            }

            // 5. 恢复 Transform
            var spatial = snapshot.spatial;
            go.transform.position = spatial.position;
            go.transform.rotation = spatial.rotation;
            go.transform.localScale = spatial.localScale;

            // 6. 恢复 ShapeData（通过虚方法调用，内部会操作序列化字段）
            if (!string.IsNullOrEmpty(spatial.shapeDataJson) && spatial.shapeDataJson != "{}")
            {
                marker.DeserializeShapeData(spatial.shapeDataJson);
            }

            // 7. 恢复 Annotation
            if (snapshot.annotations != null)
            {
                foreach (var annSnapshot in snapshot.annotations)
                {
                    RestoreAnnotation(go, annSnapshot);
                }
            }

            // 8. 递归恢复子 Marker
            if (snapshot.children != null)
            {
                foreach (var childSnapshot in snapshot.children)
                {
                    RestoreSingleMarker(childSnapshot, go.transform);
                }
            }

            SBLog.Debug(SBLogTags.Snapshot,
                "已恢复 Marker: {0} (TypeId={1}, MarkerId={2})",
                go.name, snapshot.markerTypeId, snapshot.markerId);

            return marker;
        }

        /// <summary>
        /// 恢复单个 Annotation 组件到 GameObject 上。
        /// </summary>
        private static void RestoreAnnotation(GameObject go, AnnotationSnapshot annSnapshot)
        {
            if (string.IsNullOrEmpty(annSnapshot.typeId))
            {
                SBLog.Warn(SBLogTags.Snapshot, "Annotation 快照 typeId 为空，跳过");
                return;
            }

            var annDef = AnnotationDefinitionRegistry.Get(annSnapshot.typeId);
            if (annDef == null)
            {
                SBLog.Warn(SBLogTags.Snapshot,
                    "未找到 AnnotationTypeId={0} 的定义，跳过", annSnapshot.typeId);
                return;
            }

            // 检查 GO 上是否已存在同类型 Annotation（Reset() 可能已自动添加）
            var existing = go.GetComponent(annDef.ComponentType) as MarkerAnnotation;
            if (existing != null)
            {
                // 已存在：仅恢复数据，不重复添加
                var existingData = SnapshotJsonHelper.JsonToDict(annSnapshot.propertiesJson);
                if (existingData.Count > 0)
                {
                    existing.RestoreFromExportData(existingData);
                    // 强制将内存中的字段值同步到序列化层，防止 Undo 快照覆盖
                    // 详见文档：Knowledge/Developer/D4_UndoFieldOverwrite.md
                    new SerializedObject(existing).ApplyModifiedPropertiesWithoutUndo();
                }

                SBLog.Debug(SBLogTags.Snapshot,
                    "Annotation {0} 已存在于 {1}，仅恢复数据", annSnapshot.typeId, go.name);
                return;
            }

            // 添加 Annotation 组件
            var annotation = (MarkerAnnotation)go.AddComponent(annDef.ComponentType);

            // 从 JSON 恢复数据
            var data = SnapshotJsonHelper.JsonToDict(annSnapshot.propertiesJson);
            if (data.Count > 0)
            {
                annotation.RestoreFromExportData(data);
                // 强制将内存中的字段值同步到序列化层，防止 Undo 快照覆盖
                new SerializedObject(annotation).ApplyModifiedPropertiesWithoutUndo();
            }

            SBLog.Debug(SBLogTags.Snapshot,
                "已恢复 Annotation: {0} on {1}", annSnapshot.typeId, go.name);
        }
    }
}
