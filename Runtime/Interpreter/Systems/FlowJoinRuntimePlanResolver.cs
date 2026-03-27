#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal readonly struct FlowJoinRuntimePlan
    {
        public FlowJoinRuntimePlan(
            string planSource,
            int requiredCount,
            string incomingActionSummary,
            string conditionSummary,
            GraphSemanticDescriptor? graphDescriptor)
        {
            var normalizedRequiredCount = Math.Max(1, requiredCount);
            var normalizedIncomingSummary = incomingActionSummary ?? string.Empty;
            PlanSource = planSource ?? string.Empty;
            RequiredCount = normalizedRequiredCount;
            IncomingActionSummary = normalizedIncomingSummary;
            ConditionSummary = conditionSummary ?? string.Empty;
            GraphDescriptor = graphDescriptor
                ?? SemanticDescriptorUtility.BuildFlowJoinGraphDescriptor(
                    normalizedRequiredCount,
                    Array.Empty<string>(),
                    SemanticSummaryUtility.BuildFlowJoinIncomingSummary(Array.Empty<string>()));
        }

        public string PlanSource { get; }

        public int RequiredCount { get; }

        public string IncomingActionSummary { get; }

        public string ConditionSummary { get; }

        public GraphSemanticDescriptor GraphDescriptor { get; }

        public string GraphSummary =>
            GraphDescriptor.Summary?.Trim() ?? string.Empty;

        public string PlanSummary =>
            string.IsNullOrWhiteSpace(GraphSummary)
                ? $"{PlanSource} | {ConditionSummary}"
                : $"{PlanSource} | {ConditionSummary} | {GraphSummary}";
    }

    /// <summary>
    /// 统一收口 Flow.Join 在 runtime 的计划解析。
    /// 运行时主路径优先消费 compiled payload；无编译结果时走最小 `compiled-missing` 默认 barrier；
    /// activation 无 frame 场景下的 synthetic 默认值也统一从这里下沉；
    /// compiled 稀疏 metadata 不再从 runtime transitions 现场补图，而是走显式 sparse fallback。
    /// </summary>
    internal static class FlowJoinRuntimePlanResolver
    {
        private const string MissingCompiledPlanSource = "compiled-missing";
        private const string SparseCompiledPlanSource = "compiled-sparse";
        private const string SyntheticPlanSource = "synthetic";

        public static FlowJoinRuntimePlan Resolve(BlueprintFrame frame, int actionIndex, int fallbackRequiredCount = 1)
        {
            var compiledJoin = CompiledActionResolver.TryGetFlowJoin(frame, actionIndex);
            if (compiledJoin != null)
            {
                return ResolveFromCompiled(compiledJoin, fallbackRequiredCount);
            }

            return CreateMissingCompiledPlan(fallbackRequiredCount);
        }

        public static FlowJoinRuntimePlan ResolveForActivation(
            BlueprintFrame? frame,
            int actionIndex,
            int fallbackRequiredCount = 1)
        {
            return frame != null
                ? Resolve(frame, actionIndex, fallbackRequiredCount)
                : CreateSyntheticActivationPlan(fallbackRequiredCount);
        }

        private static FlowJoinRuntimePlan ResolveFromCompiled(
            FlowJoinCompiledData compiledJoin,
            int fallbackRequiredCount)
        {
            var compiledGraphDescriptor = SemanticDescriptorUtility.TryGetGraphDescriptor(
                compiledJoin.Semantics,
                "flow.join",
                out var resolvedGraphDescriptor)
                ? resolvedGraphDescriptor
                : null;
            var incomingActionIds = ResolveCompiledIncomingActionIds(compiledJoin, compiledGraphDescriptor);
            var requiredCount = compiledJoin.RequiredCount > 0
                ? compiledJoin.RequiredCount
                : compiledGraphDescriptor?.RequiredCount > 0
                    ? compiledGraphDescriptor.RequiredCount
                    : Math.Max(1, fallbackRequiredCount);
            var incomingActionSummary = !string.IsNullOrWhiteSpace(compiledJoin.IncomingActionSummary)
                ? compiledJoin.IncomingActionSummary
                : SemanticSummaryUtility.BuildFlowJoinIncomingSummary(incomingActionIds);
            var conditionSummary = !string.IsNullOrWhiteSpace(compiledJoin.ConditionSummary)
                ? compiledJoin.ConditionSummary
                : SemanticSummaryUtility.BuildFlowJoinConditionSummary(requiredCount);
            var planSource = HasCompiledGraphContract(compiledJoin, compiledGraphDescriptor)
                ? "compiled"
                : SparseCompiledPlanSource;
            return new FlowJoinRuntimePlan(
                planSource,
                requiredCount,
                incomingActionSummary,
                conditionSummary,
                ResolveCompiledGraphDescriptor(
                    compiledGraphDescriptor,
                    requiredCount,
                    incomingActionIds,
                    incomingActionSummary));
        }

        private static GraphSemanticDescriptor ResolveCompiledGraphDescriptor(
            GraphSemanticDescriptor? compiledGraphDescriptor,
            int requiredCount,
            string[] incomingActionIds,
            string incomingActionSummary)
        {
            return compiledGraphDescriptor != null
                ? compiledGraphDescriptor
                : SemanticDescriptorUtility.BuildFlowJoinGraphDescriptor(
                    requiredCount,
                    incomingActionIds,
                    incomingActionSummary);
        }

        private static bool HasCompiledGraphContract(
            FlowJoinCompiledData compiledJoin,
            GraphSemanticDescriptor? compiledGraphDescriptor)
        {
            return compiledJoin.RequiredCount > 0
                   || (compiledJoin.IncomingActionIds != null && compiledJoin.IncomingActionIds.Length > 0)
                   || !string.IsNullOrWhiteSpace(compiledJoin.IncomingActionSummary)
                   || !string.IsNullOrWhiteSpace(compiledJoin.ConditionSummary)
                   || (compiledGraphDescriptor?.RequiredCount > 0)
                   || (compiledGraphDescriptor?.IncomingActionIds != null && compiledGraphDescriptor.IncomingActionIds.Length > 0)
                   || !string.IsNullOrWhiteSpace(compiledGraphDescriptor?.Summary);
        }

        private static string[] ResolveCompiledIncomingActionIds(
            FlowJoinCompiledData compiledJoin,
            GraphSemanticDescriptor? compiledGraphDescriptor)
        {
            if (compiledJoin.IncomingActionIds != null && compiledJoin.IncomingActionIds.Length > 0)
            {
                return NormalizeIncomingActionIds(compiledJoin.IncomingActionIds);
            }

            if (compiledGraphDescriptor?.IncomingActionIds != null && compiledGraphDescriptor.IncomingActionIds.Length > 0)
            {
                return NormalizeIncomingActionIds(compiledGraphDescriptor.IncomingActionIds);
            }

            return Array.Empty<string>();
        }

        private static FlowJoinRuntimePlan CreateMissingCompiledPlan(int fallbackRequiredCount)
        {
            var requiredCount = Math.Max(1, fallbackRequiredCount);
            return new FlowJoinRuntimePlan(
                MissingCompiledPlanSource,
                requiredCount,
                string.Empty,
                "缺少 Flow.Join compiled plan，默认按单路输入执行",
                SemanticDescriptorUtility.BuildFlowJoinGraphDescriptor(
                    requiredCount,
                    Array.Empty<string>(),
                    SemanticSummaryUtility.BuildFlowJoinConditionSummary(requiredCount)));
        }

        private static FlowJoinRuntimePlan CreateSyntheticActivationPlan(int fallbackRequiredCount)
        {
            var requiredCount = Math.Max(1, fallbackRequiredCount);
            var incomingActionSummary = SemanticSummaryUtility.BuildFlowJoinIncomingSummary(Array.Empty<string>());
            return new FlowJoinRuntimePlan(
                SyntheticPlanSource,
                requiredCount,
                incomingActionSummary,
                SemanticSummaryUtility.BuildFlowJoinConditionSummary(requiredCount),
                SemanticDescriptorUtility.BuildFlowJoinGraphDescriptor(
                    requiredCount,
                    Array.Empty<string>(),
                    incomingActionSummary));
        }

        private static string[] NormalizeIncomingActionIds(string[]? actionIds)
        {
            if (actionIds == null || actionIds.Length == 0)
            {
                return Array.Empty<string>();
            }

            var normalized = new List<string>(actionIds.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < actionIds.Length; index++)
            {
                var actionId = actionIds[index]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(actionId) || !seen.Add(actionId))
                {
                    continue;
                }

                normalized.Add(actionId);
            }

            return normalized.ToArray();
        }
    }
}
