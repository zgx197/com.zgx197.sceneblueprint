#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Flow.Filter 的定义驱动 authoring 读取工具。
    /// 这是 Flow.Filter 在 editor / compiler / runtime fallback 间共享的正式 authoring 入口。
    /// </summary>
    public static class FlowFilterAuthoringUtility
    {
        private static readonly string[] OperatorOptions = { "==", "!=", ">", "<", ">=", "<=" };
        private static readonly string[] OperatorDisplayOptions =
        {
            "等于 ==",
            "不等于 !=",
            "大于 >",
            "小于 <",
            "大于等于 >=",
            "小于等于 <=",
        };

        private static readonly PropertyDefinition[] Definitions =
        {
            Prop.Enum(
                    ActionPortIds.FlowFilter.Op,
                    "比较运算符",
                    OperatorOptions,
                    defaultValue: "==",
                    tooltip: "把 compareValue 数据端口输入与常量值做比较。")
                .WithEnumDisplayOptions(OperatorDisplayOptions)
                .InSection("condition", "条件 Condition", sectionOrder: 0),
            CreateConstValueProperty(),
        };

        public static IReadOnlyList<PropertyDefinition> Properties
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static PropertyDefinition[] CreatePropertiesArray()
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static IReadOnlyList<string> Operators => OperatorOptions;

        public static IReadOnlyList<string> OperatorDisplayNames => OperatorDisplayOptions;

        public static PropertyDefinition? FindProperty(string propertyKey)
            => PropertyDefinitionValueUtility.FindClonedDefinition(Definitions, propertyKey);

        public static PropertyBagReader CreateBagReader(PropertyBag bag)
        {
            return new PropertyBagReader(bag, Definitions);
        }

        public static PropertyValueReader CreatePropertyReader(ActionEntry action, ActionDefinition? definition = null)
        {
            return new PropertyValueReader(action, definition ?? CreateDefinitionFallback());
        }

        public static FlowFilterAuthoringState Read(PropertyBag bag)
        {
            return Read(CreateBagReader(bag));
        }

        public static FlowFilterAuthoringState Read(ActionEntry action, ActionDefinition? definition = null)
        {
            return Read(CreatePropertyReader(action, definition));
        }

        public static FlowFilterAuthoringState Read(PropertyBagReader reader)
        {
            return new FlowFilterAuthoringState(
                NormalizeOperator(reader.GetString(ActionPortIds.FlowFilter.Op.Key, "==")),
                NormalizeConstValue(reader.GetString(ActionPortIds.FlowFilter.ConstValue.Key, "0")));
        }

        public static FlowFilterAuthoringState Read(PropertyValueReader reader)
        {
            return new FlowFilterAuthoringState(
                NormalizeOperator(reader.GetString(ActionPortIds.FlowFilter.Op.Key, "==")),
                NormalizeConstValue(reader.GetString(ActionPortIds.FlowFilter.ConstValue.Key, "0")));
        }

        public static string NormalizeOperator(string? value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            for (var index = 0; index < OperatorOptions.Length; index++)
            {
                if (string.Equals(OperatorOptions[index], trimmed, StringComparison.Ordinal))
                {
                    return OperatorOptions[index];
                }
            }

            return "==";
        }

        public static string NormalizeConstValue(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? "0" : trimmed;
        }

        public static bool RequiresNumericConstant(string? op)
        {
            return NormalizeOperator(op) switch
            {
                ">" => true,
                "<" => true,
                ">=" => true,
                "<=" => true,
                _ => false,
            };
        }

        public static bool TryParseNumericConstant(string? constValue, out double numericValue)
        {
            return double.TryParse(
                NormalizeConstValue(constValue),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out numericValue);
        }

        public static bool TryBuildAuthoringHint(string? rawConstValue, string? op, out string message, out bool isWarning)
        {
            message = string.Empty;
            isWarning = false;

            var normalizedOperator = NormalizeOperator(op);
            var normalizedConstValue = NormalizeConstValue(rawConstValue);
            var rawText = rawConstValue ?? string.Empty;
            if (RequiresNumericConstant(normalizedOperator) && !TryParseNumericConstant(normalizedConstValue, out _))
            {
                message = $"当前操作符 {normalizedOperator} 需要数值常量；否则运行时比较结果将始终不满足。";
                isWarning = true;
                return true;
            }

            if (!string.Equals(rawText, normalizedConstValue, StringComparison.Ordinal))
            {
                message = $"常量值会归一化为 {normalizedConstValue}";
                isWarning = false;
                return true;
            }

            return false;
        }

        public static bool TryBuildValidationIssue(
            string? rawConstValue,
            string? op,
            out ValidationIssue issue)
        {
            issue = null!;
            var normalizedOperator = NormalizeOperator(op);
            var normalizedConstValue = NormalizeConstValue(rawConstValue);
            if (RequiresNumericConstant(normalizedOperator)
                && !TryParseNumericConstant(normalizedConstValue, out _))
            {
                issue = ValidationIssue.Error(
                    $"当前操作符 {normalizedOperator} 需要数值常量，否则比较结果将始终不满足。");
                return true;
            }

            return false;
        }

        public static bool TryBuildAuthoringHint(
            string? rawConstValue,
            string? op,
            bool hasCompareValueInput,
            bool hasPassOutput,
            bool hasRejectOutput,
            out string message,
            out bool isWarning)
        {
            if (TryBuildAuthoringHint(rawConstValue, op, out message, out isWarning))
            {
                return true;
            }

            if (!hasPassOutput && !hasRejectOutput)
            {
                message = "满足 / 不满足两个分支当前都未连接，筛选结果不会向下游传播。";
                isWarning = true;
                return true;
            }

            if (!hasCompareValueInput)
            {
                message = "compareValue 输入当前未连接，运行时会按无条件通过处理。";
                isWarning = false;
                return true;
            }

            if (!hasPassOutput)
            {
                message = "当前只连接了不满足分支；条件满足时不会有下游接收。";
                isWarning = false;
                return true;
            }

            if (!hasRejectOutput)
            {
                message = "当前只连接了满足分支；条件不满足时不会有下游接收。";
                isWarning = false;
                return true;
            }

            message = string.Empty;
            isWarning = false;
            return false;
        }

        public static bool EvaluateCondition(object? compareValue, string? op, string? targetValue)
        {
            var normalizedOperator = NormalizeOperator(op);
            var normalizedTargetValue = NormalizeConstValue(targetValue);

            if (compareValue == null)
            {
                return string.Equals(normalizedOperator, "!=", StringComparison.Ordinal);
            }

            var compareText = compareValue.ToString() ?? string.Empty;
            if (double.TryParse(compareText, NumberStyles.Any, CultureInfo.InvariantCulture, out var compareNumber)
                && double.TryParse(normalizedTargetValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var targetNumber))
            {
                return normalizedOperator switch
                {
                    "==" => Math.Abs(compareNumber - targetNumber) < 0.0001d,
                    "!=" => Math.Abs(compareNumber - targetNumber) >= 0.0001d,
                    ">" => compareNumber > targetNumber,
                    "<" => compareNumber < targetNumber,
                    ">=" => compareNumber >= targetNumber,
                    "<=" => compareNumber <= targetNumber,
                    _ => false,
                };
            }

            return normalizedOperator switch
            {
                "==" => string.Equals(compareText, normalizedTargetValue, StringComparison.Ordinal),
                "!=" => !string.Equals(compareText, normalizedTargetValue, StringComparison.Ordinal),
                _ => false,
            };
        }

        public static string BuildConditionSummary(string? op, string? constValue)
        {
            return SemanticSummaryUtility.BuildFlowFilterConditionSummary(
                NormalizeOperator(op),
                NormalizeConstValue(constValue));
        }

        public static string BuildConnectionSummary(
            bool hasCompareValueInput,
            bool hasPassOutput,
            bool hasRejectOutput)
        {
            var compareInput = hasCompareValueInput ? "已连接" : "未连接";
            var passBranch = hasPassOutput ? "已连接" : "未连接";
            var rejectBranch = hasRejectOutput ? "已连接" : "未连接";
            return $"compareValue 输入: {compareInput}；满足分支: {passBranch}；不满足分支: {rejectBranch}";
        }

        private static PropertyDefinition CreateConstValueProperty()
        {
            var property = Prop.String(
                    ActionPortIds.FlowFilter.ConstValue,
                    "常量值",
                    defaultValue: "0",
                    tooltip: "当 compareValue 未连线时，运行时会按无条件通过处理。")
                .InSection("condition", "条件 Condition", sectionOrder: 0);
            property.AuthoringRule = new FlowFilterConstValuePropertyRule();
            return property;
        }

        private static ActionDefinition CreateDefinitionFallback()
        {
            return new ActionDefinition
            {
                Properties = CreatePropertiesArray(),
            };
        }
    }

    public readonly struct FlowFilterAuthoringState
    {
        public FlowFilterAuthoringState(string @operator, string constValue)
        {
            Operator = @operator ?? "==";
            ConstValue = constValue ?? "0";
        }

        public string Operator { get; }

        public string ConstValue { get; }
    }
}
