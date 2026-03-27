#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Signal.CompositeCondition 的定义驱动 authoring 读取工具。
    /// </summary>
    public static class SignalCompositeConditionAuthoringUtility
    {
        public const string AndMode = "AND";
        public const string OrMode = "OR";

        private static readonly string[] ModeDisplayOptions =
        {
            "全部满足 AND",
            "任一满足 OR",
        };

        private static readonly PropertyDefinition[] Definitions =
        {
            Prop.Enum(
                    ActionPortIds.SignalCompositeCondition.Mode,
                    "组合模式",
                    new[] { AndMode, OrMode },
                    defaultValue: AndMode,
                    tooltip: "AND = 所有子条件均触发才激活；OR = 任一子条件触发即激活。")
                .WithEnumDisplayOptions(ModeDisplayOptions)
                .InSection("condition", "条件 Condition", sectionOrder: 0),
            Prop.Float(
                    ActionPortIds.SignalCompositeCondition.Timeout,
                    "超时(s)",
                    defaultValue: 0f,
                    min: 0f,
                    tooltip: "0 = 无超时，永久等待。")
                .InSection("execution", "执行 Execution", sectionOrder: 20, isAdvanced: true),
        };

        public static IReadOnlyList<PropertyDefinition> Properties
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static PropertyDefinition[] CreatePropertiesArray()
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static IReadOnlyList<string> ModeValues => Definitions[0].EnumOptions ?? Array.Empty<string>();

        public static IReadOnlyList<string> ModeDisplayNames => ModeDisplayOptions;

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

        public static SignalCompositeConditionAuthoringState Read(PropertyBag bag)
        {
            return Read(CreateBagReader(bag));
        }

        public static SignalCompositeConditionAuthoringState Read(ActionEntry action, ActionDefinition? definition = null)
        {
            return Read(CreatePropertyReader(action, definition));
        }

        public static SignalCompositeConditionAuthoringState Read(PropertyBagReader reader)
        {
            return new SignalCompositeConditionAuthoringState(
                SemanticSummaryUtility.NormalizeCompositeConditionMode(
                    reader.GetString(ActionPortIds.SignalCompositeCondition.Mode.Key, AndMode)),
                Math.Max(0f, reader.GetFloat(ActionPortIds.SignalCompositeCondition.Timeout.Key)));
        }

        public static SignalCompositeConditionAuthoringState Read(PropertyValueReader reader)
        {
            return new SignalCompositeConditionAuthoringState(
                SemanticSummaryUtility.NormalizeCompositeConditionMode(
                    reader.GetString(ActionPortIds.SignalCompositeCondition.Mode.Key, AndMode)),
                Math.Max(0f, reader.GetFloat(ActionPortIds.SignalCompositeCondition.Timeout.Key)));
        }

        public static string ToReadableModeLabel(string? value)
        {
            return SemanticSummaryUtility.DescribeCompositeConditionMode(value);
        }

        public static string BuildAuthoringHeadline(string? mode)
        {
            return $"触发规则: {ToReadableModeLabel(mode)}";
        }

        public static bool TryNormalizePortId(string? portId, out string normalizedPortId)
        {
            normalizedPortId = portId?.Trim() ?? string.Empty;
            switch (normalizedPortId)
            {
                case ActionPortIds.SignalCompositeCondition.Cond0:
                case ActionPortIds.SignalCompositeCondition.Cond1:
                case ActionPortIds.SignalCompositeCondition.Cond2:
                case ActionPortIds.SignalCompositeCondition.Cond3:
                    return true;
                default:
                    normalizedPortId = string.Empty;
                    return false;
            }
        }

        public static int ComparePortId(string? left, string? right)
        {
            return GetPortOrder(left).CompareTo(GetPortOrder(right));
        }

        public static int BuildConnectedMask(IReadOnlyList<string>? connectedPortIds)
        {
            if (connectedPortIds == null || connectedPortIds.Count == 0)
            {
                return 0;
            }

            var mask = 0;
            for (var index = 0; index < connectedPortIds.Count; index++)
            {
                if (!TryNormalizePortId(connectedPortIds[index], out var normalizedPortId))
                {
                    continue;
                }

                mask |= normalizedPortId switch
                {
                    ActionPortIds.SignalCompositeCondition.Cond0 => 1 << 0,
                    ActionPortIds.SignalCompositeCondition.Cond1 => 1 << 1,
                    ActionPortIds.SignalCompositeCondition.Cond2 => 1 << 2,
                    ActionPortIds.SignalCompositeCondition.Cond3 => 1 << 3,
                    _ => 0,
                };
            }

            return mask;
        }

        public static string[] NormalizeAndSortConnectedPortIds(IEnumerable<string>? portIds)
        {
            if (portIds == null)
            {
                return Array.Empty<string>();
            }

            var normalizedPortIds = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var portId in portIds)
            {
                if (!TryNormalizePortId(portId, out var normalizedPortId)
                    || !seen.Add(normalizedPortId))
                {
                    continue;
                }

                normalizedPortIds.Add(normalizedPortId);
            }

            normalizedPortIds.Sort(ComparePortId);
            return normalizedPortIds.ToArray();
        }

        public static string BuildConnectedPortSummary(IReadOnlyList<string>? connectedPortIds)
        {
            if (connectedPortIds == null || connectedPortIds.Count == 0)
            {
                return "无已连接条件";
            }

            var labels = new string[connectedPortIds.Count];
            for (var index = 0; index < connectedPortIds.Count; index++)
            {
                labels[index] = SemanticSummaryUtility.DescribeCompositeConditionPort(connectedPortIds[index]);
            }

            return string.Join(" + ", labels);
        }

        public static string BuildConditionSummary(string? mode, IReadOnlyList<string>? connectedPortIds)
        {
            if (connectedPortIds == null || connectedPortIds.Count == 0)
            {
                return SemanticSummaryUtility.BuildCompositeConditionSummary(
                    SemanticSummaryUtility.NormalizeCompositeConditionMode(mode),
                    Array.Empty<string>());
            }

            var labels = new string[connectedPortIds.Count];
            for (var index = 0; index < connectedPortIds.Count; index++)
            {
                labels[index] = SemanticSummaryUtility.DescribeCompositeConditionPort(connectedPortIds[index]);
            }

            return SemanticSummaryUtility.BuildCompositeConditionSummary(
                SemanticSummaryUtility.NormalizeCompositeConditionMode(mode),
                labels);
        }

        public static bool TryBuildAuthoringHint(
            IReadOnlyList<string>? connectedPortIds,
            out string message,
            out bool isWarning)
        {
            message = string.Empty;
            isWarning = false;

            var normalizedPortIds = NormalizeAndSortConnectedPortIds(connectedPortIds);
            if (normalizedPortIds.Length == 0)
            {
                message = "当前没有已连接的条件输入，组合条件不会产生有效结果。";
                isWarning = true;
                return true;
            }

            if (normalizedPortIds.Length == 1)
            {
                message = "当前只连接了 1 个条件输入，通常不需要使用组合条件节点。";
                isWarning = true;
                return true;
            }

            return false;
        }

        public static bool TryBuildValidationIssue(
            IReadOnlyList<string>? connectedPortIds,
            out ValidationIssue issue)
        {
            issue = null!;

            var normalizedPortIds = NormalizeAndSortConnectedPortIds(connectedPortIds);
            if (normalizedPortIds.Length == 0)
            {
                issue = ValidationIssue.Error("当前没有已连接的条件输入，组合条件不会产生有效结果。");
                return true;
            }

            if (normalizedPortIds.Length == 1)
            {
                issue = ValidationIssue.Warning("当前只连接了 1 个条件输入，通常不需要使用组合条件节点。");
                return true;
            }

            return false;
        }

        private static int GetPortOrder(string? portId)
        {
            return portId switch
            {
                var value when string.Equals(value, ActionPortIds.SignalCompositeCondition.Cond0, StringComparison.Ordinal) => 0,
                var value when string.Equals(value, ActionPortIds.SignalCompositeCondition.Cond1, StringComparison.Ordinal) => 1,
                var value when string.Equals(value, ActionPortIds.SignalCompositeCondition.Cond2, StringComparison.Ordinal) => 2,
                var value when string.Equals(value, ActionPortIds.SignalCompositeCondition.Cond3, StringComparison.Ordinal) => 3,
                _ => int.MaxValue,
            };
        }

        private static ActionDefinition CreateDefinitionFallback()
        {
            return new ActionDefinition
            {
                Properties = CreatePropertiesArray(),
            };
        }
    }

    public readonly struct SignalCompositeConditionAuthoringState
    {
        public SignalCompositeConditionAuthoringState(string mode, float timeoutSeconds)
        {
            Mode = mode ?? SignalCompositeConditionAuthoringUtility.AndMode;
            TimeoutSeconds = timeoutSeconds;
        }

        public string Mode { get; }

        public float TimeoutSeconds { get; }
    }
}
