#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// 蓝图 ↔ 场景双向联动事件桥。
    /// <para>
    /// 静态事件总线，蓝图编辑器和 Scene View 各自订阅对方的事件：
    /// <list type="bullet">
    ///   <item>蓝图→场景：选中节点 → 高亮关联标记，双击节点 → 聚焦标记</item>
    ///   <item>场景→蓝图：选中标记 → 高亮引用节点，双击标记 → 聚焦节点</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class SceneMarkerSelectionBridge
    {
        // ─── 蓝图 → 场景方向的事件 ───

        /// <summary>
        /// 蓝图编辑器中节点选中发生变化时触发。
        /// 参数：选中节点关联的 MarkerId 列表。
        /// </summary>
        public static event Action<IReadOnlyList<string>>? OnHighlightMarkersRequested;

        /// <summary>
        /// 蓝图编辑器中双击节点时触发——请求 Scene View 聚焦到标记位置。
        /// 参数：要聚焦的 MarkerId 列表。
        /// </summary>
        public static event Action<IReadOnlyList<string>>? OnFrameMarkersRequested;

        // ─── 场景 → 蓝图方向的事件 ───

        /// <summary>
        /// 场景中选中标记时触发——请求蓝图编辑器高亮引用该标记的节点。
        /// 参数：选中标记的 MarkerId。
        /// </summary>
        public static event Action<string>? OnHighlightNodesForMarkerRequested;

        /// <summary>
        /// 场景中双击标记时触发——请求蓝图编辑器聚焦到引用该标记的节点。
        /// 参数：双击的标记 MarkerId。
        /// </summary>
        public static event Action<string>? OnFrameNodeForMarkerRequested;

        // ─── 当前高亮状态（供 GizmoDrawer 读取） ───

        private static readonly HashSet<string> _highlightedMarkerIds = new();

        /// <summary>当前需要高亮的 MarkerId 集合（蓝图选中节点关联的标记）</summary>
        public static IReadOnlyCollection<string> HighlightedMarkerIds => _highlightedMarkerIds;

        /// <summary>判断某个标记是否应该高亮</summary>
        public static bool IsMarkerHighlighted(string markerId)
        {
            return !string.IsNullOrEmpty(markerId) && _highlightedMarkerIds.Contains(markerId);
        }

        // ─── 蓝图侧调用（由 SceneBlueprintWindow 在选中变化时调用） ───

        /// <summary>
        /// 通知：蓝图中选中了节点，这些节点关联的标记需要在场景中高亮。
        /// </summary>
        public static void NotifyBlueprintSelectionChanged(IReadOnlyList<string> markerIds)
        {
            _highlightedMarkerIds.Clear();
            if (markerIds != null)
            {
                foreach (var id in markerIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        _highlightedMarkerIds.Add(id);
                }
            }

            OnHighlightMarkersRequested?.Invoke(markerIds ?? Array.Empty<string>());

            // 启动/停止持续重绘（脉冲动画需要每帧刷新 Scene View）
            if (_highlightedMarkerIds.Count > 0)
                StartContinuousRepaint();
            else
                StopContinuousRepaint();
        }

        /// <summary>
        /// 通知：蓝图中双击了节点，请求 Scene View 聚焦到关联标记。
        /// </summary>
        public static void NotifyFrameMarkers(IReadOnlyList<string> markerIds)
        {
            OnFrameMarkersRequested?.Invoke(markerIds);
        }

        /// <summary>清除所有高亮</summary>
        public static void ClearHighlight()
        {
            _highlightedMarkerIds.Clear();
            StopContinuousRepaint();
        }

        // ─── 持续重绘（脉冲动画） ───

        private static bool _isRepainting;

        private static void StartContinuousRepaint()
        {
            if (_isRepainting) return;
            _isRepainting = true;
            EditorApplication.update += ContinuousRepaintTick;
        }

        private static void StopContinuousRepaint()
        {
            if (!_isRepainting) return;
            _isRepainting = false;
            EditorApplication.update -= ContinuousRepaintTick;
            SceneView.RepaintAll(); // 最后一帧刷新，恢复原始状态
        }

        private static void ContinuousRepaintTick()
        {
            if (_highlightedMarkerIds.Count == 0)
            {
                StopContinuousRepaint();
                return;
            }
            SceneView.RepaintAll();
        }

        // ─── 场景侧调用（由 Scene View Selection 回调触发） ───

        /// <summary>
        /// 通知：场景中选中了标记，请求蓝图编辑器高亮引用该标记的节点。
        /// </summary>
        public static void NotifySceneMarkerSelected(string markerId)
        {
            OnHighlightNodesForMarkerRequested?.Invoke(markerId);
        }

        /// <summary>
        /// 通知：场景中双击了标记，请求蓝图编辑器聚焦到对应节点。
        /// </summary>
        public static void NotifyFrameNodeForMarker(string markerId)
        {
            OnFrameNodeForMarkerRequested?.Invoke(markerId);
        }

        // ─── 工具方法：从 Graph 中查找引用了指定 MarkerId 的节点 ───

        /// <summary>
        /// 在 Graph 中查找所有引用了指定 markerId 的节点 ID。
        /// 通过检查节点的 ActionNodeData 中的属性值来匹配。
        /// </summary>
        public static List<string> FindNodesReferencingMarker(Graph graph, string markerId)
        {
            var result = new List<string>();
            if (graph == null || string.IsNullOrEmpty(markerId)) return result;

            foreach (var node in graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;

                // 检查属性值中是否包含该 markerId
                foreach (var kvp in data.Properties.All)
                {
                    if (kvp.Value is string strVal && strVal == markerId)
                    {
                        result.Add(node.Id);
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 从场景中查找指定 MarkerId 对应的 SceneMarker 组件。
        /// </summary>
        public static SceneMarker? FindMarkerInScene(string markerId)
        {
            if (string.IsNullOrEmpty(markerId)) return null;

            var allMarkers = UnityEngine.Object.FindObjectsOfType<SceneMarker>();
            return allMarkers.FirstOrDefault(m => m.MarkerId == markerId);
        }

        /// <summary>
        /// 从场景中查找所有指定 MarkerId 对应的 SceneMarker 组件。
        /// </summary>
        public static List<SceneMarker> FindMarkersInScene(IReadOnlyList<string> markerIds)
        {
            if (markerIds == null || markerIds.Count == 0) return new List<SceneMarker>();

            var idSet = new HashSet<string>(markerIds);
            var allMarkers = UnityEngine.Object.FindObjectsOfType<SceneMarker>();
            return allMarkers.Where(m => idSet.Contains(m.MarkerId)).ToList();
        }

        /// <summary>
        /// 在 Scene View 中聚焦到一组标记的包围盒中心。
        /// </summary>
        public static void FrameMarkersInSceneView(IReadOnlyList<string> markerIds)
        {
            var markers = FindMarkersInScene(markerIds);
            if (markers.Count == 0) return;

            // 计算所有标记的包围盒
            var bounds = new Bounds(markers[0].GetRepresentativePosition(), Vector3.zero);
            for (int i = 1; i < markers.Count; i++)
            {
                bounds.Encapsulate(markers[i].GetRepresentativePosition());
            }

            // 扩展一点以确保标记都在视野内
            bounds.Expand(5f);

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Frame(bounds, false);
            }
        }
    }
}
