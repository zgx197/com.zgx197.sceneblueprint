#nullable enable
using System.Linq;
using System.Text;
using UnityEngine;
using SceneBlueprint.Contract.Knowledge;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Session;
using SceneBlueprint.Runtime.Knowledge;

namespace SceneBlueprint.Editor.Knowledge
{
    /// <summary>
    /// 编辑器层的蓝图上下文采集实现。
    /// 从当前活跃的 <see cref="Session.BlueprintEditorSession"/> 中采集实时状态。
    /// </summary>
    public class EditorBlueprintContextProvider : IBlueprintContextProvider
    {
        /// <summary>
        /// 获取当前活跃的 Session（由 SceneBlueprintWindow 设置）。
        /// </summary>
        internal BlueprintEditorSession? ActiveSession { get; set; }

        public bool HasActiveSession => ActiveSession != null && ActiveSession.ViewModel != null;

        public BlueprintContext GetCurrentContext()
        {
            var ctx = new BlueprintContext();
            var session = ActiveSession;
            if (session == null) return ctx;

            var vm = session.ViewModel;
            if (vm == null) return ctx;

            var graph = vm.Graph;

            // 蓝图名称
            ctx.BlueprintName = session.CurrentAsset != null ? session.CurrentAsset.BlueprintName : "";

            // 节点统计
            ctx.NodeCount = graph.Nodes.Count;

            // 节点列表摘要（按 TypeId 分组计数）
            var nodeGroups = graph.Nodes
                .Where(n => n.UserData is ActionNodeData)
                .Select(n => ((ActionNodeData)n.UserData!).ActionTypeId)
                .Where(t => !string.IsNullOrEmpty(t))
                .GroupBy(t => t)
                .Select(g => $"{g.Key} × {g.Count()}")
                .ToArray();
            ctx.NodeListSummary = nodeGroups.Length > 0
                ? string.Join(", ", nodeGroups)
                : "(无节点)";

            // 选中节点信息
            var selectedIds = vm.Selection.SelectedNodeIds.ToList();
            if (selectedIds.Count == 1)
            {
                var node = graph.Nodes.FirstOrDefault(n => n.Id == selectedIds[0]);
                if (node != null && node.UserData is ActionNodeData actionData)
                {
                    ctx.SelectedNodeTypeId = actionData.ActionTypeId;

                    // 从 ActionRegistry 获取显示名
                    if (session.ActionRegistry != null &&
                        session.ActionRegistry.TryGet(actionData.ActionTypeId, out var def))
                    {
                        ctx.SelectedNodeDisplayName = def.DisplayName;
                    }
                    else
                    {
                        ctx.SelectedNodeDisplayName = actionData.ActionTypeId;
                    }

                    // 属性摘要
                    var propSb = new StringBuilder();
                    if (actionData.Properties != null)
                    {
                        foreach (var kvp in actionData.Properties.All)
                        {
                            if (propSb.Length > 0) propSb.Append(", ");
                            propSb.Append($"{kvp.Key}={kvp.Value}");
                        }
                    }
                    ctx.SelectedNodeProperties = propSb.ToString();
                }
            }

            // 校验问题
            var report = session.LastAnalysisReport;
            if (report != null && report.Diagnostics.Count > 0)
            {
                var issueSb = new StringBuilder();
                foreach (var d in report.Diagnostics)
                {
                    if (issueSb.Length > 0) issueSb.AppendLine();
                    issueSb.Append($"[{d.Severity}] {d.Message}");
                }
                ctx.ValidationIssues = issueSb.ToString();
            }

            return ctx;
        }
    }
}
