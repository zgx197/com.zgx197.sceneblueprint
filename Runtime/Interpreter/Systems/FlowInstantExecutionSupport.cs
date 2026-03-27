#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.State;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal readonly struct FlowInstantNodeRuntimePlan
    {
        public FlowInstantNodeRuntimePlan(
            string planSource,
            string planSummary,
            string executionSummary,
            string eventKind,
            string eventValue,
            bool isTerminal,
            string outputPortId)
        {
            PlanSource = planSource ?? string.Empty;
            PlanSummary = planSummary ?? string.Empty;
            ExecutionSummary = executionSummary ?? string.Empty;
            EventKind = eventKind ?? string.Empty;
            EventValue = eventValue ?? string.Empty;
            IsTerminal = isTerminal;
            OutputPortId = outputPortId ?? string.Empty;
        }

        public string PlanSource { get; }

        public string PlanSummary { get; }

        public string ExecutionSummary { get; }

        public string EventKind { get; }

        public string EventValue { get; }

        public bool IsTerminal { get; }

        public string OutputPortId { get; }
    }

    internal static class FlowInstantExecutionSupport
    {
        public static bool TryExecute(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            NodeStateDescriptor<InstantEventNodeState> descriptor,
            FlowInstantNodeRuntimePlan runtimePlan,
            bool markBlueprintCompleted = false)
        {
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                descriptor,
                ref runtimeState);
            var eventState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(eventState, runtimePlan, view.CurrentTick);
            }

            var completionResult = GraphNodeExecutionTemplate.CreateCompletionResult(
                GraphNodeExecutionResultKind.Completed,
                string.Empty,
                runtimePlan.OutputPortId);

            if (!GraphNodeExecutionTemplate.TryFinalizeCompletion(
                    frame,
                    ref view,
                    actionIndex,
                    ref runtimeState,
                    descriptor,
                    completionResult))
            {
                return false;
            }

            if (markBlueprintCompleted)
            {
                view.Query.IsCompleted = true;
            }

            Debug.Log(BuildCompletedLogMessage(actionIndex, eventState, view.CurrentTick, markBlueprintCompleted));
            return true;
        }

        public static void InitializeState(
            InstantEventNodeState eventState,
            FlowInstantNodeRuntimePlan runtimePlan,
            int currentTick)
        {
            GraphNodeExecutionTemplate.InitializeInstantState(
                eventState,
                currentTick,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    string.Empty),
                runtimePlan.ExecutionSummary,
                runtimePlan.EventKind,
                runtimePlan.EventValue,
                runtimePlan.IsTerminal);
        }

        private static string BuildCompletedLogMessage(
            int actionIndex,
            InstantEventNodeState eventState,
            int currentTick,
            bool blueprintCompleted)
        {
            var suffix = blueprintCompleted ? "，蓝图执行结束" : string.Empty;
            return $"[FlowSystem] {eventState.EventKind} (index={actionIndex}) -> Completed{suffix} " +
                   $"(event={eventState.EventKind}, value={eventState.EventValue}, waited {eventState.GetElapsedTicks(currentTick)} ticks)";
        }
    }
}
