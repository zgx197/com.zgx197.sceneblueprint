#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal static class FlowJoinExecutionSupport
    {
        private const string FlowJoinCompletedEventKind = "Flow.Join.Completed";

        public static bool TryAccumulateArrival(
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            int currentTick,
            int fallbackRequiredCount,
            out JoinNodeState? joinState)
        {
            joinState = null;

            if (NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame) == null)
            {
                return false;
            }

            var runtimePlan = FlowJoinRuntimePlanResolver.Resolve(frame, actionIndex, fallbackRequiredCount);
            var stateAccess = GraphNodeExecutionTemplate.AcquireState(
                frame,
                actionIndex,
                FlowSystem.JoinStateDescriptor,
                runtimeState.Phase == ActionPhase.Idle,
                ref runtimeState);
            joinState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(joinState, runtimePlan, currentTick, initialReceivedCount: 0);
            }
            else
            {
                BackfillStateIfMissing(joinState, runtimePlan);
            }

            joinState.ReceivedCount++;
            joinState.ExecutionSummary = BuildExecutionSummary(joinState.ReceivedCount, joinState.RequiredCount);
            return true;
        }

        public static bool TryExecute(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var joinState = GetOrCreateRunningState(
                ref view,
                frame,
                actionIndex,
                ref runtimeState);
            RecordCompletedEvent(frame, actionIndex, view.CurrentTick, joinState);

            if (!GraphNodeExecutionTemplate.TryFinalizeCompletion(
                    frame,
                    ref view,
                    actionIndex,
                    ref runtimeState,
                    FlowSystem.JoinStateDescriptor,
                    BuildCompletionContract()))
            {
                return false;
            }

            Debug.Log(BuildCompletedLogMessage(actionIndex, joinState, view.CurrentTick));
            return true;
        }

        public static JoinNodeState GetOrCreateRunningState(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var runtimePlan = FlowJoinRuntimePlanResolver.Resolve(frame, actionIndex, 1);
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                FlowSystem.JoinStateDescriptor,
                ref runtimeState);
            var joinState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(
                    joinState,
                    runtimePlan,
                    view.CurrentTick,
                    initialReceivedCount: runtimePlan.RequiredCount);
            }
            else
            {
                BackfillStateIfMissing(joinState, runtimePlan);
                if (joinState.RequiredCount <= 0)
                {
                    joinState.RequiredCount = runtimePlan.RequiredCount;
                }

                if (joinState.ReceivedCount <= 0)
                {
                    joinState.ReceivedCount = joinState.RequiredCount;
                }

                joinState.ExecutionSummary = BuildExecutionSummary(joinState.ReceivedCount, joinState.RequiredCount);
            }

            return joinState;
        }

        public static GraphNodeCompletionContract BuildCompletionContract()
        {
            return GraphNodeExecutionTemplate.CreateCompletionContract(
                GraphNodeExecutionResultKind.Completed,
                FlowJoinCompletedEventKind,
                ActionPortIds.FlowJoin.Out);
        }

        public static void RecordCompletedEvent(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            JoinNodeState joinState)
        {
            GraphNodeExecutionTemplate.RecordCompletionEvent(
                frame,
                actionIndex,
                currentTick,
                joinState,
                BuildCompletionContract(),
                payload =>
                {
                    payload["RequiredCount"] = joinState.RequiredCount.ToString();
                    payload["ReceivedCount"] = joinState.ReceivedCount.ToString();
                    payload["IncomingActionSummary"] = joinState.IncomingActionSummary;
                });
        }

        public static string BuildCompletedLogMessage(int actionIndex, JoinNodeState joinState, int currentTick)
        {
            return $"[FlowSystem] Flow.Join (index={actionIndex}) → Completed " +
                   $"({joinState.ReceivedCount}/{joinState.RequiredCount}, waited {joinState.GetElapsedTicks(currentTick)} ticks)";
        }

        private static void InitializeState(
            JoinNodeState joinState,
            FlowJoinRuntimePlan runtimePlan,
            int currentTick,
            int initialReceivedCount)
        {
            GraphNodeExecutionTemplate.InitializeTimedState(
                joinState,
                currentTick,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    runtimePlan.ConditionSummary),
                string.Empty);
            joinState.RequiredCount = runtimePlan.RequiredCount;
            joinState.ReceivedCount = initialReceivedCount;
            joinState.IncomingActionSummary = runtimePlan.IncomingActionSummary;
            joinState.ExecutionSummary = BuildExecutionSummary(joinState.ReceivedCount, joinState.RequiredCount);
        }

        private static void BackfillStateIfMissing(
            JoinNodeState joinState,
            FlowJoinRuntimePlan runtimePlan)
        {
            GraphNodeExecutionTemplate.BackfillPlanHeaderIfMissing(
                joinState,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    runtimePlan.ConditionSummary));

            if (joinState.RequiredCount <= 0)
            {
                joinState.RequiredCount = runtimePlan.RequiredCount;
            }

            if (string.IsNullOrWhiteSpace(joinState.IncomingActionSummary))
            {
                joinState.IncomingActionSummary = runtimePlan.IncomingActionSummary;
            }

            if (string.IsNullOrWhiteSpace(joinState.ExecutionSummary))
            {
                joinState.ExecutionSummary = BuildExecutionSummary(joinState.ReceivedCount, joinState.RequiredCount);
            }
        }

        private static string BuildExecutionSummary(int receivedCount, int requiredCount)
        {
            return $"已到达 {System.Math.Max(0, receivedCount)}/{System.Math.Max(1, requiredCount)} 输入";
        }
    }
}
