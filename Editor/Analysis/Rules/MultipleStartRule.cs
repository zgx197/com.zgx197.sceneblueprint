#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace SceneBlueprint.Editor.Analysis.Rules
{
    /// <summary>
    /// SB003：检查 Flow.Start 节点数量。
    /// 蓝图必须有且仅有一个 Flow.Start 入口节点。
    /// 此规则优先执行，使后续规则可假设 Start 节点唯一。
    /// </summary>
    public class MultipleStartRule : IBlueprintRule
    {
        public string RuleId => "SB003";

        private const string FlowStartTypeId = "Flow.Start";

        public IEnumerable<Diagnostic> Check(AnalysisContext ctx)
        {
            var startNodes = ctx.Graph.Nodes
                .Where(n => n.TypeId == FlowStartTypeId && ctx.IsBusinessNode(n.Id))
                .ToList();

            if (startNodes.Count == 0)
            {
                yield return Diagnostic.Error(RuleId, "蓝图缺少 Flow.Start 入口节点");
            }
            else if (startNodes.Count > 1)
            {
                yield return Diagnostic.Error(RuleId,
                    $"蓝图有 {startNodes.Count} 个 Flow.Start 节点，应仅有一个");

                foreach (var node in startNodes)
                    yield return Diagnostic.Error(RuleId, $"重复的 Flow.Start 节点", node.Id);
            }
        }
    }
}
