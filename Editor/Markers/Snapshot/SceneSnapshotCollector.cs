#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;
using SceneBlueprint.Runtime.Snapshot;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor;

namespace SceneBlueprint.Editor.Markers.Snapshot
{
    /// <summary>
    /// 场景快照采集器 — 从 BindingContext（内存）采集场景中的 Marker + Annotation 数据，
    /// 生成 <see cref="BindingSnapshot"/> 列表。
    /// <para>
    /// 采集流程：
    /// 1. 遍历 BindingContext 中所有绑定
    /// 2. 对每个有效 GameObject，提取 SceneMarker 及其 Annotation 数据
    /// 3. 递归采集子 Marker（Transform 子级中的 SceneMarker）
    /// 4. 推断 hierarchyGroup（从 Hierarchy 路径中提取分组名）
    /// </para>
    /// </summary>
    public static class SceneSnapshotCollector
    {
        /// <summary>
        /// 从编辑器 BindingContext（内存）直接采集快照。
        /// 遍历 BindingContext 中所有绑定，采集每个 Marker 的快照。
        /// 同一 Marker 被多个绑定键引用时不去重，恢复时由 Restorer 通过 MarkerId 天然去重。
        /// </summary>
        /// <param name="bindingContext">编辑器内存中的绑定上下文</param>
        /// <returns>快照列表</returns>
        public static List<BindingSnapshot> CollectFromBindingContext(BindingContext bindingContext)
        {
            var snapshots = new List<BindingSnapshot>();

            if (bindingContext == null || bindingContext.Count == 0)
            {
                SBLog.Debug(SBLogTags.Snapshot, "CollectFromBindingContext: BindingContext 为空");
                return snapshots;
            }

            foreach (var kvp in bindingContext.All)
            {
                string scopedBindingKey = kvp.Key;
                GameObject? go = kvp.Value;
                if (go == null) continue;

                var marker = go.GetComponent<SceneMarker>();
                if (marker == null)
                {
                    SBLog.Warn(SBLogTags.Snapshot,
                        "绑定 {0} 的 GameObject 上没有 SceneMarker 组件，跳过", scopedBindingKey);
                    continue;
                }

                var snapshot = CaptureMarkerSnapshot(marker, scopedBindingKey);
                snapshots.Add(snapshot);
            }

            SBLog.Info(SBLogTags.Snapshot,
                "从 BindingContext 采集完成：共 {0} 个绑定快照", snapshots.Count);

            return snapshots;
        }

        /// <summary>
        /// 从单个 SceneMarker 采集快照数据。
        /// </summary>
        /// <param name="marker">场景标记组件</param>
        /// <param name="scopedBindingKey">作用域绑定键（可为空，子 Marker 没有绑定键）</param>
        public static BindingSnapshot CaptureMarkerSnapshot(SceneMarker marker, string scopedBindingKey = "")
        {
            var t = marker.transform;

            var snapshot = new BindingSnapshot
            {
                schemaVersion = 1,
                scopedBindingKey = scopedBindingKey,
                markerId = marker.MarkerId,
                markerTypeId = marker.MarkerTypeId,
                markerName = marker.MarkerName,
                tag = marker.Tag,
                subGraphId = marker.SubGraphId,
                hierarchyGroup = InferHierarchyGroup(t),
                gameObjectName = marker.gameObject.name,
                spatial = CaptureSpatialSnapshot(marker),
                annotations = CaptureAnnotationSnapshots(marker),
                children = CaptureChildSnapshots(marker)
            };

            return snapshot;
        }

        /// <summary>
        /// 采集空间数据快照（Transform + ShapeData）。
        /// </summary>
        private static SpatialSnapshot CaptureSpatialSnapshot(SceneMarker marker)
        {
            var t = marker.transform;
            return new SpatialSnapshot
            {
                position = t.position,
                rotation = t.rotation,
                localScale = t.localScale,
                shapeDataJson = marker.SerializeShapeData()
            };
        }

        /// <summary>
        /// 采集 Marker 上所有 Annotation 的快照。
        /// </summary>
        private static AnnotationSnapshot[] CaptureAnnotationSnapshots(SceneMarker marker)
        {
            var annotations = marker.GetComponents<MarkerAnnotation>();
            if (annotations == null || annotations.Length == 0)
                return System.Array.Empty<AnnotationSnapshot>();

            var result = new AnnotationSnapshot[annotations.Length];
            for (int i = 0; i < annotations.Length; i++)
            {
                var ann = annotations[i];
                var data = new Dictionary<string, object>();
                ann.CollectExportData(data);

                result[i] = new AnnotationSnapshot
                {
                    typeId = ann.AnnotationTypeId,
                    propertiesJson = SnapshotJsonHelper.DictToJson(data)
                };
            }
            return result;
        }

        /// <summary>
        /// 递归采集子 Marker 快照（Transform 直接子级中的 SceneMarker）。
        /// </summary>
        private static BindingSnapshot[] CaptureChildSnapshots(SceneMarker parentMarker)
        {
            var children = new List<BindingSnapshot>();
            var parentTransform = parentMarker.transform;

            for (int i = 0; i < parentTransform.childCount; i++)
            {
                var childTransform = parentTransform.GetChild(i);
                var childMarker = childTransform.GetComponent<SceneMarker>();
                if (childMarker != null)
                {
                    // 子 Marker 没有独立的 scopedBindingKey
                    children.Add(CaptureMarkerSnapshot(childMarker, ""));
                }
            }

            return children.Count > 0 ? children.ToArray() : System.Array.Empty<BindingSnapshot>();
        }

        /// <summary>
        /// 从场景中采集所有未被 BindingContext 覆盖的 Marker 快照。
        /// <para>
        /// 遍历 SceneBlueprintMarkers/ 根容器下的全部 SceneMarker，
        /// 跳过已在 <paramref name="coveredMarkerIds"/> 中的 Marker（避免重复）。
        /// 未绑定的 Marker 的 scopedBindingKey 为空字符串。
        /// </para>
        /// </summary>
        /// <param name="coveredMarkerIds">已被 BindingContext 采集的 MarkerId 集合（用于去重）</param>
        /// <returns>未绑定 Marker 的快照列表</returns>
        public static List<BindingSnapshot> CollectUnboundFromScene(HashSet<string> coveredMarkerIds)
        {
            var snapshots = new List<BindingSnapshot>();

            var root = UnityEngine.GameObject.Find("SceneBlueprintMarkers");
            if (root == null)
            {
                SBLog.Debug(SBLogTags.Snapshot, "CollectUnboundFromScene: 场景中没有 SceneBlueprintMarkers 根容器");
                return snapshots;
            }

            // 遍历根容器下所有子级（分组容器），再遍历每个分组下的 Marker
            foreach (var marker in root.GetComponentsInChildren<SceneMarker>(includeInactive: true))
            {
                // 跳过已被 BindingContext 采集的 Marker
                if (!string.IsNullOrEmpty(marker.MarkerId) && coveredMarkerIds.Contains(marker.MarkerId))
                    continue;

                // 跳过子 Marker（父级也是 SceneMarker），子 Marker 由父级递归采集
                if (marker.transform.parent != null &&
                    marker.transform.parent.GetComponent<SceneMarker>() != null)
                    continue;

                var snapshot = CaptureMarkerSnapshot(marker, "");
                snapshots.Add(snapshot);

                SBLog.Debug(SBLogTags.Snapshot,
                    "采集未绑定 Marker: {0} (TypeId={1}, MarkerId={2})",
                    marker.gameObject.name, marker.MarkerTypeId, marker.MarkerId);
            }

            SBLog.Info(SBLogTags.Snapshot,
                "从场景采集未绑定 Marker 完成：共 {0} 个", snapshots.Count);

            return snapshots;
        }

        /// <summary>
        /// 从 Hierarchy 路径推断分组名。
        /// <para>
        /// 预期路径：SceneBlueprintMarkers/[分组名]/MarkerObject
        /// 如果 Marker 不在标准路径下，返回空字符串（未分组）。
        /// </para>
        /// </summary>
        private static string InferHierarchyGroup(Transform markerTransform)
        {
            // Marker 的 parent 应该是分组容器，parent.parent 应该是根容器
            var parent = markerTransform.parent;
            if (parent == null) return "";

            var grandParent = parent.parent;
            if (grandParent == null) return "";

            // 验证是否在 SceneBlueprintMarkers 根下
            if (grandParent.name != "SceneBlueprintMarkers") return "";

            string groupName = parent.name;

            // 去掉包裹的方括号（旧格式兼容）
            if (groupName.StartsWith("[") && groupName.EndsWith("]"))
                groupName = groupName.Substring(1, groupName.Length - 2);

            // [未分组] 映射为空字符串
            if (groupName == "未分组") return "";

            return groupName;
        }
    }

    /// <summary>
    /// 快照 JSON 序列化辅助 — 委托给 <see cref="MiniJson"/>。
    /// </summary>
    internal static class SnapshotJsonHelper
    {
        /// <summary>将 key-value 字典序列化为 JSON 字符串</summary>
        public static string DictToJson(IDictionary<string, object> data)
        {
            if (data == null || data.Count == 0) return "{}";
            return MiniJson.Serialize(data);
        }

        /// <summary>从 JSON 字符串反序列化为 key-value 字典</summary>
        public static Dictionary<string, object> JsonToDict(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "{}") return new Dictionary<string, object>();
            try
            {
                var parsed = MiniJson.Deserialize(json);
                if (parsed is Dictionary<string, object> dict)
                    return dict;
            }
            catch (System.Exception ex)
            {
                SBLog.Warn(SBLogTags.Snapshot,
                    "SnapshotJsonHelper.JsonToDict 解析失败: {0}", ex.Message);
            }
            return new Dictionary<string, object>();
        }
    }
}
