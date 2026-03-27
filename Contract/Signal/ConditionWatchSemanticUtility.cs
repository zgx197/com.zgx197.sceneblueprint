#nullable enable
using System;
using System.Globalization;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// WatchCondition 最小语义工具。
    /// 统一收口运行时与编译期都需要共享的 condition type / HPThreshold 参数规范化逻辑，
    /// 避免在 runtime fallback、compiler 和 editor authoring 中重复维护同一套字符串协议。
    /// </summary>
    public static class ConditionWatchSemanticUtility
    {
        public const string HPThresholdConditionType = "HPThreshold";
        public const string HPThresholdKey = "threshold";
        public const string HPModeKey = "mode";
        public const string DropToMode = "DropTo";
        public const string DropByMode = "DropBy";
        public const float DefaultHPThreshold = 0.3f;

        public static string NormalizeConditionType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return string.Equals(trimmed, HPThresholdConditionType, StringComparison.OrdinalIgnoreCase)
                ? HPThresholdConditionType
                : trimmed;
        }

        public static bool IsHPThreshold(string? conditionType)
        {
            return string.Equals(
                NormalizeConditionType(conditionType),
                HPThresholdConditionType,
                StringComparison.Ordinal);
        }

        public static bool TryParseHPThreshold(
            string? raw,
            out float threshold,
            out string mode)
        {
            return TryParseHPThreshold(ConditionParameterCodec.Parse(raw), out threshold, out mode);
        }

        public static bool TryParseHPThreshold(
            ConditionParameter[]? parameters,
            out float threshold,
            out string mode)
        {
            threshold = DefaultHPThreshold;
            mode = DropToMode;

            if (parameters == null || parameters.Length == 0)
            {
                return false;
            }

            var recognized = false;
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key))
                {
                    continue;
                }

                if (string.Equals(parameter.Key, HPThresholdKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(parameter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedThreshold))
                    {
                        threshold = NormalizeThreshold(parsedThreshold);
                    }

                    recognized = true;
                    continue;
                }

                if (string.Equals(parameter.Key, HPModeKey, StringComparison.OrdinalIgnoreCase))
                {
                    mode = NormalizeMode(parameter.Value);
                    recognized = true;
                }
            }

            return recognized;
        }

        public static ConditionParameter[] BuildHPThresholdParameters(float threshold, string? mode)
        {
            var normalizedThreshold = NormalizeThreshold(threshold);
            var normalizedMode = NormalizeMode(mode);
            return new[]
            {
                new ConditionParameter
                {
                    Key = HPThresholdKey,
                    Value = FormatThreshold(normalizedThreshold),
                },
                new ConditionParameter
                {
                    Key = HPModeKey,
                    Value = normalizedMode,
                }
            };
        }

        public static ConditionParameter[] NormalizeParameters(string? conditionType, string? rawParameters)
        {
            if (IsHPThreshold(conditionType))
            {
                TryParseHPThreshold(rawParameters, out var threshold, out var mode);
                return BuildHPThresholdParameters(threshold, mode);
            }

            return ConditionParameterCodec.Parse(rawParameters);
        }

        public static ConditionParameter[] NormalizeParameters(string? conditionType, ConditionParameter[]? parameters)
        {
            if (IsHPThreshold(conditionType))
            {
                TryParseHPThreshold(parameters, out var threshold, out var mode);
                return BuildHPThresholdParameters(threshold, mode);
            }

            return parameters ?? Array.Empty<ConditionParameter>();
        }

        public static string SerializeParameters(string? conditionType, string? rawParameters)
        {
            return ConditionParameterCodec.Serialize(NormalizeParameters(conditionType, rawParameters));
        }

        public static string SerializeParameters(string? conditionType, ConditionParameter[]? parameters)
        {
            return ConditionParameterCodec.Serialize(NormalizeParameters(conditionType, parameters));
        }

        public static string BuildParameterSummary(string? conditionType, string? rawParameters)
        {
            return BuildParameterSummary(conditionType, NormalizeParameters(conditionType, rawParameters));
        }

        public static string BuildParameterSummary(string? conditionType, ConditionParameter[]? parameters)
        {
            if (IsHPThreshold(conditionType))
            {
                TryParseHPThreshold(parameters, out var threshold, out var mode);
                return NormalizeMode(mode) == DropByMode
                    ? $"每下降 {threshold * 100f:0.#}% 触发"
                    : $"降到 {threshold * 100f:0.#}% 触发";
            }

            return ConditionParameterCodec.Serialize(parameters);
        }

        public static string BuildConditionSummary(
            string? conditionType,
            string? targetSummary,
            string? rawParameters)
        {
            return SemanticSummaryUtility.BuildWatchConditionSummary(
                NormalizeConditionType(conditionType),
                targetSummary,
                BuildParameterSummary(conditionType, rawParameters));
        }

        public static ConditionWatchDescriptor BuildDescriptor(
            string? conditionType,
            string? targetRefSerialized,
            string? rawParameters,
            float timeoutSeconds)
        {
            var normalizedConditionType = NormalizeConditionType(conditionType);
            return new ConditionWatchDescriptor
            {
                ConditionType = normalizedConditionType,
                Target = EntityRefCodec.Parse(targetRefSerialized),
                Parameters = NormalizeParameters(normalizedConditionType, rawParameters),
                Timeout = Math.Max(0f, timeoutSeconds),
            };
        }

        public static string NormalizeMode(string? value)
        {
            if (string.Equals(value?.Trim(), DropByMode, StringComparison.OrdinalIgnoreCase))
            {
                return DropByMode;
            }

            return DropToMode;
        }

        public static float NormalizeThreshold(float threshold)
        {
            if (threshold < 0f)
            {
                return 0f;
            }

            if (threshold > 1f)
            {
                return 1f;
            }

            return threshold;
        }

        public static string FormatThreshold(float threshold)
        {
            return NormalizeThreshold(threshold).ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
