#nullable enable
using System.Globalization;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    public readonly struct FlowDelayRuntimeConfig
    {
        public FlowDelayRuntimeConfig(
            string planSource,
            float rawDelaySeconds,
            float effectiveDelaySeconds)
        {
            PlanSource = NormalizePlanSource(planSource);
            RawDelaySeconds = rawDelaySeconds;
            EffectiveDelaySeconds = effectiveDelaySeconds;
        }

        public string PlanSource { get; }

        public float RawDelaySeconds { get; }

        public float EffectiveDelaySeconds { get; }

        public string ConditionSummary => Summary;

        public string PlanSummary => $"{PlanSource} | {ConditionSummary}";

        public string Summary =>
            $"延迟 {EffectiveDelaySeconds.ToString("0.###", CultureInfo.InvariantCulture)} 秒";

        private static string NormalizePlanSource(string? planSource)
        {
            return string.IsNullOrWhiteSpace(planSource) ? "unknown" : planSource.Trim();
        }
    }

    internal static class FlowDelayRuntimeConfigResolver
    {
        private const string MissingCompiledPlanSource = "compiled-missing";

        public static FlowDelayRuntimeConfig Resolve(BlueprintFrame frame, int actionIndex)
        {
            var compiledDelay = CompiledActionResolver.TryGetFlowDelay(frame, actionIndex);
            if (compiledDelay != null)
            {
                return new FlowDelayRuntimeConfig(
                    "compiled",
                    compiledDelay.RawDelaySeconds,
                    compiledDelay.EffectiveDelaySeconds > 0f ? compiledDelay.EffectiveDelaySeconds : 1f);
            }

            return new FlowDelayRuntimeConfig(MissingCompiledPlanSource, 1f, 1f);
        }
    }
}
