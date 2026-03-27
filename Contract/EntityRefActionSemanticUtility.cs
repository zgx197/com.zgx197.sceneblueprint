#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// Trigger / Interaction 这类主体引用动作的最小语义工具。
    /// 统一收口 fallback 默认值、range clamp 与 condition summary 拼装，
    /// 让 compiler 与 runtime 对“未配置主体/目标/区域”说同一种语言。
    /// </summary>
    public static class EntityRefActionSemanticUtility
    {
        public const string DefaultSubjectSummary = "Player";
        public const string DefaultTriggerAreaSummary = "未绑定区域";
        public const string DefaultTargetSummary = "未配置目标";

        public static string NormalizeTriggerSubjectSummary(string? subjectSummary)
        {
            return NormalizeSummary(subjectSummary, DefaultSubjectSummary);
        }

        public static string NormalizeTriggerAreaSummary(string? triggerAreaSummary)
        {
            return NormalizeSummary(triggerAreaSummary, DefaultTriggerAreaSummary);
        }

        public static string BuildTriggerEnterAreaConditionSummary(
            string? subjectSummary,
            string? triggerAreaSummary,
            bool requireFullyInside)
        {
            return SemanticSummaryUtility.BuildTriggerEnterAreaSummary(
                NormalizeTriggerSubjectSummary(subjectSummary),
                NormalizeTriggerAreaSummary(triggerAreaSummary),
                requireFullyInside);
        }

        public static string NormalizeInteractionSubjectSummary(string? subjectSummary)
        {
            return NormalizeSummary(subjectSummary, DefaultSubjectSummary);
        }

        public static string NormalizeInteractionTargetSummary(string? targetSummary)
        {
            return NormalizeSummary(targetSummary, DefaultTargetSummary);
        }

        public static float NormalizeInteractionRange(float range)
        {
            return Math.Max(0f, range);
        }

        public static string BuildInteractionApproachConditionSummary(
            string? subjectSummary,
            string? targetSummary,
            float range)
        {
            return SemanticSummaryUtility.BuildInteractionApproachSummary(
                NormalizeInteractionSubjectSummary(subjectSummary),
                NormalizeInteractionTargetSummary(targetSummary),
                NormalizeInteractionRange(range));
        }

        private static string NormalizeSummary(string? summary, string fallback)
        {
            return string.IsNullOrWhiteSpace(summary)
                ? fallback
                : summary.Trim();
        }
    }
}
