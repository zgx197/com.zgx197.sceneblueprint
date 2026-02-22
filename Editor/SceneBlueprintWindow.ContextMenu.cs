#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NodeGraph.Commands;
using NodeGraph.Core;
using NodeGraph.Math;
using SceneBlueprint.Editor.Templates;
using GraphPort = NodeGraph.Core.Port;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        // ── 上下文菜单回调（由框架层 ContextMenuHandler 触发）──

        /// <summary>
        /// 右键点击画布空白区域的回调。
        /// 使用 Unity GenericMenu 按分类显示所有已注册的 Action 节点类型。
        /// </summary>
        private void OnCanvasContextMenu(Vec2 canvasPos)
        {
            if (_session == null) return;
            var vm = _session.ViewModel;

            var menu = new GenericMenu();

            var grouped = _session.Profile.NodeTypes.GetAll()
                .GroupBy(def => string.IsNullOrEmpty(def.Category) ? "未分类" : def.Category)
                .OrderBy(g => CategoryRegistry.GetSortOrder(g.Key))
                .ThenBy(g => g.Key);

            foreach (var group in grouped)
            {
                string categoryDisplayName = CategoryRegistry.GetDisplayName(group.Key);

                foreach (var typeDef in group.OrderBy(d => d.DisplayName))
                {
                    string menuPath = $"{categoryDisplayName}/{typeDef.DisplayName}";
                    var capturedTypeId = typeDef.TypeId;
                    var capturedPos = canvasPos;

                    menu.AddItem(new GUIContent(menuPath), false, () =>
                    {
                        if (_session == null) return;
                        var addNodeCmd = new AddNodeCommand(capturedTypeId, capturedPos);
                        _session.ViewModel.Commands.Execute(addNodeCmd);
                        if (!string.IsNullOrEmpty(addNodeCmd.CreatedNodeId))
                            _session.MarkPreviewDirtyAll("CanvasContext.AddNode");
                        _session.ViewModel.RequestRepaint();
                        Repaint();
                    });
                }
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("(无可用节点类型) (No Available Node Types)"));
            }

            // 从模板创建子蓝图
            var templatesGrouped = BlueprintTemplateUtils.FindAllTemplatesGrouped();
            if (templatesGrouped.Count > 0)
            {
                menu.AddSeparator("");
                foreach (var kvp in templatesGrouped.OrderBy(k => k.Key))
                {
                    foreach (var tmpl in kvp.Value.OrderBy(t => t.DisplayName))
                    {
                        var capturedTemplate = tmpl;
                        var capturedPos = canvasPos;
                        string displayName = string.IsNullOrEmpty(tmpl.DisplayName) ? tmpl.name : tmpl.DisplayName;
                        string menuPath = $"从模板创建 (From Template)/{kvp.Key}/{displayName}";
                        menu.AddItem(new GUIContent(menuPath), false, () =>
                        {
                            if (_session == null) return;
                            var result = BlueprintTemplateUtils.InstantiateTemplate(
                                _session.ViewModel.Graph, capturedTemplate,
                                CreateGraphSerializer(), capturedPos);
                            if (result != null)
                            {
                                _session.MarkPreviewDirtyAll("CanvasContext.InstantiateTemplate");
                                _session.ViewModel.RequestRepaint();
                                Repaint();
                            }
                        });
                    }
                }
            }

            // 如果有多个节点被选中，添加"创建子蓝图"选项
            if (_session.ViewModel.Selection.SelectedNodeIds.Count >= 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("创建子蓝图 (Create SubGraph)"), false, () =>
                {
                    if (_session == null) return;
                    var selectedIds = _session.ViewModel.Selection.SelectedNodeIds.ToList();
                    if (selectedIds.Count == 0) return;

                    var name = EditorInputDialog.Show("创建子蓝图", "请输入子蓝图名称：", "新子蓝图");
                    if (!string.IsNullOrWhiteSpace(name))
                        _session.SubGraphCtrl.GroupSelected(name, selectedIds);
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>右键点击节点的回调。显示节点操作菜单（删除、复制等）。</summary>
        private void OnNodeContextMenu(Node node, Vec2 canvasPos)
        {
            if (_session == null) return;
            var vm = _session.ViewModel;

            var menu = new GenericMenu();
            var capturedNodeId = node.Id;

            var frame = vm.Graph.FindContainerSubGraphFrame(capturedNodeId);
            bool isRepNode = frame != null && frame.RepresentativeNodeId == capturedNodeId;

            if (isRepNode && frame != null)
            {
                var capturedFrameId = frame.Id;
                var isCollapsed = frame.IsCollapsed;

                menu.AddItem(new GUIContent(isCollapsed ? "展开子蓝图 (Expand)" : "折叠子蓝图 (Collapse)"), false, () =>
                {
                    if (_session == null) return;
                    _session.SubGraphCtrl.Toggle(capturedFrameId);
                });

                menu.AddItem(new GUIContent("解散子蓝图 (Ungroup)"), false, () =>
                {
                    if (_session == null) return;
                    _session.SubGraphCtrl.Ungroup(capturedFrameId);
                });

                menu.AddItem(new GUIContent("保存为模板 (Save as Template)..."), false, () =>
                {
                    if (_session == null) return;
                    var targetFrame = _session.ViewModel.Graph.SubGraphFrames.FirstOrDefault(f => f.Id == capturedFrameId);
                    if (targetFrame == null) return;
                    BlueprintTemplateUtils.SaveAsTemplate(_session.ViewModel.Graph, targetFrame, CreateGraphSerializer());
                });

                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent("删除节点 (Delete)"), false, () =>
            {
                if (_session == null) return;
                var deletedNodeIds = _session.ViewModel.Selection.SelectedNodeIds.ToList();
                _session.ViewModel.DeleteSelected();
                if (deletedNodeIds.Count > 0)
                    _session.NotifyNodesDeleted(deletedNodeIds);
                Repaint();
            });

            if (vm.Selection.SelectedNodeIds.Count > 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("创建子蓝图 (Create SubGraph)"), false, () =>
                {
                    if (_session == null) return;
                    var selectedIds = _session.ViewModel.Selection.SelectedNodeIds.ToList();
                    var subName = EditorInputDialog.Show("创建子蓝图", "请输入子蓝图名称：", "新子蓝图");
                    if (!string.IsNullOrWhiteSpace(subName))
                        _session.SubGraphCtrl.GroupSelected(subName, selectedIds);
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>右键点击端口的回调。显示该端口连线管理菜单。</summary>
        private void OnPortContextMenu(GraphPort port, Vec2 canvasPos)
        {
            if (_session == null) return;
            var vm = _session.ViewModel;

            var edges = vm.Graph.GetEdgesForPort(port.Id).ToList();
            if (edges.Count == 0) return;

            var menu = new GenericMenu();

            foreach (var edge in edges)
            {
                var otherPortId = edge.SourcePortId == port.Id ? edge.TargetPortId : edge.SourcePortId;
                var otherPort = vm.Graph.FindPort(otherPortId);
                var otherNode = otherPort != null ? vm.Graph.FindNode(otherPort.NodeId) : null;

                string label = otherNode != null
                    ? $"断开连线 (Disconnect): {otherNode.TypeId}.{otherPort!.Name}"
                    : $"断开连线 (Disconnect): {otherPortId}";

                var capturedEdgeId = edge.Id;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    if (_session == null) return;
                    _session.ViewModel.Commands.Execute(new DisconnectCommand(capturedEdgeId));
                    _session.ViewModel.RequestRepaint(); Repaint();
                });
            }

            if (edges.Count > 1)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("断开所有连线 (Disconnect All)"), false, () =>
                {
                    if (_session == null) return;
                    using (_session.ViewModel.Commands.BeginCompound("断开所有连线"))
                    {
                        foreach (var e in edges)
                            _session.ViewModel.Commands.Execute(new DisconnectCommand(e.Id));
                    }
                    _session.ViewModel.RequestRepaint(); Repaint();
                });
            }

            menu.ShowAsContext();
        }
    }
}
