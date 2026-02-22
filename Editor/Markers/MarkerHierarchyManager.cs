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
    ///   ├── [子蓝图名称]/                 ← 按子蓝图分组
    ///   │   ├── SpawnArea_走廊中段
    ///   │   └── SpawnPoint_01
    ///   └── [未分组]/                     ← 顶层节点的标记
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
        /// 获取或创建指定子蓝图的分组容器。
        /// </summary>
        /// <param name="subGraphName">子蓝图名称（为空或 null 时使用"未分组"）</param>
        public static Transform GetOrCreateGroup(string? subGraphName)
        {
            var root = GetOrCreateRoot();
            string groupName = string.IsNullOrEmpty(subGraphName) ? UngroupedName : $"[{subGraphName}]";

            var groupTransform = root.transform.Find(groupName);
            if (groupTransform == null)
            {
                var groupObj = new GameObject(groupName);
                Undo.RegisterCreatedObjectUndo(groupObj, $"创建标记分组 {groupName}");
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
        /// <param name="subGraphName">所属子蓝图名称（用于 Hierarchy 分组显示）</param>
        /// <returns>创建的标记组件</returns>
        public static T CreateMarker<T>(
            string markerName,
            Vector3 position,
            string tag = "",
            string subGraphId = "",
            string? subGraphName = null) where T : SceneMarker
        {
            var parent = GetOrCreateGroup(subGraphName);

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
            marker.MarkerName = markerName;
            marker.Tag = tag;
            marker.SubGraphId = subGraphId;

            // MarkerId 由 SceneMarker.Reset() 自动生成

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
        /// <param name="subGraphName">所属子蓝图名称（用于 Hierarchy 分组显示）</param>
        /// <returns>创建的标记组件</returns>
        public static SceneMarker CreateMarker(
            System.Type componentType,
            string markerName,
            Vector3 position,
            string tag = "",
            string subGraphId = "",
            string? subGraphName = null)
        {
            var parent = GetOrCreateGroup(subGraphName);

            string typeName = componentType.Name.Replace("Marker", "");
            string objName = string.IsNullOrEmpty(markerName)
                ? typeName
                : $"{typeName}_{markerName}";

            var go = new GameObject(objName);
            Undo.RegisterCreatedObjectUndo(go, $"创建标记 {objName}");

            go.transform.SetParent(parent);
            go.transform.position = position;

            var marker = (SceneMarker)go.AddComponent(componentType);
            marker.MarkerName = markerName;
            marker.Tag = tag;
            marker.SubGraphId = subGraphId;

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
