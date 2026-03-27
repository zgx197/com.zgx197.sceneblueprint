#nullable enable
using System.Collections.Generic;
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

            foreach (var node in graph.Nodes)
            {
                if (!ctx.IsBusinessNode(node.Id)) continue;

                // 跳过不可达节点（已由 SB001 报错，避免重复噪声）
                if (ctx.ReachableNodeIds != null && !ctx.ReachableNodeIds.Contains(node.Id)) continue;

                if (node.UserData is not ActionNodeData data) continue;
                if (!ctx.ActionRegistry.TryGet(data.ActionTypeId, out var actionDef)) continue;
                if (!ActionDefinitionValidationSupport.HasValidationHooks(actionDef)) continue;

                var result = ActionDefinitionValidationSupport.EvaluateResult(
                    node.Id,
                    actionDef,
                    graph,
                    data.Properties,
                    variables: ctx.Variables);

                foreach (var issue in result.Issues)
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
