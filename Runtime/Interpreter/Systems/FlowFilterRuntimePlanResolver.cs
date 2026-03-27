#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal readonly struct FlowFilterRuntimePlan
    {
        public FlowFilterRuntimePlan(
            string planSource,
            string @operator,
            string constValueText,
            string conditionSummary,
            bool missingCompareInputPasses,
            bool forceUnconditionalPass,
            SemanticDescriptorSet? semantics)
        {
            PlanSource = planSource ?? string.Empty;
            Operator = @operator ?? string.Empty;
            ConstValueText = constValueText ?? string.Empty;
            ConditionSummary = conditionSummary ?? string.Empty;
            MissingCompareInputPasses = missingCompareInputPasses;
            ForceUnconditionalPass = forceUnconditionalPass;
            Semantics = semantics ?? new SemanticDescriptorSet
            {
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildFlowFilterConditionDescriptor(
                        Operator,
                        ConstValueText,
                        ConditionSummary),
                },
            };
        }

        public string PlanSource { get; }

        public string Operator { get; }

        public string ConstValueText { get; }

        public string ConditionSummary { get; }

        public bool MissingCompareInputPasses { get; }

        public bool ForceUnconditionalPass { get; }

        public SemanticDescriptorSet Semantics { get; }

        public string PlanSummary =>
            string.IsNullOrWhiteSpace(ConditionSummary)
                ? PlanSource
                : $"{PlanSource} | {ConditionSummary}";

        public string ExecutionSummary =>
            ForceUnconditionalPass
                ? "缺少 Flow.Filter compiled plan，默认通过 -> pass"
                : string.IsNullOrWhiteSpace(ConditionSummary)
                ? "等待条件评估"
                : $"等待条件评估 | {ConditionSummary}";
    }

    internal static class FlowFilterRuntimePlanResolver
    {
        private const string MissingCompiledPlanSource = "compiled-missing";

        public static FlowFilterRuntimePlan Resolve(BlueprintFrame frame, int actionIndex)
        {
            var compiledFilter = CompiledActionResolver.TryGetFlowFilter(frame, actionIndex);
            if (compiledFilter != null)
            {
                var semantics = compiledFilter.Semantics;
                var conditionSummary = SemanticDescriptorUtility.GetConditionSummary(
                    semantics,
                    compiledFilter.ConditionSummary);
                return new FlowFilterRuntimePlan(
                    "compiled",
                    compiledFilter.Operator,
                    compiledFilter.ConstValueText,
                    conditionSummary,
                    compiledFilter.MissingCompareInputPasses,
                    forceUnconditionalPass: false,
                    semantics);
            }

            return new FlowFilterRuntimePlan(
                MissingCompiledPlanSource,
                string.Empty,
                string.Empty,
                "缺少 Flow.Filter compiled plan，默认通过",
                missingCompareInputPasses: false,
                forceUnconditionalPass: true,
                semantics: new SemanticDescriptorSet
                {
                    Conditions = new[]
                    {
                        SemanticDescriptorUtility.BuildFlowFilterConditionDescriptor(
                            string.Empty,
                            string.Empty,
                            "缺少 Flow.Filter compiled plan，默认通过"),
                    },
                });
        }
    }
}
