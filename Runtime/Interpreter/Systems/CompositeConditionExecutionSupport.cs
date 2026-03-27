#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal static class CompositeConditionExecutionSupport
    {
        private const string CompositeConditionReactiveSlotKey = "composite-condition.reactive";
        private const string CompositeConditionSchedulingSlotKey = "composite-condition.timeout";
        private const string CompositeConditionSourceDomain = "composite-condition";

        private static readonly int[] CondPortHashes =
        {
            PortHashes.Cond0, PortHashes.Cond1, PortHashes.Cond2, PortHashes.Cond3
        };

        public static bool TryAccumulateTriggeredMask(
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            int toPortHash)
        {
            var bitIndex = GetCondBitIndex(toPortHash);
            if (bitIndex < 0)
            {
                return false;
            }

            if (NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame) == null)
            {
                return false;
            }

            var stateAccess = GraphNodeExecutionTemplate.AcquireState(
                frame,
                actionIndex,
                CompositeConditionSystem.CompositeConditionStateDescriptor,
                runtimeState.IsFirstEntry,
                ref runtimeState);
            var compositeState = stateAccess.State;

            BackfillPlanFieldsIfMissing(frame, actionIndex, compositeState);
            compositeState.TriggeredMask |= 1 << bitIndex;
            compositeState.ExecutionSummary = BuildExecutionSummary(
                compositeState.TriggeredMask,
                compositeState.ConnectedMask);
            NotifyTriggered(frame, actionIndex, bitIndex, frame.TickCount);
            return true;
        }

        public static bool TryExecute(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var compositeState = GetOrCreateRunningState(ref view, frame, actionIndex, ref runtimeState);
            var tickResult = EvaluateTick(
                frame,
                actionIndex,
                view.CurrentTick,
                compositeState);
            return GraphNodeExecutionTemplate.TryFinalizeCompletion(
                frame,
                ref view,
                actionIndex,
                ref runtimeState,
                CompositeConditionSystem.CompositeConditionStateDescriptor,
                tickResult);
        }

        public static CompositeConditionNodeState GetOrCreateRunningState(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                CompositeConditionSystem.CompositeConditionStateDescriptor,
                ref runtimeState);
            var compositeState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(ref view, frame, actionIndex, compositeState);
            }
            else
            {
                BackfillPlanFieldsIfMissing(frame, actionIndex, compositeState);
            }

            return compositeState;
        }

        public static void InitializeState(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            CompositeConditionNodeState compositeState)
        {
            var runtimePlan = CompositeConditionRuntimePlanResolver.Resolve(frame, actionIndex);
            GraphNodeExecutionTemplate.InitializeTimedState(
                compositeState,
                view.CurrentTick,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    runtimePlan.ConditionSummary),
                string.Empty);
            compositeState.Mode = runtimePlan.Mode;
            compositeState.ConnectedMask = runtimePlan.ConnectedMask;
            compositeState.ConnectedPortSummary = runtimePlan.ConnectedPortSummary;
            compositeState.ExecutionSummary = BuildExecutionSummary(
                compositeState.TriggeredMask,
                compositeState.ConnectedMask);
            compositeState.TimeoutTargetTick = BlueprintTime.ScheduleTimeoutAfterSeconds(
                view.CurrentTick,
                runtimePlan.TimeoutSeconds,
                view.TimeSettings);
            ResetAuxiliaryRefs(compositeState);
            Debug.Log($"[CompositeConditionSystem] (index={actionIndex}) 启动: " +
                      $"mode={compositeState.Mode}, connectedMask={compositeState.ConnectedMask:X}, timeoutTargetTick={compositeState.TimeoutTargetTick}");
        }

        public static void EnsureAuxiliaryState(
            BlueprintFrame frame,
            int actionIndex,
            CompositeConditionNodeState compositeState,
            int currentTick)
        {
            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return;
            }

            var ownerRef = NodePrivateExecutionStateSupport.CreateActionOwnerRef(frame, actionIndex);

            if (BlueprintTime.HasDeadline(compositeState.TimeoutTargetTick) && !compositeState.SchedulingEntryRef.IsValid)
            {
                var schedulingDomain = host.GetRequiredDomain<SchedulingStateDomain>();
                compositeState.SchedulingEntryRef = schedulingDomain.Schedule(new SchedulingEntryRequest(
                    ownerRef,
                    CompositeConditionSchedulingSlotKey,
                    SchedulingKind.Timeout,
                    StateLifetime.Execution,
                    new RuntimeTick(compositeState.StartTick),
                    new RuntimeTick(compositeState.TimeoutTargetTick),
                    PausePolicy.RespectRuntimePause));
            }

            if (compositeState.ConnectedMask != 0 && !compositeState.ReactiveWaitEntryRef.IsValid)
            {
                var reactiveDomain = host.GetRequiredDomain<ReactiveStateDomain>();
                compositeState.ReactiveWaitEntryRef = reactiveDomain.EnsureWait(new ReactiveWaitRequest(
                    ownerRef,
                    CompositeConditionReactiveSlotKey,
                    ReactiveWaitKind.CompositeCondition,
                    StateLifetime.Execution,
                    SemanticSummaryUtility.NormalizeCompositeConditionMode(compositeState.Mode) == "OR"
                        ? ResolvePolicy.Any
                        : ResolvePolicy.All));

                for (var bitIndex = 0; bitIndex < CondPortHashes.Length; bitIndex++)
                {
                    if ((compositeState.ConnectedMask & (1 << bitIndex)) == 0)
                    {
                        continue;
                    }

                    var subscriptionEntryRef = reactiveDomain.AttachSubscription(new ReactiveSubscriptionRequest(
                        compositeState.ReactiveWaitEntryRef,
                        $"cond{bitIndex}",
                        SourceKind.Condition,
                        CreateSourceRef(frame, actionIndex, bitIndex)));
                    SetSubscriptionRef(compositeState, bitIndex, subscriptionEntryRef);
                }
            }

            if (compositeState.ReactiveWaitEntryRef.IsValid
                && compositeState.SchedulingEntryRef.IsValid
                && TryGetEntryState(frame, compositeState.ReactiveWaitEntryRef, out ReactiveStateDomain.ReactiveWaitState? reactiveWaitState))
            {
                reactiveWaitState!.ScheduleEntryRef = compositeState.SchedulingEntryRef;
            }

            SynchronizeTriggeredMask(frame, actionIndex, compositeState, currentTick);
        }

        public static GraphNodeExecutionResult EvaluateTick(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            CompositeConditionNodeState compositeState)
        {
            if (IsSatisfied(compositeState.Mode, compositeState.ConnectedMask, compositeState.TriggeredMask))
            {
                var completionResult = GraphNodeExecutionTemplate.CreateCompletionResult(
                    GraphNodeExecutionResultKind.Completed,
                    "Signal.CompositeCondition.Satisfied",
                    ActionPortIds.SignalCompositeCondition.Out);
                Debug.Log($"[CompositeConditionSystem] (index={actionIndex}) 条件满足！" +
                          $"mode={compositeState.Mode}, triggered={compositeState.TriggeredMask:X}, connected={compositeState.ConnectedMask:X}");
                ReleaseAuxiliaryState(frame, compositeState);
                RecordCompletionEvent(
                    frame,
                    actionIndex,
                    currentTick,
                    compositeState,
                    completionResult);
                return completionResult;
            }

            if (HasTimedOut(frame, currentTick, compositeState))
            {
                var completionResult = GraphNodeExecutionTemplate.CreateCompletionResult(
                    GraphNodeExecutionResultKind.TimedOut,
                    "Signal.CompositeCondition.Timeout",
                    ActionPortIds.SignalCompositeCondition.OnTimeout);
                Debug.Log($"[CompositeConditionSystem] (index={actionIndex}) 超时！");
                ReleaseAuxiliaryState(frame, compositeState);
                RecordCompletionEvent(
                    frame,
                    actionIndex,
                    currentTick,
                    compositeState,
                    completionResult);
                return completionResult;
            }

            return GraphNodeExecutionTemplate.RunningResult;
        }

        public static void ReleaseAuxiliaryState(BlueprintFrame frame, CompositeConditionNodeState compositeState)
        {
            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host != null)
            {
                if (compositeState.SchedulingEntryRef.IsValid)
                {
                    host.GetRequiredDomain<SchedulingStateDomain>().ReleaseEntry(compositeState.SchedulingEntryRef);
                }

                var reactiveDomain = host.GetRequiredDomain<ReactiveStateDomain>();
                if (compositeState.ReactiveWaitEntryRef.IsValid)
                {
                    reactiveDomain.ReleaseEntry(compositeState.ReactiveWaitEntryRef);
                }
                else
                {
                    for (var bitIndex = 0; bitIndex < CondPortHashes.Length; bitIndex++)
                    {
                        var subscriptionEntryRef = GetSubscriptionRef(compositeState, bitIndex);
                        if (subscriptionEntryRef.IsValid)
                        {
                            reactiveDomain.ReleaseEntry(subscriptionEntryRef);
                        }
                    }
                }
            }

            ResetAuxiliaryRefs(compositeState);
        }

        public static bool TryGetCompositeConditionState(
            BlueprintFrame frame,
            int actionIndex,
            out CompositeConditionNodeState? compositeState)
        {
            var entryRef = NodePrivateExecutionStateSupport.TryGetEntryRef(
                frame,
                actionIndex,
                CompositeConditionSystem.CompositeConditionStateDescriptor);
            if (!entryRef.HasValue)
            {
                compositeState = null;
                return false;
            }

            return TryGetEntryState(frame, entryRef.Value, out compositeState);
        }

        private static void BackfillPlanFieldsIfMissing(
            BlueprintFrame frame,
            int actionIndex,
            CompositeConditionNodeState compositeState)
        {
            if (compositeState.ConnectedMask != 0
                && !string.IsNullOrWhiteSpace(compositeState.PlanSource)
                && !string.IsNullOrWhiteSpace(compositeState.Mode)
                && !string.IsNullOrWhiteSpace(compositeState.PlanSummary)
                && !string.IsNullOrWhiteSpace(compositeState.ConnectedPortSummary)
                && !string.IsNullOrWhiteSpace(compositeState.ConditionSummary))
            {
                return;
            }

            var runtimePlan = CompositeConditionRuntimePlanResolver.Resolve(frame, actionIndex);
            GraphNodeExecutionTemplate.BackfillPlanHeaderIfMissing(
                compositeState,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimePlan.PlanSource,
                    runtimePlan.PlanSummary,
                    runtimePlan.ConditionSummary));

            if (string.IsNullOrWhiteSpace(compositeState.Mode))
            {
                compositeState.Mode = runtimePlan.Mode;
            }

            if (compositeState.ConnectedMask == 0)
            {
                compositeState.ConnectedMask = runtimePlan.ConnectedMask;
            }

            if (string.IsNullOrWhiteSpace(compositeState.ConnectedPortSummary))
            {
                compositeState.ConnectedPortSummary = runtimePlan.ConnectedPortSummary;
            }

        }

        private static bool IsSatisfied(string mode, int connectedMask, int triggeredMask)
        {
            if (connectedMask == 0)
            {
                return false;
            }

            return SemanticSummaryUtility.NormalizeCompositeConditionMode(mode) == "OR"
                ? (triggeredMask & connectedMask) != 0
                : (triggeredMask & connectedMask) == connectedMask;
        }

        private static string BuildExecutionSummary(int triggeredMask, int connectedMask)
        {
            var connectedCount = CountConnectedConditions(connectedMask);
            var triggeredCount = CountConnectedConditions(triggeredMask);
            return $"已触发 {triggeredCount}/{connectedCount} 条条件";
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

        private static void SynchronizeTriggeredMask(
            BlueprintFrame frame,
            int actionIndex,
            CompositeConditionNodeState compositeState,
            int currentTick)
        {
            if (compositeState.TriggeredMask == 0)
            {
                return;
            }

            for (var bitIndex = 0; bitIndex < CondPortHashes.Length; bitIndex++)
            {
                if ((compositeState.TriggeredMask & (1 << bitIndex)) == 0)
                {
                    continue;
                }

                if (!TryGetEntryState(
                        frame,
                        GetSubscriptionRef(compositeState, bitIndex),
                        out ReactiveStateDomain.ReactiveSubscriptionState? subscriptionState)
                    || subscriptionState!.Status == ReactiveStateDomain.ReactiveSubscriptionStatus.Matched)
                {
                    continue;
                }

                NotifyTriggered(frame, actionIndex, bitIndex, currentTick);
            }
        }

        private static void NotifyTriggered(BlueprintFrame frame, int actionIndex, int bitIndex, int currentTick)
        {
            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return;
            }

            var sourceRef = CreateSourceRef(frame, actionIndex, bitIndex);
            host.GetRequiredDomain<ReactiveStateDomain>().NotifySource(new ReactiveSourceNotification(
                sourceRef,
                new MatchTokenRef(
                    sourceRef,
                    TokenKind.ConditionOccurrence,
                    $"{currentTick}:{actionIndex}:cond{bitIndex}")));
        }

        private static bool HasTimedOut(
            BlueprintFrame frame,
            int currentTick,
            CompositeConditionNodeState compositeState)
        {
            if (TryGetEntryState(frame, compositeState.SchedulingEntryRef, out SchedulingStateDomain.SchedulingEntryState? schedulingState))
            {
                return schedulingState!.Status == SchedulingStateDomain.SchedulingEntryStatus.Scheduled
                    && currentTick >= schedulingState.TargetTick.Value;
            }

            return BlueprintTime.HasReached(currentTick, compositeState.TimeoutTargetTick);
        }

        private static void RecordCompletionEvent(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            CompositeConditionNodeState compositeState,
            GraphNodeExecutionResult completionResult)
        {
            GraphNodeExecutionTemplate.RecordCompletionEvent(
                frame,
                actionIndex,
                currentTick,
                compositeState,
                GraphNodeExecutionTemplate.CreateCompletionContract(completionResult),
                payload =>
                {
                    payload["Mode"] = compositeState.Mode;
                    payload["ConnectedMask"] = compositeState.ConnectedMask.ToString();
                    payload["TriggeredMask"] = compositeState.TriggeredMask.ToString();
                    payload["ConnectedPortSummary"] = compositeState.ConnectedPortSummary;

                    if (BlueprintTime.HasDeadline(compositeState.TimeoutTargetTick))
                    {
                        payload["TimeoutTargetTick"] = compositeState.TimeoutTargetTick.ToString();
                    }
                });
        }

        private static void ResetAuxiliaryRefs(CompositeConditionNodeState compositeState)
        {
            compositeState.ReactiveWaitEntryRef = RuntimeEntryRef.Invalid;
            compositeState.ReactiveCond0SubscriptionEntryRef = RuntimeEntryRef.Invalid;
            compositeState.ReactiveCond1SubscriptionEntryRef = RuntimeEntryRef.Invalid;
            compositeState.ReactiveCond2SubscriptionEntryRef = RuntimeEntryRef.Invalid;
            compositeState.ReactiveCond3SubscriptionEntryRef = RuntimeEntryRef.Invalid;
            compositeState.SchedulingEntryRef = RuntimeEntryRef.Invalid;
        }

        private static RuntimeEntryRef GetSubscriptionRef(CompositeConditionNodeState compositeState, int bitIndex)
        {
            return bitIndex switch
            {
                0 => compositeState.ReactiveCond0SubscriptionEntryRef,
                1 => compositeState.ReactiveCond1SubscriptionEntryRef,
                2 => compositeState.ReactiveCond2SubscriptionEntryRef,
                3 => compositeState.ReactiveCond3SubscriptionEntryRef,
                _ => RuntimeEntryRef.Invalid,
            };
        }

        private static void SetSubscriptionRef(
            CompositeConditionNodeState compositeState,
            int bitIndex,
            RuntimeEntryRef entryRef)
        {
            switch (bitIndex)
            {
                case 0:
                    compositeState.ReactiveCond0SubscriptionEntryRef = entryRef;
                    break;
                case 1:
                    compositeState.ReactiveCond1SubscriptionEntryRef = entryRef;
                    break;
                case 2:
                    compositeState.ReactiveCond2SubscriptionEntryRef = entryRef;
                    break;
                case 3:
                    compositeState.ReactiveCond3SubscriptionEntryRef = entryRef;
                    break;
            }
        }

        private static SourceRef CreateSourceRef(BlueprintFrame frame, int actionIndex, int bitIndex)
        {
            return new SourceRef(
                CompositeConditionSourceDomain,
                SourceKind.Condition,
                $"{frame.Actions[actionIndex].Id}:cond{bitIndex}");
        }

        private static int GetCondBitIndex(int toPortHash)
        {
            for (var bitIndex = 0; bitIndex < CondPortHashes.Length; bitIndex++)
            {
                if (toPortHash == CondPortHashes[bitIndex])
                {
                    return bitIndex;
                }
            }

            return -1;
        }

        private static bool TryGetEntryState<TState>(
            BlueprintFrame frame,
            RuntimeEntryRef entryRef,
            out TState? state)
            where TState : class
        {
            state = null;

            if (!entryRef.IsValid)
            {
                return false;
            }

            var backend = frame.Runner?.GetService<ObjectStateBackend>();
            if (backend == null
                || !backend.TryGetEntry(entryRef, out var entry)
                || entry?.State is not TState typedState)
            {
                return false;
            }

            state = typedState;
            return true;
        }
    }
}
