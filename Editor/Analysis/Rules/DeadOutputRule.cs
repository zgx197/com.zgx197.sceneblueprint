#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Analysis.Rules
{
    /// <summary>
    /// SB004：检查无消费者的 DataOut 端口（死输出）。
    /// DataOut 端口如果没有任何出边，说明策划可能遗漏了连线，报 Warning。
    ///
    /// 说明：Control 端口不检查（有多条分支时部分端口不连线是合法的）。
    /// </summary>
    public class DeadOutputRule : IBlueprintRule
    {
        public string RuleId => "SB004";

        public IEnumerable<Diagnostic> Check(AnalysisContext ctx)
        {
            var graph = ctx.Graph;

            // 建立有出边的 portId 集合
            var portsWithOutEdge = new HashSet<string>();
            foreach (var edge in graph.Edges)
                portsWithOutEdge.Add(edge.SourcePortId);

            foreach (var node in graph.Nodes)
            {
                if (!ctx.IsBusinessNode(node.Id)) continue;

                // 跳过不可达节点（减少噪声）
                if (ctx.ReachableNodeIds != null && !ctx.ReachableNodeIds.Contains(node.Id)) continue;

                if (node.UserData is not ActionNodeData data) continue;

                foreach (var port in node.Ports)
                {
                    if (port.Direction != PortDirection.Output) continue;
                    if (port.Kind != PortKind.Data) continue;
                    if (portsWithOutEdge.Contains(port.Id)) continue;

                    yield return Diagnostic.Warning(RuleId,
                        $"节点 '{data.ActionTypeId}' 的 DataOut 端口 '{port.SemanticId}' 无消费者（可能遗漏连线）",
                        node.Id, port.SemanticId);
                }
            }
        }
    }
}
