#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal readonly struct FlowFilterEvaluationResult
    {
        public FlowFilterEvaluationResult(
            string eventKind,
            string outputPortId,
            GraphNodeExecutionResult completionResult)
        {
            EventKind = eventKind ?? string.Empty;
            OutputPortId = outputPortId ?? string.Empty;
            CompletionResult = completionResult;
        }

        public string EventKind { get; }

        public string OutputPortId { get; }

        public GraphNodeExecutionResult CompletionResult { get; }
    }

    internal static class FlowFilterExecutionSupport
    {
        public static bool TryExecute(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var runtimePlan = FlowFilterRuntimePlanResolver.Resolve(frame, actionIndex);
            var filterState = GetOrCreateRunningState(
                ref view,
                frame,
                actionIndex,
                ref runtimeState,
                runtimePlan);
            var evaluationResult = Evaluate(
                frame,
                actionIndex,
                view.CurrentTick,
                runtimePlan,
                filterState);

            RecordEvaluationEvent(
                frame,
                actionIndex,
                view.CurrentTick,
                filterState,
                runtimePlan,
                evaluationResult);

            if (evaluationResult.CompletionResult.ShouldComplete)
            {
                if (!GraphNodeExecutionTemplate.TryFinalizeCompletion(
                        frame,
                        ref view,
                        actionIndex,
                        ref runtimeState,
                        FlowFilterSystem.FlowFilterStateDescriptor,
                        evaluationResult.CompletionResult))
                {
                    return false;
                }
            }
            else
            {
                GraphNodeExecutionTemplate.EmitFlowAndMoveToListening(
                    ref view,
                    actionIndex,
                    ref runtimeState,
                    evaluationResult.OutputPortId);
            }

            Debug.Log(BuildEvaluationLogMessage(actionIndex, filterState, view.CurrentTick, runtimeState.Phase));
            return true;
        }

        public static FlowFilterNodeState GetOrCreateRunningState(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            FlowFilterRuntimePlan runtimePlan)
        {
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                FlowFilterSystem.FlowFilterStateDescriptor,
                ref runtimeState);
            var filterState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(filterState, runtimePlan, view.CurrentTick);
            }
            else
            {
                BackfillStateIfMissing(filterState, runtimePlan);
            }

            return filterState;
        }

        public static void InitializeState(
            FlowFilterNodeState filterState,
            FlowFilterRuntimePlan runtimePlan,
            int currentTick)
        {
            filterState.StartTick = currentTick;
            filterState.LastEvaluationTick = currentTick;
            filterState.EvaluationCount = 0;
            GraphNodeExecutionTemplate.ApplyPlanHeader(
                filterState,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    runtimePlan.ConditionSummary));
            filterState.Operator = runtimePlan.Operator;
            filterState.ConstValueText = runtimePlan.ConstValueText;
            filterState.CompareValueText = string.Empty;
            filterState.ExecutionSummary = runtimePlan.ExecutionSummary;
            filterState.ConditionMet = false;
            filterState.WasUnconditionalPass = false;
            filterState.RoutedPort = string.Empty;
        }

        public static FlowFilterEvaluationResult Evaluate(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            FlowFilterRuntimePlan runtimePlan,
            FlowFilterNodeState filterState)
        {
            filterState.LastEvaluationTick = currentTick;
            filterState.EvaluationCount++;
            filterState.Operator = runtimePlan.Operator;
            filterState.ConstValueText = runtimePlan.ConstValueText;
            filterState.CompareValueText = string.Empty;
            filterState.ConditionMet = false;
            filterState.WasUnconditionalPass = false;
            filterState.RoutedPort = string.Empty;

            string? compareValue = frame.GetDataPortValue(actionIndex, ActionPortIds.FlowFilter.CompareValue);
            filterState.CompareValueText = compareValue ?? string.Empty;

            if (runtimePlan.ForceUnconditionalPass)
            {
                filterState.ConditionMet = true;
                filterState.WasUnconditionalPass = true;
                filterState.RoutedPort = ActionPortIds.FlowFilter.Pass;
                filterState.ExecutionSummary = "缺少 Flow.Filter compiled plan，默认通过 -> pass";
                return new FlowFilterEvaluationResult(
                    "Flow.Filter.Pass",
                    ActionPortIds.FlowFilter.Pass,
                    GraphNodeExecutionTemplate.CreateCompletionResult(
                        GraphNodeExecutionResultKind.Completed,
                        "Flow.Filter.Pass",
                        ActionPortIds.FlowFilter.Pass));
            }

            if (compareValue == null && runtimePlan.MissingCompareInputPasses)
            {
                filterState.ConditionMet = true;
                filterState.WasUnconditionalPass = true;
                filterState.RoutedPort = ActionPortIds.FlowFilter.Pass;
                filterState.ExecutionSummary = "比较值未接线，按无条件通过 -> pass";
                return new FlowFilterEvaluationResult(
                    "Flow.Filter.Pass",
                    ActionPortIds.FlowFilter.Pass,
                    GraphNodeExecutionTemplate.CreateCompletionResult(
                        GraphNodeExecutionResultKind.Completed,
                        "Flow.Filter.Pass",
                        ActionPortIds.FlowFilter.Pass));
            }

            var conditionMet = !string.IsNullOrEmpty(compareValue)
                && FlowFilterAuthoringUtility.EvaluateCondition(
                    compareValue,
                    runtimePlan.Operator,
                    runtimePlan.ConstValueText);
            var routedPort = conditionMet
                ? ActionPortIds.FlowFilter.Pass
                : ActionPortIds.FlowFilter.Reject;

            filterState.ConditionMet = conditionMet;
            filterState.RoutedPort = routedPort;
            filterState.ExecutionSummary = BuildExecutionSummary(filterState);

            return conditionMet
                ? new FlowFilterEvaluationResult(
                    "Flow.Filter.Pass",
                    routedPort,
                    GraphNodeExecutionTemplate.CreateCompletionResult(
                        GraphNodeExecutionResultKind.Completed,
                        "Flow.Filter.Pass",
                        routedPort))
                : new FlowFilterEvaluationResult(
                    "Flow.Filter.Reject",
                    routedPort,
                    GraphNodeExecutionTemplate.RunningResult);
        }

        public static void RecordEvaluationEvent(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            FlowFilterNodeState filterState,
            FlowFilterRuntimePlan runtimePlan,
            FlowFilterEvaluationResult evaluationResult)
        {
            var emissionOptions = new GraphNodeEventEmissionOptions(runtimePlan.Semantics);
            if (evaluationResult.CompletionResult.ShouldComplete)
            {
                GraphNodeExecutionTemplate.RecordCompletionEvent(
                    frame,
                    actionIndex,
                    currentTick,
                    filterState,
                    GraphNodeExecutionTemplate.CreateCompletionContract(
                        evaluationResult.CompletionResult,
                        emissionOptions),
                    payload => EnrichPayload(payload, filterState));
                return;
            }

            GraphNodeExecutionTemplate.RecordTimedEvent(
                frame,
                actionIndex,
                currentTick,
                filterState,
                GraphNodeExecutionTemplate.CreateEventContract(
                    evaluationResult.EventKind,
                    evaluationResult.OutputPortId,
                    emissionOptions),
                payload => EnrichPayload(payload, filterState));
        }

        public static string BuildEvaluationLogMessage(
            int actionIndex,
            FlowFilterNodeState filterState,
            int currentTick,
            ActionPhase phase)
        {
            return $"[FlowFilterSystem] Flow.Filter (index={actionIndex}) -> {phase}, " +
                   $"{filterState.ExecutionSummary}, waited {filterState.GetElapsedTicks(currentTick)} ticks";
        }

        private static void BackfillStateIfMissing(
            FlowFilterNodeState filterState,
            FlowFilterRuntimePlan runtimePlan)
        {
            GraphNodeExecutionTemplate.BackfillPlanHeaderIfMissing(
                filterState,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    runtimePlan.ConditionSummary));

            if (string.IsNullOrWhiteSpace(filterState.Operator))
            {
                filterState.Operator = runtimePlan.Operator;
            }

            if (string.IsNullOrWhiteSpace(filterState.ConstValueText))
            {
                filterState.ConstValueText = runtimePlan.ConstValueText;
            }

            if (string.IsNullOrWhiteSpace(filterState.ExecutionSummary))
            {
                filterState.ExecutionSummary = runtimePlan.ExecutionSummary;
            }
        }

        private static void EnrichPayload(
            SignalPayload payload,
            FlowFilterNodeState filterState)
        {
            payload["CompareValue"] = filterState.CompareValueText;
            payload["Operator"] = filterState.Operator;
            payload["ConstValue"] = filterState.ConstValueText;
            payload["ConditionMet"] = filterState.ConditionMet.ToString();
            payload["UnconditionalPass"] = filterState.WasUnconditionalPass.ToString();
            payload["RoutedPort"] = filterState.RoutedPort;
        }

        private static string BuildExecutionSummary(FlowFilterNodeState filterState)
        {
            if (filterState.WasUnconditionalPass)
            {
                return "比较值未接线，按无条件通过 -> pass";
            }

            var compareValue = string.IsNullOrWhiteSpace(filterState.CompareValueText)
                ? "<empty>"
                : filterState.CompareValueText;
            return $"{compareValue} {filterState.Operator} {filterState.ConstValueText} -> {filterState.RoutedPort}";
        }
    }
}
