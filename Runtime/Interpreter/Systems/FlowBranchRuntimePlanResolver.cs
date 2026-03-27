#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal readonly struct FlowBranchRuntimePlan
    {
        public FlowBranchRuntimePlan(
            string planSource,
            bool conditionValue,
            string conditionSummary,
            string routedPort,
            GraphSemanticDescriptor? graphDescriptor)
        {
            PlanSource = planSource ?? string.Empty;
            ConditionValue = conditionValue;
            ConditionSummary = conditionSummary ?? string.Empty;
            RoutedPort = routedPort ?? string.Empty;
            GraphDescriptor = graphDescriptor
                ?? SemanticDescriptorUtility.BuildFlowBranchGraphDescriptor(
                    conditionValue,
                    routedPort,
                    FlowBranchAuthoringUtility.BuildRouteSummary(conditionValue, routedPort));
        }

        public string PlanSource { get; }

        public bool ConditionValue { get; }

        public string ConditionSummary { get; }

        public string RoutedPort { get; }

        public GraphSemanticDescriptor GraphDescriptor { get; }

        public string GraphSummary =>
            GraphDescriptor.Summary?.Trim() ?? string.Empty;

        public string PlanSummary =>
            string.IsNullOrWhiteSpace(ConditionSummary)
                ? string.IsNullOrWhiteSpace(GraphSummary)
                    ? PlanSource
                    : $"{PlanSource} | {GraphSummary}"
                : $"{PlanSource} | {ConditionSummary}";

        public string ExecutionSummary =>
            string.IsNullOrWhiteSpace(GraphSummary)
                ? FlowBranchAuthoringUtility.BuildRouteSummary(ConditionValue, RoutedPort)
                : GraphSummary;
    }

    /// <summary>
    /// 统一收口 Flow.Branch 在 runtime 的计划解析。
    /// system 主逻辑只消费 branch plan，compiled 缺失时的默认收口也压到这里。
    /// </summary>
    internal static class FlowBranchRuntimePlanResolver
    {
        private const string MissingCompiledPlanSource = "compiled-missing";

        public static FlowBranchRuntimePlan Resolve(BlueprintFrame frame, int actionIndex)
        {
            var compiledBranch = CompiledActionResolver.TryGetFlowBranch(frame, actionIndex);
            if (compiledBranch != null)
            {
                var graphDescriptor = SemanticDescriptorUtility.TryGetGraphDescriptor(
                    compiledBranch.Semantics,
                    "flow.branch",
                    out var compiledGraphDescriptor)
                    ? compiledGraphDescriptor
                    : null;
                var routedPort = !string.IsNullOrWhiteSpace(compiledBranch.RoutedPort)
                    ? compiledBranch.RoutedPort
                    : !string.IsNullOrWhiteSpace(graphDescriptor?.RoutedPort)
                        ? graphDescriptor.RoutedPort
                    : compiledBranch.ConditionValue
                        ? ActionPortIds.FlowBranch.True
                        : ActionPortIds.FlowBranch.False;
                var conditionSummary = !string.IsNullOrWhiteSpace(compiledBranch.ConditionSummary)
                    ? compiledBranch.ConditionSummary
                    : FlowBranchAuthoringUtility.BuildConditionSummary(compiledBranch.ConditionValue);
                return new FlowBranchRuntimePlan(
                    "compiled",
                    compiledBranch.ConditionValue,
                    conditionSummary,
                    routedPort,
                    graphDescriptor
                    ?? SemanticDescriptorUtility.BuildFlowBranchGraphDescriptor(
                        compiledBranch.ConditionValue,
                        routedPort,
                        FlowBranchAuthoringUtility.BuildRouteSummary(compiledBranch.ConditionValue, routedPort)));
            }

            return new FlowBranchRuntimePlan(
                MissingCompiledPlanSource,
                false,
                "缺少 Flow.Branch compiled plan，默认走 false",
                ActionPortIds.FlowBranch.False,
                SemanticDescriptorUtility.BuildFlowBranchGraphDescriptor(
                    false,
                    ActionPortIds.FlowBranch.False,
                    FlowBranchAuthoringUtility.BuildRouteSummary(
                        conditionValue: false,
                        routedPort: ActionPortIds.FlowBranch.False)));
        }
    }
}
