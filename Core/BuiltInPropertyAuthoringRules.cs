#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    public sealed class SignalWatchConditionParametersPropertyRule : IPropertyAuthoringRule, IPropertyAuthoringRuleSnapshotProvider
    {
        public IPropertyAuthoringRule CreateSnapshotRule()
            => new SignalWatchConditionParametersPropertyRule();

        public bool TryNormalize(PropertyAuthoringContext context, object? currentValue, out object? normalizedValue)
        {
            var authoring = SignalWatchConditionAuthoringUtility.Read(context.CreateBagReader());
            normalizedValue = null;
            if (authoring.IsHPThreshold)
            {
                return false;
            }

            var validCount = ConditionParameterCodec.CountValidEntries(authoring.RawParameters);
            if (validCount <= 0)
            {
                return false;
            }

            var normalized = SignalWatchConditionAuthoringUtility.NormalizeCustomParameters(authoring.RawParameters);
            if (string.Equals(authoring.RawParameters ?? string.Empty, normalized, System.StringComparison.Ordinal))
            {
                return false;
            }

            normalizedValue = normalized;
            return true;
        }

        public IEnumerable<ValidationIssue> Validate(PropertyAuthoringContext context)
        {
            var authoring = SignalWatchConditionAuthoringUtility.Read(context.CreateBagReader());
            if (authoring.IsHPThreshold)
            {
                yield break;
            }

            var validCount = ConditionParameterCodec.CountValidEntries(authoring.RawParameters);
            var invalidCount = ConditionParameterCodec.CountInvalidEntries(authoring.RawParameters);
            if (invalidCount > 0 && validCount == 0)
            {
                yield return ValidationIssue.Warning("当前参数里没有有效的 key=value 项；无效参数不会进入编译结果。");
                yield break;
            }

            if (invalidCount > 0)
            {
                yield return ValidationIssue.Warning($"当前参数中包含 {invalidCount} 个无效片段；编译时会自动忽略。");
            }
        }
    }

    public sealed class FlowFilterConstValuePropertyRule : IPropertyAuthoringRule, IPropertyAuthoringRuleSnapshotProvider
    {
        public IPropertyAuthoringRule CreateSnapshotRule()
            => new FlowFilterConstValuePropertyRule();

        public bool TryNormalize(PropertyAuthoringContext context, object? currentValue, out object? normalizedValue)
        {
            var raw = currentValue?.ToString() ?? string.Empty;
            var normalized = FlowFilterAuthoringUtility.NormalizeConstValue(raw);
            normalizedValue = normalized;
            return !string.Equals(raw, normalized, System.StringComparison.Ordinal);
        }

        public IEnumerable<ValidationIssue> Validate(PropertyAuthoringContext context)
        {
            var authoring = FlowFilterAuthoringUtility.Read(context.CreateBagReader());
            if (FlowFilterAuthoringUtility.TryBuildValidationIssue(
                    authoring.ConstValue,
                    authoring.Operator,
                    out var issue))
            {
                yield return issue;
            }
        }
    }

    public sealed class BlackboardSetValuePropertyRule : IPropertyAuthoringRule, IPropertyAuthoringRuleSnapshotProvider
    {
        public IPropertyAuthoringRule CreateSnapshotRule()
            => new BlackboardSetValuePropertyRule();

        public bool TryNormalize(PropertyAuthoringContext context, object? currentValue, out object? normalizedValue)
        {
            var authoring = BlackboardAuthoringUtility.ReadSet(context.CreateBagReader());
            var variable = BlackboardAuthoringUtility.ResolveVariable(
                ValidationContextVariableUtility.ToVariableArray(context.Variables),
                authoring.VariableIndex);
            normalizedValue = null;
            if (variable == null)
            {
                return false;
            }

            var normalizedText = BlackboardAuthoringUtility.NormalizeValueText(
                variable.Type,
                authoring.ValueText,
                out var usedFallback);
            if (usedFallback)
            {
                return false;
            }

            normalizedValue = normalizedText;
            return !string.Equals(authoring.ValueText ?? string.Empty, normalizedText, System.StringComparison.Ordinal);
        }

        public IEnumerable<ValidationIssue> Validate(PropertyAuthoringContext context)
        {
            var authoring = BlackboardAuthoringUtility.ReadSet(context.CreateBagReader());
            if (authoring.VariableIndex < 0)
            {
                yield break;
            }

            var variable = BlackboardAuthoringUtility.ResolveVariable(
                ValidationContextVariableUtility.ToVariableArray(context.Variables),
                authoring.VariableIndex);
            if (variable == null)
            {
                yield break;
            }

            if (BlackboardAuthoringUtility.TryBuildSetValidationIssue(
                    variable,
                    authoring.VariableIndex,
                    authoring.ValueText,
                    out var issue)
                && issue.IsError)
            {
                yield return issue;
            }
        }
    }
}
