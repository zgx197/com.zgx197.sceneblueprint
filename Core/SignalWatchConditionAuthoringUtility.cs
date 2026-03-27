#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Signal.WatchCondition 的定义驱动 authoring 读取工具。
    /// 让 editor / compiler / runtime fallback 共用同一套默认值与参数规范化规则。
    /// </summary>
    public static class SignalWatchConditionAuthoringUtility
    {
        public const string HPThresholdConditionType = ConditionWatchSemanticUtility.HPThresholdConditionType;
        public const string DropToMode = ConditionWatchSemanticUtility.DropToMode;
        public const string DropByMode = ConditionWatchSemanticUtility.DropByMode;
        public const float DefaultHPThreshold = ConditionWatchSemanticUtility.DefaultHPThreshold;

        private static readonly string[] ConditionTypeOptionValues =
        {
            HPThresholdConditionType,
            string.Empty,
        };

        private static readonly string[] ConditionTypeOptionDisplayNames =
        {
            "血量阈值 HPThreshold",
            "自定义 Custom",
        };

        private static readonly string[] HPModeOptionValues =
        {
            DropToMode,
            DropByMode,
        };

        private static readonly string[] HPModeOptionDisplayNames =
        {
            "降至阈值 DropTo",
            "每下降阈值 DropBy",
        };

        private static readonly PropertyDefinition[] Definitions =
        {
            Prop.String(
                    ActionPortIds.SignalWatchCondition.ConditionType,
                    "条件类型",
                    defaultValue: string.Empty,
                    tooltip: "对应 IConditionEvaluator.TypeId，如 HPThreshold。")
                .InSection("condition", "条件 Condition", sectionOrder: 0),
            Prop.EntityRefSelector(
                    ActionPortIds.SignalWatchCondition.TargetRef,
                    "目标引用",
                    defaultValue: string.Empty,
                    tooltip: "结构化实体引用，支持 ByAlias / ByRole / BySceneRef / ByTag / ByTags / All / Any。")
                .InSection("condition", "条件 Condition", sectionOrder: 0),
            CreateParametersProperty(),
            Prop.Float(
                    ActionPortIds.SignalWatchCondition.Timeout,
                    "超时(s)",
                    defaultValue: 0f,
                    min: 0f,
                    tooltip: "0 = 无超时，永久等待。")
                .InSection("execution", "执行 Execution", sectionOrder: 20, isAdvanced: true),
            Prop.Bool(ActionPortIds.SignalWatchCondition.Repeat, "重复触发", defaultValue: false)
                .WithTooltip("触发后保持监听，并允许重复发射条件触发事件。")
                .InSection("execution", "执行 Execution", sectionOrder: 20, isAdvanced: true),
        };

        public static IReadOnlyList<PropertyDefinition> Properties
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static PropertyDefinition[] CreatePropertiesArray()
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static IReadOnlyList<string> ConditionTypeValues => ConditionTypeOptionValues;

        public static IReadOnlyList<string> ConditionTypeDisplayNames => ConditionTypeOptionDisplayNames;

        public static string[] ConditionTypeDisplayOptions => ConditionTypeOptionDisplayNames;

        public static IReadOnlyList<string> HPModeValues => HPModeOptionValues;

        public static IReadOnlyList<string> HPModeDisplayNames => HPModeOptionDisplayNames;

        public static string[] HPModeDisplayOptions => HPModeOptionDisplayNames;

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

        public static SignalWatchConditionAuthoringState Read(PropertyBag bag)
        {
            return Read(CreateBagReader(bag));
        }

        public static SignalWatchConditionAuthoringState Read(ActionEntry action, ActionDefinition? definition = null)
        {
            return Read(CreatePropertyReader(action, definition));
        }

        public static SignalWatchConditionAuthoringState Read(PropertyBagReader reader)
        {
            return BuildState(
                reader.GetString(ActionPortIds.SignalWatchCondition.ConditionType.Key),
                reader.GetString(ActionPortIds.SignalWatchCondition.TargetRef.Key),
                reader.GetString(ActionPortIds.SignalWatchCondition.Parameters.Key),
                reader.GetFloat(ActionPortIds.SignalWatchCondition.Timeout.Key),
                reader.GetBool(ActionPortIds.SignalWatchCondition.Repeat.Key));
        }

        public static SignalWatchConditionAuthoringState Read(PropertyValueReader reader)
        {
            return BuildState(
                reader.GetString(ActionPortIds.SignalWatchCondition.ConditionType.Key),
                reader.GetString(ActionPortIds.SignalWatchCondition.TargetRef.Key),
                reader.GetString(ActionPortIds.SignalWatchCondition.Parameters.Key),
                reader.GetFloat(ActionPortIds.SignalWatchCondition.Timeout.Key),
                reader.GetBool(ActionPortIds.SignalWatchCondition.Repeat.Key));
        }

        public static void ApplyHPThreshold(PropertyBag bag, float threshold, string? mode)
        {
            bag.Set(ActionPortIds.SignalWatchCondition.ConditionType.Key, HPThresholdConditionType);
            bag.Set(
                ActionPortIds.SignalWatchCondition.Parameters.Key,
                SerializeHPThreshold(threshold, mode));
        }

        public static ConditionParameter[] BuildParameters(SignalWatchConditionAuthoringState state)
        {
            return ConditionWatchSemanticUtility.NormalizeParameters(
                state.ConditionType,
                state.NormalizedParameters);
        }

        public static ConditionWatchDescriptor BuildDescriptor(
            SignalWatchConditionAuthoringState state,
            ConditionParameter[]? parameters = null)
        {
            var resolvedParameters = parameters ?? BuildParameters(state);
            return new ConditionWatchDescriptor
            {
                ConditionType = state.ConditionType,
                Target = EntityRefCodec.Parse(state.TargetRefSerialized),
                Parameters = resolvedParameters,
                Timeout = Math.Max(0f, state.TimeoutSeconds),
            };
        }

        public static string BuildConditionSummary(
            SignalWatchConditionAuthoringState state,
            string? targetSummary)
        {
            return SemanticSummaryUtility.BuildWatchConditionSummary(
                state.ConditionType,
                targetSummary,
                state.ParameterSummary);
        }

        public static string BuildParameterDetailSummary(SignalWatchConditionAuthoringState state)
        {
            return string.IsNullOrWhiteSpace(state.ParameterSummary)
                ? "参数摘要: 无参数"
                : $"参数摘要: {state.ParameterSummary}";
        }

        public static string NormalizeCustomParameters(string? rawParameters)
        {
            return ConditionParameterCodec.Normalize(rawParameters);
        }

        public static string BuildHPThresholdParameterSummary(float threshold, string? mode)
        {
            var normalizedThreshold = ConditionWatchSemanticUtility.NormalizeThreshold(threshold);
            return ConditionWatchSemanticUtility.NormalizeMode(mode) == DropByMode
                ? $"当前会在目标生命值每下降 {normalizedThreshold * 100f:0.#}% 时触发。"
                : $"当前会在目标生命值降到 {normalizedThreshold * 100f:0.#}% 及以下时触发。";
        }

        public static bool TryBuildParameterHint(
            SignalWatchConditionAuthoringState state,
            out string message,
            out bool isWarning)
        {
            if (state.IsHPThreshold)
            {
                message = BuildHPThresholdParameterSummary(state.Threshold, state.Mode);
                isWarning = false;
                return true;
            }

            return TryBuildCustomParameterHint(state, out message, out isWarning);
        }

        public static IEnumerable<ValidationIssue> BuildValidationIssues(
            SignalWatchConditionAuthoringState state)
        {
            if (string.IsNullOrWhiteSpace(state.ConditionType))
            {
                yield return ValidationIssue.Error("请先配置条件类型。");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(state.TargetRefSerialized))
            {
                yield return ValidationIssue.Error("请先配置目标引用。");
            }

            if (!state.IsHPThreshold)
            {
                yield break;
            }

            var recognized = ConditionWatchSemanticUtility.TryParseHPThreshold(
                state.RawParameters,
                out _,
                out _);
            if (!string.IsNullOrWhiteSpace(state.RawParameters) && !recognized)
            {
                yield return ValidationIssue.Warning("HPThreshold 参数无法识别，将回退为默认阈值。");
            }
        }

        public static bool TryBuildCustomParameterHint(
            SignalWatchConditionAuthoringState state,
            out string message,
            out bool isWarning)
        {
            message = string.Empty;
            isWarning = false;
            if (state.IsHPThreshold)
            {
                return false;
            }

            var validCount = ConditionParameterCodec.CountValidEntries(state.RawParameters);
            var invalidCount = ConditionParameterCodec.CountInvalidEntries(state.RawParameters);
            if (invalidCount > 0 && validCount == 0)
            {
                message = "当前参数里没有有效的 key=value 项；只有合法项会进入编译与运行时。";
                isWarning = true;
                return true;
            }

            if (invalidCount > 0)
            {
                message = $"已忽略 {invalidCount} 个无效参数片段，当前有效参数将规范化为: {state.NormalizedParameters}";
                isWarning = true;
                return true;
            }

            if (!string.Equals(state.RawParameters ?? string.Empty, state.NormalizedParameters ?? string.Empty, StringComparison.Ordinal))
            {
                message = $"参数已按 key=value 规范化为: {state.NormalizedParameters}";
                isWarning = false;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(state.ParameterSummary))
            {
                message = $"参数摘要: {state.ParameterSummary}";
                isWarning = false;
                return true;
            }

            return false;
        }

        public static string ToReadableModeLabel(string? value)
        {
            return ConditionWatchSemanticUtility.NormalizeMode(value) switch
            {
                DropByMode => "每下降阈值 DropBy",
                _ => "降至阈值 DropTo",
            };
        }

        public static string SerializeHPThreshold(float threshold, string? mode)
        {
            return ConditionWatchSemanticUtility.SerializeParameters(
                HPThresholdConditionType,
                ConditionWatchSemanticUtility.BuildHPThresholdParameters(threshold, mode));
        }

        public static int FindConditionTypeOptionIndex(string? conditionType)
        {
            return ConditionWatchSemanticUtility.IsHPThreshold(conditionType) ? 0 : 1;
        }

        public static int FindHPModeOptionIndex(string? mode)
        {
            return string.Equals(
                ConditionWatchSemanticUtility.NormalizeMode(mode),
                DropByMode,
                StringComparison.Ordinal)
                ? 1
                : 0;
        }

        private static SignalWatchConditionAuthoringState BuildState(
            string rawConditionType,
            string targetRefSerialized,
            string rawParameters,
            float timeoutSeconds,
            bool repeat)
        {
            var conditionType = ConditionWatchSemanticUtility.NormalizeConditionType(rawConditionType);
            var isHPThreshold = ConditionWatchSemanticUtility.IsHPThreshold(conditionType);
            var normalizedParameters = isHPThreshold
                ? ConditionWatchSemanticUtility.SerializeParameters(
                    HPThresholdConditionType,
                    ConditionWatchSemanticUtility.BuildHPThresholdParameters(
                        DefaultHPThreshold,
                        DropToMode))
                : string.Empty;

            float threshold = DefaultHPThreshold;
            var mode = DropToMode;
            if (isHPThreshold)
            {
                ConditionWatchSemanticUtility.TryParseHPThreshold(rawParameters, out threshold, out mode);
                threshold = ConditionWatchSemanticUtility.NormalizeThreshold(threshold);
                mode = ConditionWatchSemanticUtility.NormalizeMode(mode);
                normalizedParameters = ConditionWatchSemanticUtility.SerializeParameters(
                    HPThresholdConditionType,
                    ConditionWatchSemanticUtility.BuildHPThresholdParameters(threshold, mode));
            }
            else if (!string.IsNullOrWhiteSpace(rawParameters))
            {
                normalizedParameters = ConditionWatchSemanticUtility.SerializeParameters(conditionType, rawParameters);
            }

            return new SignalWatchConditionAuthoringState(
                conditionType,
                targetRefSerialized ?? string.Empty,
                rawParameters ?? string.Empty,
                normalizedParameters,
                Math.Max(0f, timeoutSeconds),
                repeat,
                isHPThreshold,
                threshold,
                mode,
                ConditionWatchSemanticUtility.BuildParameterSummary(conditionType, normalizedParameters));
        }

        private static PropertyDefinition CreateParametersProperty()
        {
            var property = Prop.ConditionParams(
                ActionPortIds.SignalWatchCondition.Parameters,
                "条件参数",
                defaultValue: string.Empty,
                tooltip: "键值对参数，如 threshold=0.3;mode=DropTo。")
                .InSection("condition", "条件 Condition", sectionOrder: 0);
            property.AuthoringRule = new SignalWatchConditionParametersPropertyRule();
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

    public readonly struct SignalWatchConditionAuthoringState
    {
        public SignalWatchConditionAuthoringState(
            string conditionType,
            string targetRefSerialized,
            string rawParameters,
            string normalizedParameters,
            float timeoutSeconds,
            bool repeat,
            bool isHPThreshold,
            float threshold,
            string mode,
            string parameterSummary)
        {
            ConditionType = conditionType ?? string.Empty;
            TargetRefSerialized = targetRefSerialized ?? string.Empty;
            RawParameters = rawParameters ?? string.Empty;
            NormalizedParameters = normalizedParameters ?? string.Empty;
            TimeoutSeconds = timeoutSeconds;
            Repeat = repeat;
            IsHPThreshold = isHPThreshold;
            Threshold = threshold;
            Mode = mode ?? string.Empty;
            ParameterSummary = parameterSummary ?? string.Empty;
        }

        public string ConditionType { get; }

        public string TargetRefSerialized { get; }

        public string RawParameters { get; }

        public string NormalizedParameters { get; }

        public float TimeoutSeconds { get; }

        public bool Repeat { get; }

        public bool IsHPThreshold { get; }

        public float Threshold { get; }

        public string Mode { get; }

        public string ParameterSummary { get; }
    }
}
