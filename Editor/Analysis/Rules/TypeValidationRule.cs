#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Analysis.Rules
{
    /// <summary>
    /// SB006：调用 ActionDefinition.Validator 执行类型级自定义验证。
    /// 对每个可达的业务节点，若其 ActionDefinition 配置了 IActionValidator，
    /// 则组装 NodeValidationContext 并调用，将结果转为 Diagnostic。
    ///
    /// 依赖：需在 SB001（ReachabilityRule）之后执行，跳过不可达节点。
    /// </summary>
    public class TypeValidationRule : IBlueprintRule
    {
        public string RuleId => "SB006";

        public IEnumerable<Diagnostic> Check(AnalysisContext ctx)
        {
            var graph = ctx.Graph;

            // 建立 portId → semanticId 快速查表（用于收集已连接端口的 semanticId）
            var portIdToSemantic = new Dictionary<string, string>();
            foreach (var node in graph.Nodes)
                foreach (var port in node.Ports)
                    portIdToSemantic[port.Id] = port.SemanticId;

            // 建立 nodeId → 已连接端口 semanticId 集合
            var nodeConnectedSemantics = new Dictionary<string, HashSet<string>>();
            foreach (var edge in graph.Edges)
            {
                if (portIdToSemantic.TryGetValue(edge.SourcePortId, out var srcSemantic))
                {
                    var srcNodeId = graph.FindPort(edge.SourcePortId)?.NodeId;
                    if (srcNodeId != null)
                    {
                        if (!nodeConnectedSemantics.TryGetValue(srcNodeId, out var set))
                            nodeConnectedSemantics[srcNodeId] = set = new HashSet<string>();
                        set.Add(srcSemantic);
                    }
                }
                if (portIdToSemantic.TryGetValue(edge.TargetPortId, out var tgtSemantic))
                {
                    var tgtNodeId = graph.FindPort(edge.TargetPortId)?.NodeId;
                    if (tgtNodeId != null)
                    {
                        if (!nodeConnectedSemantics.TryGetValue(tgtNodeId, out var set))
                            nodeConnectedSemantics[tgtNodeId] = set = new HashSet<string>();
                        set.Add(tgtSemantic);
                    }
                }
            }

            foreach (var node in graph.Nodes)
            {
                if (!ctx.IsBusinessNode(node.Id)) continue;

                // 跳过不可达节点（已由 SB001 报错，避免重复噪声）
                if (ctx.ReachableNodeIds != null && !ctx.ReachableNodeIds.Contains(node.Id)) continue;

                if (node.UserData is not ActionNodeData data) continue;
                if (!ctx.ActionRegistry.TryGet(data.ActionTypeId, out var actionDef)) continue;
                if (actionDef.Validator == null) continue;

                var connectedSemantics = nodeConnectedSemantics.TryGetValue(node.Id, out var s)
                    ? (IReadOnlyCollection<string>)s
                    : System.Array.Empty<string>();

                var validationCtx = new NodeValidationContext(
                    node.Id,
                    actionDef,
                    connectedSemantics,
                    data.Properties);

                foreach (var issue in actionDef.Validator.Validate(validationCtx))
                {
                    if (issue.IsError)
                        yield return Diagnostic.Error(RuleId, issue.Message, node.Id, issue.PortId);
                    else
                        yield return Diagnostic.Warning(RuleId, issue.Message, node.Id, issue.PortId);
                }
            }
        }
    }
}
