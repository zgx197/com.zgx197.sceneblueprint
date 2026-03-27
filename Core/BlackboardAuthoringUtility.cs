#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Blackboard.Get / Blackboard.Set 的定义驱动 authoring 读取工具。
    /// 这是 Blackboard 节点在 editor / compiler / runtime fallback 间共享的正式 authoring 入口。
    /// </summary>
    public static class BlackboardAuthoringUtility
    {
        private static readonly PropertyDefinition[] BlackboardGetDefinitions =
        {
            Prop.VariableSelector(ActionPortIds.BlackboardGet.VariableIndex, "目标变量", defaultValue: -1)
                .WithTooltip("选择要读取的声明变量。")
                .InSection("variable", "变量 Variable", sectionOrder: 0),
        };

        private static readonly PropertyDefinition[] BlackboardSetDefinitions =
        {
            Prop.VariableSelector(ActionPortIds.BlackboardSet.VariableIndex, "目标变量", defaultValue: -1)
                .WithTooltip("选择要写入的声明变量。")
                .InSection("variable", "变量 Variable", sectionOrder: 0),
            CreateSetValueProperty(),
        };

        public static IReadOnlyList<PropertyDefinition> BlackboardGetProperties
            => PropertyDefinitionValueUtility.CloneDefinitions(BlackboardGetDefinitions);

        public static IReadOnlyList<PropertyDefinition> BlackboardSetProperties
            => PropertyDefinitionValueUtility.CloneDefinitions(BlackboardSetDefinitions);

        public static PropertyDefinition[] CreateBlackboardGetPropertiesArray()
            => PropertyDefinitionValueUtility.CloneDefinitions(BlackboardGetDefinitions);

        public static PropertyDefinition[] CreateBlackboardSetPropertiesArray()
            => PropertyDefinitionValueUtility.CloneDefinitions(BlackboardSetDefinitions);

        public static PropertyDefinition? FindGetProperty(string propertyKey)
            => FindProperty(BlackboardGetDefinitions, propertyKey);

        public static PropertyDefinition? FindSetProperty(string propertyKey)
            => FindProperty(BlackboardSetDefinitions, propertyKey);

        public static PropertyBagReader CreateGetBagReader(PropertyBag bag)
        {
            return new PropertyBagReader(bag, BlackboardGetDefinitions);
        }

        public static PropertyBagReader CreateSetBagReader(PropertyBag bag)
        {
            return new PropertyBagReader(bag, BlackboardSetDefinitions);
        }

        public static PropertyValueReader CreateGetPropertyReader(ActionEntry action, ActionDefinition? definition = null)
        {
            return new PropertyValueReader(action, definition ?? CreateDefinitionFallback(BlackboardGetDefinitions));
        }

        public static PropertyValueReader CreateSetPropertyReader(ActionEntry action, ActionDefinition? definition = null)
        {
            return new PropertyValueReader(action, definition ?? CreateDefinitionFallback(BlackboardSetDefinitions));
        }

        public static BlackboardGetAuthoringState ReadGet(ActionEntry action, ActionDefinition? definition = null)
        {
            return ReadGet(CreateGetPropertyReader(action, definition));
        }

        public static BlackboardGetAuthoringState ReadGet(PropertyBag bag)
        {
            return ReadGet(CreateGetBagReader(bag));
        }

        public static BlackboardGetAuthoringState ReadGet(PropertyBagReader reader)
        {
            return new BlackboardGetAuthoringState(
                Math.Max(-1, reader.GetInt(ActionPortIds.BlackboardGet.VariableIndex.Key, -1)));
        }

        public static BlackboardGetAuthoringState ReadGet(PropertyValueReader reader)
        {
            return new BlackboardGetAuthoringState(
                Math.Max(-1, reader.GetInt(ActionPortIds.BlackboardGet.VariableIndex.Key, -1)));
        }

        public static BlackboardSetAuthoringState ReadSet(ActionEntry action, ActionDefinition? definition = null)
        {
            return ReadSet(CreateSetPropertyReader(action, definition));
        }

        public static BlackboardSetAuthoringState ReadSet(PropertyBag bag)
        {
            return ReadSet(CreateSetBagReader(bag));
        }

        public static BlackboardSetAuthoringState ReadSet(PropertyBagReader reader)
        {
            return new BlackboardSetAuthoringState(
                Math.Max(-1, reader.GetInt(ActionPortIds.BlackboardSet.VariableIndex.Key, -1)),
                reader.GetString(ActionPortIds.BlackboardSet.Value.Key, string.Empty));
        }

        public static BlackboardSetAuthoringState ReadSet(PropertyValueReader reader)
        {
            return new BlackboardSetAuthoringState(
                Math.Max(-1, reader.GetInt(ActionPortIds.BlackboardSet.VariableIndex.Key, -1)),
                reader.GetString(ActionPortIds.BlackboardSet.Value.Key, string.Empty));
        }

        public static object ParseValue(string? type, string? value)
        {
            TryParseValue(type, value, out var parsedValue);
            return parsedValue;
        }

        public static bool TryParseValue(string? type, string? value, out object parsedValue)
        {
            var raw = value ?? string.Empty;
            switch (NormalizeVariableType(type))
            {
                case "int":
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        parsedValue = intValue;
                        return true;
                    }

                    parsedValue = 0;
                    return false;

                case "float":
                    if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        parsedValue = floatValue;
                        return true;
                    }

                    parsedValue = 0f;
                    return false;

                case "bool":
                    if (bool.TryParse(raw, out var boolValue))
                    {
                        parsedValue = boolValue;
                        return true;
                    }

                    parsedValue = false;
                    return false;

                case "string":
                    parsedValue = raw;
                    return true;

                default:
                    parsedValue = raw;
                    return true;
            }
        }

        public static string NormalizeValueText(string? type, string? value, out bool usedFallback)
        {
            usedFallback = !TryParseValue(type, value, out var parsedValue);
            return FormatValueText(parsedValue);
        }

        public static string NormalizeValueText(string? type, string? value)
        {
            return NormalizeValueText(type, value, out _);
        }

        public static string FormatValueText(object? value)
        {
            return value switch
            {
                null => string.Empty,
                float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
                double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        }

        public static string NormalizeVariableType(string? type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "int" => "int",
                "float" => "float",
                "bool" => "bool",
                "string" => "string",
                _ => "string",
            };
        }

        public static string BuildVariableSummary(VariableDeclaration? variable, int variableIndex)
        {
            if (variable == null)
            {
                return variableIndex < 0
                    ? "未配置变量"
                    : $"变量[{variableIndex}] 未找到";
            }

            var scope = string.IsNullOrWhiteSpace(variable.Scope) ? "Local" : variable.Scope;
            var name = string.IsNullOrWhiteSpace(variable.Name) ? $"var[{variable.Index}]" : variable.Name;
            var type = string.IsNullOrWhiteSpace(variable.Type) ? "Unknown" : variable.Type;
            return $"{scope}.{name}[{variable.Index}] ({type})";
        }

        public static VariableDeclaration? ResolveVariable(VariableDeclaration[]? variables, int variableIndex)
        {
            if (variables == null || variableIndex < 0)
            {
                return null;
            }

            for (var index = 0; index < variables.Length; index++)
            {
                var variable = variables[index];
                if (variable != null && variable.Index == variableIndex)
                {
                    return variable;
                }
            }

            return null;
        }

        public static string BuildSetAuthoringSummary(VariableDeclaration? variable, int variableIndex, string normalizedValueText)
        {
            return SemanticSummaryUtility.BuildBlackboardAccessSummary(
                "set",
                BuildVariableSummary(variable, variableIndex),
                normalizedValueText);
        }

        public static string BuildSetHeadline(VariableDeclaration? variable, int variableIndex)
        {
            return $"变量摘要: {BuildVariableSummary(variable, variableIndex)}";
        }

        public static string? BuildSetDetail(
            VariableDeclaration? variable,
            int variableIndex,
            string normalizedValueText)
        {
            return variable == null
                ? null
                : $"写入摘要: {BuildSetAuthoringSummary(variable, variableIndex, normalizedValueText)}";
        }

        public static bool TryBuildSetAuthoringHint(
            VariableDeclaration? variable,
            int variableIndex,
            string rawValueText,
            string normalizedValueText,
            bool usedFallback,
            out string message,
            out bool isWarning)
        {
            message = string.Empty;
            isWarning = false;

            if (variableIndex < 0)
            {
                message = "请先选择目标变量，再编辑写入值。";
                isWarning = true;
                return true;
            }

            if (variable == null)
            {
                message = $"当前变量索引 {variableIndex} 未在蓝图变量表中找到。";
                isWarning = true;
                return true;
            }

            if (usedFallback)
            {
                message = $"当前输入无法按变量类型 {variable.Type} 解析，将回退为 {normalizedValueText}。";
                isWarning = true;
                return true;
            }

            if (!string.Equals(rawValueText ?? string.Empty, normalizedValueText ?? string.Empty, StringComparison.Ordinal))
            {
                message = $"值会归一化为 {normalizedValueText}";
                isWarning = false;
                return true;
            }

            message = $"写入摘要: {BuildSetAuthoringSummary(variable, variableIndex, normalizedValueText)}";
            isWarning = false;
            return true;
        }

        public static bool TryBuildGetValidationIssue(
            VariableDeclaration? variable,
            int variableIndex,
            out ValidationIssue issue)
        {
            issue = null!;
            if (variableIndex < 0)
            {
                issue = ValidationIssue.Error("请先选择要读取的变量。");
                return true;
            }

            if (variable == null)
            {
                issue = ValidationIssue.Error($"当前变量索引 {variableIndex} 未在蓝图变量表中找到。");
                return true;
            }

            return false;
        }

        public static bool TryBuildSetValidationIssue(
            VariableDeclaration? variable,
            int variableIndex,
            string rawValueText,
            out ValidationIssue issue)
        {
            issue = null!;
            if (variableIndex < 0)
            {
                issue = ValidationIssue.Error("请先选择目标变量，再配置写入值。");
                return true;
            }

            if (variable == null)
            {
                issue = ValidationIssue.Error($"当前变量索引 {variableIndex} 未在蓝图变量表中找到。");
                return true;
            }

            NormalizeValueText(variable.Type, rawValueText, out var usedFallback);
            if (usedFallback && !string.IsNullOrWhiteSpace(rawValueText))
            {
                issue = ValidationIssue.Error(
                    $"当前输入无法按变量类型 {variable.Type} 解析，运行时会回退到该类型的默认可用值。");
                return true;
            }

            return false;
        }

        private static PropertyDefinition CreateSetValueProperty()
        {
            var property = Prop.String(
                ActionPortIds.BlackboardSet.Value,
                "写入值",
                defaultValue: string.Empty,
                tooltip: "控件会根据变量类型切换；最终仍以字符串形式写回 authoring 数据。",
                typeSourceKey: ActionPortIds.BlackboardSet.VariableIndex.Key)
                .InSection("write", "写入 Write", sectionOrder: 10);
            property.AuthoringRule = new BlackboardSetValuePropertyRule();
            return property;
        }

        private static PropertyDefinition? FindProperty(PropertyDefinition[] definitions, string propertyKey)
            => PropertyDefinitionValueUtility.FindClonedDefinition(definitions, propertyKey);

        private static ActionDefinition CreateDefinitionFallback(PropertyDefinition[] definitions)
        {
            return new ActionDefinition
            {
                Properties = PropertyDefinitionValueUtility.CloneDefinitions(definitions),
            };
        }
    }

    public readonly struct BlackboardGetAuthoringState
    {
        public BlackboardGetAuthoringState(int variableIndex)
        {
            VariableIndex = variableIndex;
        }

        public int VariableIndex { get; }
    }

    public readonly struct BlackboardSetAuthoringState
    {
        public BlackboardSetAuthoringState(int variableIndex, string valueText)
        {
            VariableIndex = variableIndex;
            ValueText = valueText ?? string.Empty;
        }

        public int VariableIndex { get; }

        public string ValueText { get; }
    }
}
