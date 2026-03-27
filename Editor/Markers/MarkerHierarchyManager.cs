#nullable enable
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// 场景标记 Hierarchy 自动分组管理器。
    /// <para>
    /// 管理场景中标记对象的组织结构：
    /// <code>
    /// SceneBlueprintMarkers/              ← 根容器
    ///   ├── [分组名称]/                   ← 策划自定义分组（与子蓝图解耦）
    ///   │   ├── SpawnArea_走廊中段
    ///   │   └── SpawnPoint_01
    ///   └── [未分组]/                     ← 未指定分组的标记
    ///       └── TriggerZone_关卡入口
    /// </code>
    /// </para>
    /// </summary>
    public static class MarkerHierarchyManager
    {
        private const string RootName = "SceneBlueprintMarkers";
        private const string UngroupedName = "[未分组]";

        /// <summary>
        /// 获取或创建标记根容器 GameObject。
        /// </summary>
        public static GameObject GetOrCreateRoot()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "创建标记根容器");
            }
            return root;
        }

        /// <summary>
        /// 获取或创建指定名称的分组容器。
        /// </summary>
        /// <param name="groupName">分组名称（策划自定义，与子蓝图无关；为空或 null 时使用"未分组"）</param>
        public static Transform GetOrCreateGroup(string? groupName)
        {
            var root = GetOrCreateRoot();
            string displayName = string.IsNullOrEmpty(groupName) ? UngroupedName : $"[{groupName}]";

            var groupTransform = root.transform.Find(displayName);
            if (groupTransform == null)
            {
                var groupObj = new GameObject(displayName);
                Undo.RegisterCreatedObjectUndo(groupObj, $"创建标记分组 {displayName}");
                groupObj.transform.SetParent(root.transform);
                groupTransform = groupObj.transform;
            }
            return groupTransform;
        }

        /// <summary>
        /// 创建标记 GameObject 并放入正确的分组。
        /// </summary>
        /// <typeparam name="T">标记组件类型</typeparam>
        /// <param name="markerName">标记名称</param>
        /// <param name="position">世界坐标位置</param>
        /// <param name="tag">Tag 标签</param>
        /// <param name="subGraphId">所属子蓝图 ID</param>
        /// <param name="groupName">Hierarchy 分组名称（策划自定义，与子蓝图解耦）</param>
        /// <returns>创建的标记组件</returns>
        public static T CreateMarker<T>(
            string markerName,
            Vector3 position,
            string tag = "",
            string subGraphId = "",
            string? groupName = null) where T : SceneMarker
        {
            var parent = GetOrCreateGroup(groupName);

            // 生成 GameObject 名称：MarkerType_MarkerName
            string typeName = typeof(T).Name.Replace("Marker", "");
            string objName = string.IsNullOrEmpty(markerName)
                ? typeName
                : $"{typeName}_{markerName}";

            var go = new GameObject(objName);
            Undo.RegisterCreatedObjectUndo(go, $"创建标记 {objName}");

            go.transform.SetParent(parent);
            go.transform.position = position;

            var marker = go.AddComponent<T>();

            // 必须通过 SerializedObject API 修改序列化字段。
            // Undo.RegisterCreatedObjectUndo 会重建 C# wrapper 对象，
            // 导致直接赋值公共字段被 Undo 快照覆盖为默认值。
            // 详见文档：Knowledge/Developer/D4_UndoFieldOverwrite.md
            var so = new SerializedObject(marker);
            so.FindProperty("MarkerName").stringValue = markerName;
            so.FindProperty("Tag").stringValue = tag;
            so.FindProperty("_subGraphId").stringValue = subGraphId;
            so.ApplyModifiedProperties();

            return marker;
        }

        /// <summary>
        /// 创建标记 GameObject 并放入正确的分组（非泛型版本，支持运行时 Type）。
        /// </summary>
        /// <param name="componentType">标记组件类型（必须是 SceneMarker 的子类）</param>
        /// <param name="markerName">标记名称</param>
        /// <param name="position">世界坐标位置</param>
        /// <param name="tag">Tag 标签</param>
        /// <param name="subGraphId">所属子蓝图 ID</param>
        /// <param name="groupName">Hierarchy 分组名称（策划自定义，与子蓝图解耦）</param>
        /// <returns>创建的标记组件</returns>
        public static SceneMarker CreateMarker(
            System.Type componentType,
            string markerName,
            Vector3 position,
            string tag = "",
            string subGraphId = "",
            string? groupName = null)
        {
            var parent = GetOrCreateGroup(groupName);

            string typeName = componentType.Name.Replace("Marker", "");
            string objName = string.IsNullOrEmpty(markerName)
                ? typeName
                : $"{typeName}_{markerName}";

            var go = new GameObject(objName);
            Undo.RegisterCreatedObjectUndo(go, $"创建标记 {objName}");

            go.transform.SetParent(parent);
            go.transform.position = position;

            var marker = (SceneMarker)go.AddComponent(componentType);

            // 必须通过 SerializedObject API 修改序列化字段（同上，防止 Undo 快照覆盖）
            var so = new SerializedObject(marker);
            so.FindProperty("MarkerName").stringValue = markerName;
            so.FindProperty("Tag").stringValue = tag;
            so.FindProperty("_subGraphId").stringValue = subGraphId;
            so.ApplyModifiedProperties();

            return marker;
        }

        /// <summary>
        /// 清理空的分组容器（没有子对象的分组）。
        /// </summary>
        public static void CleanupEmptyGroups()
        {
            var root = GameObject.Find(RootName);
            if (root == null) return;

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (child.childCount == 0)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            // 如果根容器也空了，删除它
            if (root.transform.childCount == 0)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        /// <summary>
        /// 销毁 SceneBlueprintMarkers 根容器及其所有子对象（Marker + 分组）。
        /// 用于编辑器会话结束时清理场景。
        /// </summary>
        public static void DestroyAll()
        {
            var root = GameObject.Find(RootName);
            if (root == null) return;

            Undo.DestroyObjectImmediate(root);
        }

        /// <summary>
        /// 查找场景中所有标记。
        /// </summary>
        public static SceneMarker[] FindAllMarkers()
        {
            return Object.FindObjectsByType<SceneMarker>(FindObjectsSortMode.None);
        }

        /// <summary>
        /// 通过 MarkerId 查找标记。
        /// </summary>
        public static SceneMarker? FindMarkerById(string markerId)
        {
            if (string.IsNullOrEmpty(markerId)) return null;
            foreach (var marker in FindAllMarkers())
            {
                if (marker.MarkerId == markerId) return marker;
            }
            return null;
        }
    }
}
