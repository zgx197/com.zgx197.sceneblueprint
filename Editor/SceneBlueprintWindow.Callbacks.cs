#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NodeGraph.Math;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        // ── 双向联动回调 ──

        /// <summary>
        /// 蓝图编辑器中选中节点变化时的回调。
        /// 收集选中节点关联的 MarkerId，通知 Scene View 高亮。
        /// </summary>
        private void OnBlueprintSelectionChanged()
        {
            if (_session == null) return;
            var markerIds = _session.SelectionResolver.Resolve(_session.ViewModel.Selection.SelectedNodeIds);
            SceneMarkerSelectionBridge.NotifyBlueprintSelectionChanged(markerIds);
        }

        /// <summary>场景中选中标记时的回调——在蓝图中高亮引用该标记的节点。</summary>
        private void OnSceneMarkerSelected(string markerId)
        {
            if (_session == null) return;
            var vm = _session.ViewModel;

            var marker = SceneMarkerSelectionBridge.FindMarkerInScene(markerId);
            if (marker == null)
            {
                vm.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
                vm.RequestRepaint();
                Repaint();
                return;
            }

            // M14：蓝图侧节点高亮同样受 Tag 过滤表达式约束，保持与 SceneView 可见性一致。
            if (MarkerLayerSystem.HasTagFilter
                && !Core.TagExpressionMatcher.Evaluate(MarkerLayerSystem.TagFilterExpression, marker.Tag))
            {
                vm.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
                vm.RequestRepaint();
                Repaint();
                return;
            }

            var registry = _session.ActionRegistry;
            var nodeIds = new List<string>();

            foreach (var node in vm.Graph.Nodes)
            {
                if (node.UserData is not Core.ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;

                    var storedId = data.Properties.Get<string>(prop.Key);
                    if (!string.IsNullOrEmpty(storedId) && storedId == markerId)
                    {
                        nodeIds.Add(node.Id);
                        break;
                    }
                }
            }

            if (nodeIds.Count > 0)
                vm.Selection.SelectMultiple(nodeIds);
            else
            {
                vm.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
            }
            vm.RequestRepaint();
            Repaint();
        }

        private void OnUnitySelectionChanged()
        {
            if (_session == null) return;
            var vm = _session.ViewModel;

            var go = Selection.activeGameObject;
            SBLog.Debug(SBLogTags.Selection, $"OnUnitySelectionChanged: activeGameObject={go?.name ?? "null"}");

            if (go == null)
            {
                vm.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
                vm.RequestRepaint();
                Repaint();
                return;
            }

            var markerComp = go.GetComponent<SceneMarker>();
            if (markerComp != null && !string.IsNullOrEmpty(markerComp.MarkerId))
            {
                SBLog.Debug(SBLogTags.Selection, $"是 SceneMarker: {markerComp.MarkerId}");
                SceneMarkerSelectionBridge.NotifySceneMarkerSelected(markerComp.MarkerId);
            }
            else
            {
                SBLog.Debug(SBLogTags.Selection, "非 SceneMarker，清除蓝图选中");
                vm.Selection.ClearSelection();
                SceneMarkerSelectionBridge.ClearHighlight();
                vm.RequestRepaint();
                Repaint();
            }
        }

        /// <summary>节点属性修改时的回调——刷新预览。</summary>
        private void OnNodePropertyChanged(string nodeId, ActionNodeData nodeData)
        {
            if (_session == null) return;

            string areaBindingId = nodeData.ActionTypeId == "Location.RandomArea"
                ? (nodeData.Properties.Get<string>("area") ?? "")
                : "";

            SBLog.Info(SBLogTags.Pipeline,
                "OnNodePropertyChanged: node={0}, action={1}, areaId='{2}', previewContext={3}",
                nodeId, nodeData.ActionTypeId, areaBindingId, GetPreviewContextId());

            _session.NotifyNodePropertyChanged(nodeId, nodeData);
        }

        /// <summary>场景中双击标记时的回调——在蓝图中聚焦到引用该标记的节点。</summary>
        private void OnSceneMarkerDoubleClicked(string markerId)
        {
            if (_session == null) return;
            var vm = _session.ViewModel;

            var nodeIds = SceneMarkerSelectionBridge.FindNodesReferencingMarker(vm.Graph, markerId);
            if (nodeIds.Count == 0) return;

            vm.Selection.Select(nodeIds[0]);
            var node = vm.Graph.FindNode(nodeIds[0]);
            if (node != null)
            {
                vm.PanOffset = new Vec2(
                    position.width / 2f - node.Position.X * vm.ZoomLevel,
                    position.height / 2f - node.Position.Y * vm.ZoomLevel);
            }

            vm.RequestRepaint();
            Repaint();
        }
    }
}
