#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal static class FlowDelayExecutionSupport
    {
        private const string FlowDelayStartedEventKind = "Flow.Delay.Started";
        private const string FlowDelayCompletedEventKind = "Flow.Delay.Completed";

        public static bool TryExecute(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var delayState = GetOrCreateRunningState(
                ref view,
                frame,
                actionIndex,
                ref runtimeState,
                out var delayConfig);

            if (!BlueprintTime.HasReached(view.CurrentTick, delayState.TargetTick))
            {
                return false;
            }

            delayState.ExecutionSummary = BuildCompletedSummary(delayState, view.CurrentTick);
            var completionContract = GraphNodeExecutionTemplate.CreateCompletionContract(
                GraphNodeExecutionResultKind.Completed,
                FlowDelayCompletedEventKind,
                ActionPortIds.FlowDelay.Out);
            GraphNodeExecutionTemplate.RecordCompletionEvent(
                frame,
                actionIndex,
                view.CurrentTick,
                delayState,
                completionContract,
                payload =>
                {
                    payload["TargetTick"] = delayState.TargetTick.ToString();
                    payload["DelaySeconds"] = delayConfig.EffectiveDelaySeconds.ToString("0.###");
                    payload["RawDelaySeconds"] = delayConfig.RawDelaySeconds.ToString("0.###");
                });

            if (!GraphNodeExecutionTemplate.TryFinalizeCompletion(
                    frame,
                    ref view,
                    actionIndex,
                    ref runtimeState,
                    FlowSystem.DelayStateDescriptor,
                    completionContract))
            {
                return false;
            }

            Debug.Log($"[FlowSystem] Flow.Delay (index={actionIndex}) -> Completed (waited {delayState.GetElapsedTicks(view.CurrentTick)} ticks)");
            return true;
        }

        public static TimedNodeState GetOrCreateRunningState(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            out FlowDelayRuntimeConfig delayConfig)
        {
            delayConfig = FlowDelayRuntimeConfigResolver.Resolve(frame, actionIndex);
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                FlowSystem.DelayStateDescriptor,
                ref runtimeState);
            var delayState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(delayState, delayConfig, view.CurrentTick, view.TimeSettings);
                RecordStartedEvent(frame, actionIndex, view.CurrentTick, delayState, delayConfig);
                Debug.Log(
                    $"[FlowSystem] Flow.Delay (index={actionIndex}) 开始等待 targetTick={delayState.TargetTick}, {delayConfig.Summary}");
            }

            return delayState;
        }

        public static void InitializeState(
            TimedNodeState delayState,
            FlowDelayRuntimeConfig delayConfig,
            int currentTick,
            BlueprintTimeSettings timeSettings)
        {
            var targetTick = BlueprintTime.ScheduleAfterSeconds(
                currentTick,
                delayConfig.EffectiveDelaySeconds,
                timeSettings,
                1);
            GraphNodeExecutionTemplate.InitializeTimedState(
                delayState,
                currentTick,
                targetTick,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    delayConfig.PlanSource,
                    delayConfig.PlanSummary,
                    delayConfig.ConditionSummary),
                string.Empty);
            delayState.ExecutionSummary = BuildWaitingSummary(delayState, delayConfig);
        }

        private static void RecordStartedEvent(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            TimedNodeState delayState,
            FlowDelayRuntimeConfig delayConfig)
        {
            GraphNodeExecutionTemplate.RecordTimedEvent(
                frame,
                actionIndex,
                currentTick,
                delayState,
                GraphNodeExecutionTemplate.CreateEventContract(FlowDelayStartedEventKind),
                payload =>
                {
                    payload["TargetTick"] = delayState.TargetTick.ToString();
                    payload["DelaySeconds"] = delayConfig.EffectiveDelaySeconds.ToString("0.###");
                    payload["RawDelaySeconds"] = delayConfig.RawDelaySeconds.ToString("0.###");
                });
        }

        private static string BuildWaitingSummary(
            TimedNodeState delayState,
            FlowDelayRuntimeConfig delayConfig)
        {
            return $"等待至 T={delayState.TargetTick} | {delayConfig.Summary}";
        }

        private static string BuildCompletedSummary(
            TimedNodeState delayState,
            int currentTick)
        {
            return $"延迟完成 | 已等待 {delayState.GetElapsedTicks(currentTick)} ticks";
        }
    }
}
