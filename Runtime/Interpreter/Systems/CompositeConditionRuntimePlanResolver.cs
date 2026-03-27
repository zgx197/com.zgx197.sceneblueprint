#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal readonly struct CompositeConditionRuntimePlan
    {
        public CompositeConditionRuntimePlan(
            string planSource,
            string mode,
            float timeoutSeconds,
            int connectedMask,
            string[] connectedPortIds,
            string connectedPortSummary,
            string conditionSummary,
            GraphSemanticDescriptor? graphDescriptor)
        {
            PlanSource = planSource ?? string.Empty;
            Mode = SemanticSummaryUtility.NormalizeCompositeConditionMode(mode);
            TimeoutSeconds = timeoutSeconds < 0f ? 0f : timeoutSeconds;
            ConnectedMask = connectedMask;
            ConnectedPortIds = connectedPortIds ?? System.Array.Empty<string>();
            ConnectedPortSummary = connectedPortSummary ?? string.Empty;
            ConditionSummary = conditionSummary ?? string.Empty;
            GraphDescriptor = graphDescriptor
                ?? SemanticDescriptorUtility.BuildCompositeGraphDescriptor(
                    Mode,
                    CountConnectedConditions(ConnectedMask),
                    ConnectedMask,
                    ConnectedPortIds);
        }

        public string PlanSource { get; }

        public string Mode { get; }

        public float TimeoutSeconds { get; }

        public int ConnectedMask { get; }

        public string[] ConnectedPortIds { get; }

        public string ConnectedPortSummary { get; }

        public string ConditionSummary { get; }

        public GraphSemanticDescriptor GraphDescriptor { get; }

        public string GraphSummary =>
            GraphDescriptor.Summary?.Trim() ?? string.Empty;

        public string PlanSummary =>
            string.IsNullOrWhiteSpace(GraphSummary)
                ? $"{PlanSource} | {SemanticSummaryUtility.DescribeCompositeConditionMode(Mode)} | {CountConnectedConditions(ConnectedMask)} 条条件"
                : $"{PlanSource} | {GraphSummary}";

        private static int CountConnectedConditions(int mask)
        {
            var count = 0;
            var value = mask;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }
    }

    /// <summary>
    /// 统一收口 CompositeCondition 在 runtime 的计划解析。
    /// system 主逻辑只消费 plan，compiled、缺 plan 与 activation synthetic 默认值都压到这里；
    /// compiled 稀疏 metadata 不再从 runtime transitions 现场补图，而是走显式 sparse fallback。
    /// </summary>
    internal static class CompositeConditionRuntimePlanResolver
    {
        private const string MissingCompiledPlanSource = "compiled-missing";
        private const float MissingCompiledTimeoutSeconds = 1f;
        private const string SparseCompiledPlanSource = "compiled-sparse";
        private const string SyntheticPlanSource = "synthetic";

        public static CompositeConditionRuntimePlan Resolve(BlueprintFrame frame, int actionIndex)
        {
            var compiled = CompiledActionResolver.TryGetSignalCompositeCondition(frame, actionIndex);
            return compiled != null
                ? ResolveFromCompiled(compiled)
                : CreateMissingCompiledPlan();
        }

        public static CompositeConditionRuntimePlan ResolveForActivation(BlueprintFrame? frame, int actionIndex)
        {
            return frame != null
                ? Resolve(frame, actionIndex)
                : CreateSyntheticActivationPlan();
        }

        private static CompositeConditionRuntimePlan ResolveFromCompiled(
            SignalCompositeConditionCompiledData compiled)
        {
            var graphDescriptor = SemanticDescriptorUtility.TryGetGraphDescriptor(
                compiled.Semantics,
                "signal.composite",
                out var compiledGraphDescriptor)
                ? compiledGraphDescriptor
                : null;
            var connectedPortIds = ResolveCompiledConnectedPortIds(compiled, graphDescriptor);
            var mode = !string.IsNullOrWhiteSpace(compiled.Mode)
                ? compiled.Mode
                : !string.IsNullOrWhiteSpace(graphDescriptor?.Mode)
                    ? graphDescriptor.Mode
                    : SignalCompositeConditionAuthoringUtility.AndMode;
            var connectedMask = compiled.ConnectedMask > 0
                ? compiled.ConnectedMask
                : graphDescriptor?.ConnectedMask > 0
                    ? graphDescriptor.ConnectedMask
                    : SignalCompositeConditionAuthoringUtility.BuildConnectedMask(connectedPortIds);
            var connectedPortSummary = SignalCompositeConditionAuthoringUtility.BuildConnectedPortSummary(connectedPortIds);
            var conditionSummary = !string.IsNullOrWhiteSpace(compiled.ConditionSummary)
                ? compiled.ConditionSummary
                : !string.IsNullOrWhiteSpace(graphDescriptor?.Summary)
                    ? graphDescriptor.Summary
                    : SignalCompositeConditionAuthoringUtility.BuildConditionSummary(mode, connectedPortIds);
            var timeoutSeconds = ResolveTimeoutSeconds(compiled, connectedMask);
            var planSource = HasCompiledGraphContract(compiled, graphDescriptor)
                ? "compiled"
                : SparseCompiledPlanSource;

            return new CompositeConditionRuntimePlan(
                planSource,
                mode,
                timeoutSeconds,
                connectedMask,
                connectedPortIds,
                connectedPortSummary,
                conditionSummary,
                graphDescriptor
                ?? SemanticDescriptorUtility.BuildCompositeGraphDescriptor(
                    mode,
                    CountConnectedConditions(connectedMask),
                    connectedMask,
                    connectedPortIds,
                    conditionSummary));
        }

        private static bool HasCompiledGraphContract(
            SignalCompositeConditionCompiledData compiled,
            GraphSemanticDescriptor? graphDescriptor)
        {
            return !string.IsNullOrWhiteSpace(compiled.Mode)
                   || compiled.TimeoutSeconds > 0f
                   || compiled.ConnectedMask > 0
                   || compiled.ConnectedCount > 0
                   || (compiled.ConnectedPortIds != null && compiled.ConnectedPortIds.Length > 0)
                   || !string.IsNullOrWhiteSpace(compiled.ConditionSummary)
                   || !string.IsNullOrWhiteSpace(graphDescriptor?.Mode)
                   || (graphDescriptor?.ConnectedMask > 0)
                   || (graphDescriptor?.ConnectedCount > 0)
                   || (graphDescriptor?.ConnectedPortIds != null && graphDescriptor.ConnectedPortIds.Length > 0)
                   || !string.IsNullOrWhiteSpace(graphDescriptor?.Summary);
        }

        private static string[] ResolveCompiledConnectedPortIds(
            SignalCompositeConditionCompiledData compiled,
            GraphSemanticDescriptor? graphDescriptor)
        {
            if (compiled.ConnectedPortIds != null && compiled.ConnectedPortIds.Length > 0)
            {
                return SignalCompositeConditionAuthoringUtility.NormalizeAndSortConnectedPortIds(compiled.ConnectedPortIds);
            }

            if (graphDescriptor?.ConnectedPortIds != null && graphDescriptor.ConnectedPortIds.Length > 0)
            {
                return SignalCompositeConditionAuthoringUtility.NormalizeAndSortConnectedPortIds(graphDescriptor.ConnectedPortIds);
            }

            return System.Array.Empty<string>();
        }

        private static float ResolveTimeoutSeconds(
            SignalCompositeConditionCompiledData compiled,
            int connectedMask)
        {
            if (compiled.TimeoutSeconds > 0f)
            {
                return compiled.TimeoutSeconds;
            }

            return connectedMask == 0
                ? MissingCompiledTimeoutSeconds
                : 0f;
        }

        private static CompositeConditionRuntimePlan CreateMissingCompiledPlan()
        {
            const string summary = "缺少 Signal.CompositeCondition compiled plan，按默认超时 1s 收口";
            return new CompositeConditionRuntimePlan(
                MissingCompiledPlanSource,
                SignalCompositeConditionAuthoringUtility.AndMode,
                MissingCompiledTimeoutSeconds,
                0,
                System.Array.Empty<string>(),
                string.Empty,
                summary,
                SemanticDescriptorUtility.BuildCompositeGraphDescriptor(
                    SignalCompositeConditionAuthoringUtility.AndMode,
                    0,
                    0,
                    System.Array.Empty<string>(),
                    summary));
        }

        private static CompositeConditionRuntimePlan CreateSyntheticActivationPlan()
        {
            var mode = SignalCompositeConditionAuthoringUtility.AndMode;
            return new CompositeConditionRuntimePlan(
                SyntheticPlanSource,
                mode,
                0f,
                0,
                System.Array.Empty<string>(),
                string.Empty,
                string.Empty,
                SemanticDescriptorUtility.BuildCompositeGraphDescriptor(
                    mode,
                    0,
                    0,
                    System.Array.Empty<string>()));
        }

        private static int CountConnectedConditions(int mask)
        {
            var count = 0;
            var value = mask;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }
    }
}
