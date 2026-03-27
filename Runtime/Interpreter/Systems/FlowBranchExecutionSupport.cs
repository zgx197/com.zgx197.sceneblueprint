#nullable enable
using SceneBlueprint.Contract;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal static class FlowBranchExecutionSupport
    {
        public static bool TryExecute(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var branchState = GetOrCreateRunningState(
                ref view,
                frame,
                actionIndex,
                ref runtimeState);
            var completionContract = BuildCompletionContract(branchState);
            RecordCompletionEvent(
                frame,
                actionIndex,
                view.CurrentTick,
                branchState,
                completionContract);

            if (!GraphNodeExecutionTemplate.TryFinalizeCompletion(
                    frame,
                    ref view,
                    actionIndex,
                    ref runtimeState,
                    FlowSystem.BranchStateDescriptor,
                    completionContract))
            {
                return false;
            }

            Debug.Log(BuildCompletedLogMessage(actionIndex, branchState, view.CurrentTick));
            return true;
        }

        public static BranchNodeState GetOrCreateRunningState(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                FlowSystem.BranchStateDescriptor,
                ref runtimeState);
            var branchState = stateAccess.State;
            var runtimePlan = FlowBranchRuntimePlanResolver.Resolve(frame, actionIndex);

            if (stateAccess.CreatedFresh)
            {
                InitializeState(branchState, runtimePlan, view.CurrentTick);
            }
            else
            {
                BackfillStateIfMissing(branchState, runtimePlan);
            }

            return branchState;
        }

        public static void InitializeState(
            BranchNodeState branchState,
            FlowBranchRuntimePlan runtimePlan,
            int currentTick)
        {
            GraphNodeExecutionTemplate.InitializeTimedState(
                branchState,
                currentTick,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    runtimePlan.ConditionSummary),
                runtimePlan.ExecutionSummary);
            branchState.ConditionResult = runtimePlan.ConditionValue;
            branchState.RoutedPort = runtimePlan.RoutedPort;
        }

        public static GraphNodeCompletionContract BuildCompletionContract(BranchNodeState branchState)
        {
            return GraphNodeExecutionTemplate.CreateCompletionContract(
                GraphNodeExecutionResultKind.Completed,
                GetEventKind(branchState.ConditionResult),
                branchState.RoutedPort);
        }

        public static void RecordCompletionEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            BranchNodeState branchState,
            GraphNodeCompletionContract completionContract)
        {
            GraphNodeExecutionTemplate.RecordCompletionEvent(
                frame,
                actionIndex,
                currentTick,
                branchState,
                completionContract,
                payload =>
                {
                    payload["ConditionResult"] = branchState.ConditionResult.ToString();
                    payload["RoutedPort"] = branchState.RoutedPort;
                });
        }

        public static string BuildCompletedLogMessage(int actionIndex, BranchNodeState branchState, int currentTick)
        {
            return $"[FlowSystem] Flow.Branch (index={actionIndex}) → Completed, route={branchState.RoutedPort}, " +
                   $"waited {branchState.GetElapsedTicks(currentTick)} ticks";
        }

        private static void BackfillStateIfMissing(
            BranchNodeState branchState,
            FlowBranchRuntimePlan runtimePlan)
        {
            GraphNodeExecutionTemplate.BackfillPlanHeaderIfMissing(
                branchState,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    runtimePlan.ConditionSummary));

            if (string.IsNullOrWhiteSpace(branchState.RoutedPort))
            {
                branchState.RoutedPort = runtimePlan.RoutedPort;
            }

            if (string.IsNullOrWhiteSpace(branchState.ExecutionSummary))
            {
                branchState.ExecutionSummary = runtimePlan.ExecutionSummary;
            }
        }

        private static string GetEventKind(bool conditionValue)
        {
            return conditionValue
                ? "Flow.Branch.True"
                : "Flow.Branch.False";
        }
    }
}
