#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal enum WatchConditionTickResultKind
    {
        Running = 0,
        Rearmed = 1,
        Completed = 2,
        TimedOut = 3,
    }

    internal readonly struct WatchConditionTickResult
    {
        public WatchConditionTickResult(
            WatchConditionTickResultKind kind,
            string outputPortId)
        {
            Kind = kind;
            OutputPortId = outputPortId ?? string.Empty;
        }

        public WatchConditionTickResultKind Kind { get; }

        public string OutputPortId { get; }

        public bool ShouldEmitFlow => Kind != WatchConditionTickResultKind.Running;

        public bool ShouldComplete =>
            Kind == WatchConditionTickResultKind.Completed
            || Kind == WatchConditionTickResultKind.TimedOut;

        public bool ShouldResetEventEmitted => Kind == WatchConditionTickResultKind.Rearmed;
    }

    internal static class WatchConditionExecutionSupport
    {
        private const string ReactiveSlotKey = "watch-condition.reactive";
        private const string SchedulingSlotKey = "watch-condition.timeout";
        private const string SourceDomain = "watch-condition";

        public static WatchConditionTickResult RunningResult { get; } =
            new(WatchConditionTickResultKind.Running, string.Empty);

        public static WatchConditionTickResult RearmedResult { get; } =
            new(WatchConditionTickResultKind.Rearmed, ActionPortIds.SignalWatchCondition.OnTriggered);

        public static WatchConditionTickResult CompletedResult { get; } =
            new(WatchConditionTickResultKind.Completed, ActionPortIds.SignalWatchCondition.OnTriggered);

        public static WatchConditionTickResult TimedOutResult { get; } =
            new(WatchConditionTickResultKind.TimedOut, ActionPortIds.SignalWatchCondition.OnTimeout);

        public static bool TryExecute(
            BlueprintFrame frame,
            ref FrameView view,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                SignalSystem.WatchConditionStateDescriptor,
                ref runtimeState);
            var watchState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(frame, ref view, actionIndex, watchState);
            }

            var tickResult = EvaluateTick(frame, ref view, actionIndex, watchState);
            if (!tickResult.ShouldEmitFlow)
            {
                return false;
            }

            if (!tickResult.ShouldComplete)
            {
                GraphNodeExecutionTemplate.EmitFlowPort(
                    ref view,
                    actionIndex,
                    ref runtimeState,
                    tickResult.OutputPortId,
                    tickResult.ShouldResetEventEmitted);
                return false;
            }

            var completionResult = CreateCompletionResult(tickResult);
            return completionResult.HasValue
                && GraphNodeExecutionTemplate.TryFinalizeCompletion(
                    frame,
                    ref view,
                    actionIndex,
                    ref runtimeState,
                    SignalSystem.WatchConditionStateDescriptor,
                    completionResult.Value);
        }

        public static void InitializeState(
            BlueprintFrame frame,
            ref FrameView view,
            int actionIndex,
            WatchConditionNodeState watchState)
        {
            var runtimeConfig = SignalActionRuntimeConfigResolver.ResolveWatchCondition(frame, actionIndex);
            var registration = SignalExecutionSupportUtility.CreateConditionWatchRegistration(actionIndex, runtimeConfig);
            GraphNodeExecutionTemplate.InitializeTimedState(
                watchState,
                view.CurrentTick,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimeConfig.PlanSource,
                    runtimeConfig.PlanSummary,
                    runtimeConfig.ConditionSummary),
                string.Empty);
            watchState.ConditionType = registration.ConditionType;
            watchState.TargetRefSerialized = runtimeConfig.TargetRefSerialized;
            watchState.TargetSummary = runtimeConfig.TargetSummary;
            watchState.ParametersRaw = runtimeConfig.ParametersRaw;
            watchState.ExecutionSummary = BuildWaitingSummary(runtimeConfig);
            watchState.TimeoutSeconds = runtimeConfig.TimeoutSeconds;
            watchState.Repeat = runtimeConfig.Repeat;
            watchState.Descriptor = registration.Descriptor;
            watchState.WatchHandle = registration.Handle;
            watchState.TimeoutTargetTick = BlueprintTime.ScheduleTimeoutAfterSeconds(
                view.CurrentTick,
                runtimeConfig.TimeoutSeconds,
                view.TimeSettings);
            ResetAuxiliaryRefs(watchState);
            EnsureAuxiliaryState(frame, actionIndex, watchState);

            if (string.IsNullOrEmpty(watchState.ConditionType))
            {
                watchState.ExecutionSummary = "未配置 conditionType";
                Debug.LogWarning($"[SignalSystem] WatchCondition (index={actionIndex}) 未配置 conditionType");
                return;
            }

            watchState.WatchHandle = SignalExecutionSupportUtility.BeginConditionWatch(view.Bus, registration);
            if (!watchState.WatchHandle.IsValid)
            {
                watchState.WatchHandle = registration.Handle;
            }
            Debug.Log(
                $"[SignalSystem] WatchCondition (index={actionIndex}) 已创建监听: handle={watchState.WatchHandle}, " +
                $"target={watchState.TargetSummary}, timeoutTargetTick={watchState.TimeoutTargetTick}");
        }

        public static WatchConditionTickResult EvaluateTick(
            BlueprintFrame frame,
            ref FrameView view,
            int actionIndex,
            WatchConditionNodeState watchState)
        {
            EnsureAuxiliaryState(frame, actionIndex, watchState);

            var bus = view.Bus;
            var watchHandle = EnsureWatchHandle(actionIndex, watchState);
            if (bus != null && bus.IsConditionTriggered(watchHandle))
            {
                NotifyResolved(frame, actionIndex, view.CurrentTick, watchState);
                Debug.Log($"[SignalSystem] WatchCondition (index={actionIndex}) 条件已满足！repeat={watchState.Repeat}");

                EndWatch(bus, watchState);

                if (watchState.Repeat)
                {
                    ReleaseReactiveState(frame, watchState);
                    watchState.ExecutionSummary = BuildTriggeredSummary(watchState, isRearmed: true);
                    RecordIntermediateEvent(
                        frame,
                        actionIndex,
                        view.CurrentTick,
                        watchState,
                        RearmedResult.OutputPortId,
                        "Signal.WatchCondition.Triggered",
                        watchState.Repeat);
                    var rearmedRegistration = CreateRegistration(actionIndex, watchState);
                    watchState.WatchHandle = SignalExecutionSupportUtility.BeginConditionWatch(
                        bus,
                        rearmedRegistration);
                    if (!watchState.WatchHandle.IsValid)
                    {
                        watchState.WatchHandle = rearmedRegistration.Handle;
                    }
                    EnsureAuxiliaryState(frame, actionIndex, watchState);
                    return RearmedResult;
                }

                var completionResult = CreateCompletionResult(CompletedResult);
                watchState.ExecutionSummary = BuildTriggeredSummary(watchState, isRearmed: false);
                ReleaseAuxiliaryState(frame, watchState);
                RecordCompletionEvent(
                    frame,
                    actionIndex,
                    view.CurrentTick,
                    watchState,
                    completionResult!.Value,
                    watchState.Repeat);
                return CompletedResult;
            }

            if (HasTimedOut(frame, view.CurrentTick, watchState))
            {
                Debug.Log($"[SignalSystem] WatchCondition (index={actionIndex}) 超时！");
                EndWatch(bus, watchState);
                var completionResult = CreateCompletionResult(TimedOutResult);
                watchState.ExecutionSummary = BuildTimeoutSummary(watchState);
                ReleaseAuxiliaryState(frame, watchState);
                RecordCompletionEvent(
                    frame,
                    actionIndex,
                    view.CurrentTick,
                    watchState,
                    completionResult!.Value,
                    watchState.Repeat);
                return TimedOutResult;
            }

            return RunningResult;
        }

        public static void ReleaseReactiveState(BlueprintFrame frame, WatchConditionNodeState watchState)
        {
            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host != null)
            {
                var reactiveDomain = host.GetRequiredDomain<ReactiveStateDomain>();
                if (watchState.ReactiveWaitEntryRef.IsValid)
                {
                    reactiveDomain.ReleaseEntry(watchState.ReactiveWaitEntryRef);
                }
                else if (watchState.ReactiveSubscriptionEntryRef.IsValid)
                {
                    reactiveDomain.ReleaseEntry(watchState.ReactiveSubscriptionEntryRef);
                }
            }

            watchState.ReactiveWaitEntryRef = RuntimeEntryRef.Invalid;
            watchState.ReactiveSubscriptionEntryRef = RuntimeEntryRef.Invalid;
        }

        public static void ReleaseAuxiliaryState(BlueprintFrame frame, WatchConditionNodeState watchState)
        {
            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host != null)
            {
                if (watchState.SchedulingEntryRef.IsValid)
                {
                    host.GetRequiredDomain<SchedulingStateDomain>().ReleaseEntry(watchState.SchedulingEntryRef);
                }

                var reactiveDomain = host.GetRequiredDomain<ReactiveStateDomain>();
                if (watchState.ReactiveWaitEntryRef.IsValid)
                {
                    reactiveDomain.ReleaseEntry(watchState.ReactiveWaitEntryRef);
                }
                else if (watchState.ReactiveSubscriptionEntryRef.IsValid)
                {
                    reactiveDomain.ReleaseEntry(watchState.ReactiveSubscriptionEntryRef);
                }
            }

            ResetAuxiliaryRefs(watchState);
        }

        public static void EndWatch(ISignalBus? bus, WatchConditionNodeState watchState)
        {
            SignalExecutionSupportUtility.EndConditionWatch(bus, watchState.WatchHandle);
            watchState.WatchHandle = default;
        }

        public static bool TryGetWatchConditionState(
            BlueprintFrame frame,
            int actionIndex,
            out WatchConditionNodeState? watchState)
        {
            return SignalExecutionSupportUtility.TryGetState(
                frame,
                actionIndex,
                SignalSystem.WatchConditionStateDescriptor,
                out watchState);
        }

        private static void EnsureAuxiliaryState(
            BlueprintFrame frame,
            int actionIndex,
            WatchConditionNodeState watchState)
        {
            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return;
            }

            var ownerRef = NodePrivateExecutionStateSupport.CreateActionOwnerRef(frame, actionIndex);

            if (BlueprintTime.HasDeadline(watchState.TimeoutTargetTick) && !watchState.SchedulingEntryRef.IsValid)
            {
                var schedulingDomain = host.GetRequiredDomain<SchedulingStateDomain>();
                watchState.SchedulingEntryRef = schedulingDomain.Schedule(new SchedulingEntryRequest(
                    ownerRef,
                    SchedulingSlotKey,
                    SchedulingKind.Timeout,
                    StateLifetime.Execution,
                    new RuntimeTick(watchState.StartTick),
                    new RuntimeTick(watchState.TimeoutTargetTick),
                    PausePolicy.RespectRuntimePause));
            }

            if (!string.IsNullOrEmpty(watchState.ConditionType) && !watchState.ReactiveWaitEntryRef.IsValid)
            {
                var reactiveDomain = host.GetRequiredDomain<ReactiveStateDomain>();
                var waitEntryRef = reactiveDomain.EnsureWait(new ReactiveWaitRequest(
                    ownerRef,
                    ReactiveSlotKey,
                    ReactiveWaitKind.Condition,
                    StateLifetime.Execution,
                    ResolvePolicy.Any));
                var subscriptionEntryRef = reactiveDomain.AttachSubscription(new ReactiveSubscriptionRequest(
                    waitEntryRef,
                    watchState.ConditionType,
                    SourceKind.Condition,
                    CreateSourceRef(frame, actionIndex, watchState.ConditionType)));

                watchState.ReactiveWaitEntryRef = waitEntryRef;
                watchState.ReactiveSubscriptionEntryRef = subscriptionEntryRef;
            }

            if (watchState.ReactiveWaitEntryRef.IsValid
                && watchState.SchedulingEntryRef.IsValid
                && SignalExecutionSupportUtility.TryGetEntryState(
                    frame,
                    watchState.ReactiveWaitEntryRef,
                    out ReactiveStateDomain.ReactiveWaitState? reactiveWaitState))
            {
                reactiveWaitState!.ScheduleEntryRef = watchState.SchedulingEntryRef;
            }
        }

        private static void NotifyResolved(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            WatchConditionNodeState watchState)
        {
            if (string.IsNullOrEmpty(watchState.ConditionType))
            {
                return;
            }

            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return;
            }

            var sourceRef = CreateSourceRef(frame, actionIndex, watchState.ConditionType);
            host.GetRequiredDomain<ReactiveStateDomain>().NotifySource(new ReactiveSourceNotification(
                sourceRef,
                new MatchTokenRef(
                    sourceRef,
                    TokenKind.ConditionOccurrence,
                    $"{currentTick}:{actionIndex}:{watchState.ConditionType}")));
        }

        private static void RecordIntermediateEvent(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            WatchConditionNodeState watchState,
            string outputPortId,
            string eventKind,
            bool repeat)
        {
            var semanticsPayload = SignalPayload.Empty;
            semanticsPayload["ConditionType"] = watchState.ConditionType;
            semanticsPayload["Parameters"] = watchState.ParametersRaw;
            semanticsPayload["Repeat"] = repeat.ToString();
            if (!string.IsNullOrWhiteSpace(outputPortId))
            {
                semanticsPayload["OutputPortId"] = outputPortId;
            }

            if (BlueprintTime.HasDeadline(watchState.TimeoutTargetTick))
            {
                semanticsPayload["TimeoutTargetTick"] = watchState.TimeoutTargetTick.ToString();
            }

            var eventSemantics = BlueprintEventContextSemanticUtility.BuildEventContextSemantics(
                semantics: null,
                fallbackEventKind: eventKind,
                fallbackSignalTag: string.Empty,
                payload: semanticsPayload,
                fallbackTargetRefSerialized: watchState.TargetRefSerialized,
                fallbackTargetSummary: watchState.TargetSummary);
            GraphNodeExecutionTemplate.RecordPlanEvent(
                frame,
                actionIndex,
                currentTick,
                watchState,
                GraphNodeExecutionTemplate.CreateEventContract(
                    eventKind,
                    outputPortId,
                    new GraphNodeEventEmissionOptions(eventSemantics)),
                planPayload =>
                {
                    planPayload["ConditionType"] = watchState.ConditionType;
                    planPayload["Parameters"] = watchState.ParametersRaw;
                    planPayload["Repeat"] = repeat.ToString();
                    if (BlueprintTime.HasDeadline(watchState.TimeoutTargetTick))
                    {
                        planPayload["TimeoutTargetTick"] = watchState.TimeoutTargetTick.ToString();
                    }
                });
        }

        private static void RecordCompletionEvent(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            WatchConditionNodeState watchState,
            GraphNodeExecutionResult completionResult,
            bool repeat)
        {
            var eventSemantics = BlueprintEventContextSemanticUtility.BuildEventContextSemantics(
                semantics: null,
                fallbackEventKind: completionResult.EventKind,
                fallbackSignalTag: string.Empty,
                payload: SignalPayload.Empty,
                fallbackTargetRefSerialized: watchState.TargetRefSerialized,
                fallbackTargetSummary: watchState.TargetSummary);
            var completionContract = GraphNodeExecutionTemplate.CreateCompletionContract(
                completionResult,
                new GraphNodeEventEmissionOptions(eventSemantics));
            GraphNodeExecutionTemplate.RecordCompletionEvent(
                frame,
                actionIndex,
                currentTick,
                watchState,
                completionContract,
                payload =>
                {
                    payload["ConditionType"] = watchState.ConditionType;
                    payload["Parameters"] = watchState.ParametersRaw;
                    payload["Repeat"] = repeat.ToString();

                    if (BlueprintTime.HasDeadline(watchState.TimeoutTargetTick))
                    {
                        payload["TimeoutTargetTick"] = watchState.TimeoutTargetTick.ToString();
                    }
                });
        }

        private static bool HasTimedOut(
            BlueprintFrame frame,
            int currentTick,
            WatchConditionNodeState watchState)
        {
            if (SignalExecutionSupportUtility.TryGetEntryState(
                frame,
                watchState.SchedulingEntryRef,
                out SchedulingStateDomain.SchedulingEntryState? schedulingState))
            {
                return schedulingState!.Status == SchedulingStateDomain.SchedulingEntryStatus.Scheduled
                    && currentTick >= schedulingState.TargetTick.Value;
            }

            return BlueprintTime.HasReached(currentTick, watchState.TimeoutTargetTick);
        }

        private static string BuildWaitingSummary(WatchConditionRuntimeConfig runtimeConfig)
        {
            var summary = string.IsNullOrWhiteSpace(runtimeConfig.ConditionType)
                ? "等待未配置条件"
                : $"等待条件 {runtimeConfig.ConditionType}";
            if (!string.IsNullOrWhiteSpace(runtimeConfig.TargetSummary))
            {
                summary += $" | 目标 {runtimeConfig.TargetSummary}";
            }

            if (runtimeConfig.Repeat)
            {
                summary += " | Repeat";
            }

            if (runtimeConfig.TimeoutSeconds > 0f)
            {
                summary += $" | 超时 {runtimeConfig.TimeoutSeconds:0.###}s";
            }

            return summary;
        }

        private static string BuildTriggeredSummary(
            WatchConditionNodeState watchState,
            bool isRearmed)
        {
            var summary = string.IsNullOrWhiteSpace(watchState.ConditionType)
                ? "条件已触发"
                : $"条件 {watchState.ConditionType} 已触发";
            if (isRearmed)
            {
                summary += "，已重新进入监听";
            }

            return summary;
        }

        private static string BuildTimeoutSummary(WatchConditionNodeState watchState)
        {
            return string.IsNullOrWhiteSpace(watchState.ConditionType)
                ? "等待条件超时"
                : $"等待条件 {watchState.ConditionType} 超时";
        }

        private static SourceRef CreateSourceRef(
            BlueprintFrame frame,
            int actionIndex,
            string conditionType)
        {
            return new SourceRef(
                SourceDomain,
                SourceKind.Condition,
                $"{frame.Actions[actionIndex].Id}:{conditionType}");
        }

        private static void ResetAuxiliaryRefs(WatchConditionNodeState watchState)
        {
            watchState.ReactiveWaitEntryRef = RuntimeEntryRef.Invalid;
            watchState.ReactiveSubscriptionEntryRef = RuntimeEntryRef.Invalid;
            watchState.SchedulingEntryRef = RuntimeEntryRef.Invalid;
        }

        private static ConditionWatchRegistration CreateRegistration(
            int actionIndex,
            WatchConditionNodeState watchState)
        {
            return SignalExecutionSupportUtility.CreateConditionWatchRegistration(
                actionIndex,
                watchState.Descriptor);
        }

        private static ConditionWatchHandle EnsureWatchHandle(
            int actionIndex,
            WatchConditionNodeState watchState)
        {
            if (!watchState.WatchHandle.IsValid)
            {
                watchState.WatchHandle = CreateRegistration(actionIndex, watchState).Handle;
            }

            return watchState.WatchHandle;
        }

        private static GraphNodeExecutionResult? CreateCompletionResult(WatchConditionTickResult tickResult)
        {
            return tickResult.Kind switch
            {
                WatchConditionTickResultKind.Completed => GraphNodeExecutionTemplate.CreateCompletionResult(
                    GraphNodeExecutionResultKind.Completed,
                    "Signal.WatchCondition.Triggered",
                    tickResult.OutputPortId),
                WatchConditionTickResultKind.TimedOut => GraphNodeExecutionTemplate.CreateCompletionResult(
                    GraphNodeExecutionResultKind.TimedOut,
                    "Signal.WatchCondition.Timeout",
                    tickResult.OutputPortId),
                _ => null,
            };
        }
    }
}
