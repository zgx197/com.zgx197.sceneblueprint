#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Analysis.Rules
{
    /// <summary>
    /// SB005：检查孤立节点（无任何连线）。
    /// 节点的所有端口均无连线时报 Warning。
    /// 已被 SB001 标记为不可达的节点跳过（避免重复）。
    /// </summary>
    public class IsolatedNodeRule : IBlueprintRule
    {
        public string RuleId => "SB005";

        public IEnumerable<Diagnostic> Check(AnalysisContext ctx)
        {
            var graph = ctx.Graph;

            // 建立有连线的 nodeId 集合
            var connectedNodeIds = new HashSet<string>();
            foreach (var edge in graph.Edges)
            {
                var srcPort = graph.FindPort(edge.SourcePortId);
                var tgtPort = graph.FindPort(edge.TargetPortId);
                if (srcPort != null) connectedNodeIds.Add(srcPort.NodeId);
                if (tgtPort != null) connectedNodeIds.Add(tgtPort.NodeId);
            }

            foreach (var node in graph.Nodes)
            {
                if (!ctx.IsBusinessNode(node.Id)) continue;

                // 已被 SB001 标记为不可达时跳过（不可达必然孤立，避免噪声）
                if (ctx.ReachableNodeIds != null && !ctx.ReachableNodeIds.Contains(node.Id)) continue;

                if (connectedNodeIds.Contains(node.Id)) continue;

                var typeIdHint = (node.UserData as ActionNodeData)?.ActionTypeId ?? node.TypeId;
                yield return Diagnostic.Warning(RuleId,
                    $"节点 '{typeIdHint}' 是孤立节点（无任何连线）",
                    node.Id);
            }
        }
    }
}
