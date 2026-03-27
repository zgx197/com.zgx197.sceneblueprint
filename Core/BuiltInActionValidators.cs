#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 这批 validator 负责把 built-in authoring utility 的校验规则正式挂回 ActionDefinition，
    /// 让定义层成为 Inspector / 分析层共享的默认 validation 入口。
    /// </summary>
    public sealed class SignalWatchConditionActionValidator : IActionValidator
    {
        public IEnumerable<ValidationIssue> Validate(NodeValidationContext ctx)
        {
            var authoring = SignalWatchConditionAuthoringUtility.Read(ctx.CreateBagReader());
            foreach (var issue in SignalWatchConditionAuthoringUtility.BuildValidationIssues(authoring))
            {
                yield return issue;
            }
        }
    }

    public sealed class SignalCompositeConditionActionValidator : IActionValidator
    {
        public IEnumerable<ValidationIssue> Validate(NodeValidationContext ctx)
        {
            var connectedPortIds = SignalCompositeConditionAuthoringUtility.NormalizeAndSortConnectedPortIds(
                ctx.GetConnectedPortIdsByRole(PortGraphRole.ConditionInput));
            if (SignalCompositeConditionAuthoringUtility.TryBuildValidationIssue(
                    connectedPortIds,
                    out var issue))
            {
                yield return issue;
            }
        }
    }

    public sealed class FlowFilterActionValidator : IActionValidator
    {
        public IEnumerable<ValidationIssue> Validate(NodeValidationContext ctx)
        {
            yield break;
        }
    }

    public sealed class FlowBranchActionValidator : IActionValidator
    {
        public IEnumerable<ValidationIssue> Validate(NodeValidationContext ctx)
        {
            var hasTrueOutput = ctx.IsAnyPortConnected(PortGraphRole.TrueBranch);
            var hasFalseOutput = ctx.IsAnyPortConnected(PortGraphRole.FalseBranch);
            if (FlowBranchAuthoringUtility.TryBuildValidationIssue(
                    hasTrueOutput,
                    hasFalseOutput,
                    out var issue))
            {
                yield return issue;
            }
        }
    }

    public sealed class BlackboardGetActionValidator : IActionValidator
    {
        public IEnumerable<ValidationIssue> Validate(NodeValidationContext ctx)
        {
            var authoring = BlackboardAuthoringUtility.ReadGet(ctx.CreateBagReader());
            if (authoring.VariableIndex < 0)
            {
                yield return ValidationIssue.Error("请先选择要读取的变量。");
                yield break;
            }

            if (ctx.Variables.Count > 0
                && BlackboardAuthoringUtility.ResolveVariable(
                    ValidationContextVariableUtility.ToVariableArray(ctx.Variables),
                    authoring.VariableIndex) == null)
            {
                yield return ValidationIssue.Error($"当前变量索引 {authoring.VariableIndex} 未在蓝图变量表中找到。");
            }
        }
    }

    public sealed class BlackboardSetActionValidator : IActionValidator
    {
        public IEnumerable<ValidationIssue> Validate(NodeValidationContext ctx)
        {
            var authoring = BlackboardAuthoringUtility.ReadSet(ctx.CreateBagReader());
            var variables = ValidationContextVariableUtility.ToVariableArray(ctx.Variables);
            var variable = BlackboardAuthoringUtility.ResolveVariable(variables, authoring.VariableIndex);

            if (authoring.VariableIndex < 0)
            {
                yield return ValidationIssue.Error("请先选择目标变量，再配置写入值。");
                yield break;
            }

            if (variable == null)
            {
                yield return ValidationIssue.Error($"当前变量索引 {authoring.VariableIndex} 未在蓝图变量表中找到。");
            }
        }
    }

    internal static class ValidationContextVariableUtility
    {
        public static VariableDeclaration[] ToVariableArray(IReadOnlyList<VariableDeclaration> variables)
        {
            if (variables == null || variables.Count == 0)
            {
                return System.Array.Empty<VariableDeclaration>();
            }

            var result = new VariableDeclaration[variables.Count];
            for (var index = 0; index < variables.Count; index++)
            {
                result[index] = variables[index];
            }

            return result;
        }
    }
}
