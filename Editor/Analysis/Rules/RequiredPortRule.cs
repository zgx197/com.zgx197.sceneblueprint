#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Analysis.Rules
{
    /// <summary>
    /// SB002：检查 Required DataIn 端口连线。
    /// 对每个可达的业务节点，通过 ActionRegistry 查询其 ActionDefinition，
    /// 找到所有 Required=true 的 DataIn 端口，如果无入边则报 Error。
    ///
    /// 依赖：需在 SB001（ReachabilityRule）之后执行，跳过不可达节点。
    /// </summary>
    public class RequiredPortRule : IBlueprintRule
    {
        public string RuleId => "SB002";

        public IEnumerable<Diagnostic> Check(AnalysisContext ctx)
        {
            var graph = ctx.Graph;

            // 建立 portId → 入边集合（快速判断端口是否有连线）
            var portsWithInEdge = new HashSet<string>();
            foreach (var edge in graph.Edges)
                portsWithInEdge.Add(edge.TargetPortId);

            foreach (var node in graph.Nodes)
            {
                if (!ctx.IsBusinessNode(node.Id)) continue;

                // 跳过不可达节点（已由 SB001 报错，避免重复噪声）
                if (ctx.ReachableNodeIds != null && !ctx.ReachableNodeIds.Contains(node.Id)) continue;

                if (node.UserData is not ActionNodeData data) continue;
                if (!ctx.ActionRegistry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var sbPort in actionDef.Ports)
                {
                    if (!sbPort.Required) continue;
                    if (sbPort.Direction != PortDirection.Input) continue;
                    if (sbPort.Kind != PortKind.Data) continue;

                    // 在 graph 节点的端口中找匹配的 DataIn 端口（通过 SemanticId 匹配）
                    string? matchedPortId = null;
                    foreach (var p in node.Ports)
                    {
                        if (p.Direction == PortDirection.Input
                            && p.Kind == PortKind.Data
                            && p.SemanticId == sbPort.Id)
                        {
                            matchedPortId = p.Id;
                            break;
                        }
                    }

                    if (matchedPortId == null) continue;
                    if (portsWithInEdge.Contains(matchedPortId)) continue;

                    var displayName = string.IsNullOrEmpty(sbPort.DisplayName) ? sbPort.Id : sbPort.DisplayName;
                    yield return Diagnostic.Error(RuleId,
                        $"节点 '{data.ActionTypeId}' 的必填端口 '{displayName}' 没有连线",
                        node.Id, sbPort.Id);
                }
            }
        }
    }
}
