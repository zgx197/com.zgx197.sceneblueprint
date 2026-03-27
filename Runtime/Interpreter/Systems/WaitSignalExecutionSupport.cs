#nullable enable
using System;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal static class WaitSignalExecutionSupport
    {
        private const string ReactiveSlotKey = "wait-signal.reactive";
        private const string SchedulingSlotKey = "wait-signal.timeout";
        private const string SourceDomain = "signals";

        private readonly struct WaitSignalMatch
        {
            public WaitSignalMatch(
                bool isMatched,
                string matchedSignalTag,
                int matchedSignalIndex,
                SignalEntry matchedSignalEntry)
            {
                IsMatched = isMatched;
                MatchedSignalTag = matchedSignalTag ?? string.Empty;
                MatchedSignalIndex = matchedSignalIndex;
                MatchedSignalEntry = matchedSignalEntry;
            }

            public bool IsMatched { get; }

            public string MatchedSignalTag { get; }

            public int MatchedSignalIndex { get; }

            public SignalEntry MatchedSignalEntry { get; }
        }

        public static bool TryExecute(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                SignalSystem.WaitSignalStateDescriptor,
                ref runtimeState);
            var waitState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(ref view, frame, actionIndex, waitState);
                return false;
            }

            return GraphNodeExecutionTemplate.TryFinalizeCompletion(
                frame,
                ref view,
                actionIndex,
                ref runtimeState,
                SignalSystem.WaitSignalStateDescriptor,
                EvaluateTick(frame, ref view, actionIndex, waitState));
        }

        public static void InitializeState(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            WaitSignalNodeState waitState)
        {
            var runtimeConfig = SignalActionRuntimeConfigResolver.ResolveWaitSignal(frame, actionIndex);
            GraphNodeExecutionTemplate.InitializeTimedState(
                waitState,
                view.CurrentTick,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimeConfig.PlanSource,
                    runtimeConfig.PlanSummary,
                    runtimeConfig.ConditionSummary),
                string.Empty);
            waitState.SignalTag = runtimeConfig.SignalTag;
            waitState.SubjectRefFilterSerialized = runtimeConfig.SubjectRefFilterSerialized;
            waitState.SubjectRefFilterSummary = runtimeConfig.SubjectRefFilterSummary;
            waitState.IsWildcardPattern = runtimeConfig.IsWildcardPattern;
            waitState.HasLoggedReleaseWildcardWarning = false;
            waitState.TimeoutTargetTick = BlueprintTime.ScheduleTimeoutAfterSeconds(
                view.CurrentTick,
                runtimeConfig.TimeoutSeconds,
                view.TimeSettings);
            waitState.ExecutionSummary = BuildWaitSignalExecutionSummary(waitState);
            ResetAuxiliaryRefs(waitState);
            EnsureAuxiliaryState(frame, actionIndex, waitState);

            Debug.Log(
                $"[SignalSystem] WaitSignal (index={actionIndex}) 开始等待信号: {waitState.SignalTag}, " +
                $"subjectFilter={waitState.SubjectRefFilterSummary}, timeoutTargetTick={waitState.TimeoutTargetTick} (-1=无超时)");
        }

        public static GraphNodeExecutionResult EvaluateTick(
            BlueprintFrame frame,
            ref FrameView view,
            int actionIndex,
            WaitSignalNodeState waitState)
        {
            EnsureAuxiliaryState(frame, actionIndex, waitState);

            var match = TryFindMatch(view.Bus, waitState, actionIndex);
            if (match.IsMatched)
            {
                var completionResult = GraphNodeExecutionTemplate.CreateCompletionResult(
                    GraphNodeExecutionResultKind.Completed,
                    "Signal.WaitSignal.Matched",
                    ActionPortIds.SignalWaitSignal.Out);
                waitState.ExecutionSummary = BuildWaitSignalMatchedSummary(waitState, match.MatchedSignalTag);
                NotifyResolved(frame, view.CurrentTick, waitState, match.MatchedSignalTag, match.MatchedSignalIndex);
                RecordCompletionEvent(
                    frame,
                    actionIndex,
                    view.CurrentTick,
                    waitState,
                    completionResult,
                    match);
                ReleaseAuxiliaryState(frame, waitState);
                return completionResult;
            }

            if (HasTimedOut(frame, view.CurrentTick, waitState))
            {
                var completionResult = GraphNodeExecutionTemplate.CreateCompletionResult(
                    GraphNodeExecutionResultKind.TimedOut,
                    "Signal.WaitSignal.Timeout",
                    ActionPortIds.SignalWaitSignal.OnTimeout);
                Debug.Log($"[SignalSystem] WaitSignal (index={actionIndex}) 超时！");
                waitState.ExecutionSummary = BuildWaitSignalTimeoutSummary(waitState);
                ReleaseAuxiliaryState(frame, waitState);
                RecordCompletionEvent(
                    frame,
                    actionIndex,
                    view.CurrentTick,
                    waitState,
                    completionResult,
                    default);
                return completionResult;
            }

            return GraphNodeExecutionTemplate.RunningResult;
        }

        public static void ReleaseAuxiliaryState(BlueprintFrame frame, WaitSignalNodeState waitState)
        {
            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host != null)
            {
                if (waitState.SchedulingEntryRef.IsValid)
                {
                    host.GetRequiredDomain<SchedulingStateDomain>().ReleaseEntry(waitState.SchedulingEntryRef);
                }

                var reactiveDomain = host.GetRequiredDomain<ReactiveStateDomain>();
                if (waitState.ReactiveWaitEntryRef.IsValid)
                {
                    reactiveDomain.ReleaseEntry(waitState.ReactiveWaitEntryRef);
                }
                else if (waitState.ReactiveSubscriptionEntryRef.IsValid)
                {
                    reactiveDomain.ReleaseEntry(waitState.ReactiveSubscriptionEntryRef);
                }
            }

            ResetAuxiliaryRefs(waitState);
        }

        public static bool TryGetWaitSignalState(
            BlueprintFrame frame,
            int actionIndex,
            out WaitSignalNodeState? waitState)
        {
            return SignalExecutionSupportUtility.TryGetState(
                frame,
                actionIndex,
                SignalSystem.WaitSignalStateDescriptor,
                out waitState);
        }

        private static void EnsureAuxiliaryState(
            BlueprintFrame frame,
            int actionIndex,
            WaitSignalNodeState waitState)
        {
            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return;
            }

            var ownerRef = NodePrivateExecutionStateSupport.CreateActionOwnerRef(frame, actionIndex);

            if (!string.IsNullOrEmpty(waitState.SignalTag) && !waitState.ReactiveWaitEntryRef.IsValid)
            {
                var reactiveDomain = host.GetRequiredDomain<ReactiveStateDomain>();
                var waitEntryRef = reactiveDomain.EnsureWait(new ReactiveWaitRequest(
                    ownerRef,
                    ReactiveSlotKey,
                    ReactiveWaitKind.Signal,
                    StateLifetime.Execution,
                    ResolvePolicy.Any));
                var subscriptionEntryRef = reactiveDomain.AttachSubscription(new ReactiveSubscriptionRequest(
                    waitEntryRef,
                    waitState.SignalTag,
                    SourceKind.Signal,
                    CreateSourceRef(waitState.SignalTag)));

                waitState.ReactiveWaitEntryRef = waitEntryRef;
                waitState.ReactiveSubscriptionEntryRef = subscriptionEntryRef;
            }

            if (BlueprintTime.HasDeadline(waitState.TimeoutTargetTick) && !waitState.SchedulingEntryRef.IsValid)
            {
                var schedulingDomain = host.GetRequiredDomain<SchedulingStateDomain>();
                waitState.SchedulingEntryRef = schedulingDomain.Schedule(new SchedulingEntryRequest(
                    ownerRef,
                    SchedulingSlotKey,
                    SchedulingKind.Timeout,
                    StateLifetime.Execution,
                    new RuntimeTick(waitState.StartTick),
                    new RuntimeTick(waitState.TimeoutTargetTick),
                    PausePolicy.RespectRuntimePause));
            }

            if (waitState.ReactiveWaitEntryRef.IsValid
                && waitState.SchedulingEntryRef.IsValid
                && SignalExecutionSupportUtility.TryGetEntryState(
                    frame,
                    waitState.ReactiveWaitEntryRef,
                    out ReactiveStateDomain.ReactiveWaitState? reactiveWaitState))
            {
                reactiveWaitState!.ScheduleEntryRef = waitState.SchedulingEntryRef;
            }
        }

        private static void NotifyResolved(
            BlueprintFrame frame,
            int currentTick,
            WaitSignalNodeState waitState,
            string matchedSignalTag,
            int matchedSignalIndex)
        {
            if (string.IsNullOrEmpty(waitState.SignalTag))
            {
                return;
            }

            var host = NodePrivateExecutionStateSupport.TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return;
            }

            var reactiveDomain = host.GetRequiredDomain<ReactiveStateDomain>();
            var sourceRef = CreateSourceRef(waitState.SignalTag);
            reactiveDomain.NotifySource(new ReactiveSourceNotification(
                sourceRef,
                CreateMatchToken(sourceRef, matchedSignalTag, currentTick, matchedSignalIndex)));
        }

        private static bool HasTimedOut(
            BlueprintFrame frame,
            int currentTick,
            WaitSignalNodeState waitState)
        {
            if (SignalExecutionSupportUtility.TryGetEntryState(
                frame,
                waitState.SchedulingEntryRef,
                out SchedulingStateDomain.SchedulingEntryState? schedulingState))
            {
                return schedulingState!.Status == SchedulingStateDomain.SchedulingEntryStatus.Scheduled
                    && currentTick >= schedulingState.TargetTick.Value;
            }

            return BlueprintTime.HasReached(currentTick, waitState.TimeoutTargetTick);
        }

        private static WaitSignalMatch TryFindMatch(
            ISignalBus? bus,
            WaitSignalNodeState waitState,
            int actionIndex)
        {
            if (bus == null || string.IsNullOrEmpty(waitState.SignalTag))
            {
                return default;
            }

            var injected = bus.GetFrameInjected();
            var waitTag = waitState.SignalTag;
            if (!waitState.IsWildcardPattern)
            {
                var waitHash = waitTag.GetHashCode();
                for (var signalIndex = 0; signalIndex < injected.Count; signalIndex++)
                {
                    if (injected[signalIndex].TagHash == waitHash
                        && SignalExecutionSupportUtility.MatchesEntityRefFilter(
                            waitState.SubjectRefFilterSerialized,
                            injected[signalIndex].SubjectRefSerialized))
                    {
                        Debug.Log($"[SignalSystem] WaitSignal (index={actionIndex}) 精确匹配: {waitTag}");
                        return new WaitSignalMatch(
                            true,
                            GetMatchedSignalTag(injected[signalIndex], waitTag),
                            signalIndex,
                            injected[signalIndex]);
                    }
                }

                return default;
            }

#if UNITY_EDITOR || DEBUG
            var waitSignalTag = new SignalTag(waitTag);
            for (var signalIndex = 0; signalIndex < injected.Count; signalIndex++)
            {
                var debugTag = injected[signalIndex].DebugTag;
                if (debugTag == null)
                {
                    continue;
                }

                var candidateTag = new SignalTag(debugTag);
                if (!candidateTag.MatchesPattern(waitSignalTag)
                    || !SignalExecutionSupportUtility.MatchesEntityRefFilter(
                        waitState.SubjectRefFilterSerialized,
                        injected[signalIndex].SubjectRefSerialized))
                {
                    continue;
                }

                Debug.Log(
                    $"[SignalSystem] WaitSignal (index={actionIndex}) 通配匹配: pattern={waitTag}, matched={debugTag}");
                return new WaitSignalMatch(true, debugTag, signalIndex, injected[signalIndex]);
            }

            return default;
#else
            if (!waitState.HasLoggedReleaseWildcardWarning)
            {
                Debug.LogWarning($"[SignalSystem] WaitSignal (index={actionIndex}) 通配符 '{waitTag}' 在 Release 下无效，退化为精确匹配");
                waitState.HasLoggedReleaseWildcardWarning = true;
            }

            var waitHash = waitTag.GetHashCode();
            for (var signalIndex = 0; signalIndex < injected.Count; signalIndex++)
            {
                if (injected[signalIndex].TagHash == waitHash
                    && SignalExecutionSupportUtility.MatchesEntityRefFilter(
                        waitState.SubjectRefFilterSerialized,
                        injected[signalIndex].SubjectRefSerialized))
                {
                    return new WaitSignalMatch(
                        true,
                        GetMatchedSignalTag(injected[signalIndex], waitTag),
                        signalIndex,
                        injected[signalIndex]);
                }
            }

            return default;
#endif
        }

        private static void RecordCompletionEvent(
            BlueprintFrame frame,
            int actionIndex,
            int currentTick,
            WaitSignalNodeState waitState,
            GraphNodeExecutionResult completionResult,
            WaitSignalMatch match)
        {
            var upstreamContext = match.MatchedSignalEntry.EventContext;
            var subjectSerialized = !string.IsNullOrWhiteSpace(upstreamContext?.SubjectRefSerialized)
                ? upstreamContext!.SubjectRefSerialized
                : match.MatchedSignalEntry.SubjectRefSerialized;
            var subjectSummary = !string.IsNullOrWhiteSpace(upstreamContext?.SubjectSummary)
                ? upstreamContext!.SubjectSummary
                : SemanticSummaryUtility.DescribeEntityRef(subjectSerialized);

            var payload = GraphNodeExecutionTemplate.CreatePlanPayload(waitState);
            payload["WaitSignalTag"] = waitState.SignalTag;
            payload["SubjectFilter"] = waitState.SubjectRefFilterSummary;
            payload["IsWildcardPattern"] = waitState.IsWildcardPattern.ToString();

            if (!string.IsNullOrWhiteSpace(match.MatchedSignalTag))
            {
                payload["MatchedSignalTag"] = match.MatchedSignalTag;
            }

            if (match.MatchedSignalIndex >= 0)
            {
                payload["MatchedSignalIndex"] = match.MatchedSignalIndex.ToString();
            }

            if (BlueprintTime.HasDeadline(waitState.TimeoutTargetTick))
            {
                payload["TimeoutTargetTick"] = waitState.TimeoutTargetTick.ToString();
            }

            var eventSemantics = BlueprintEventContextSemanticUtility.BuildEventContextSemantics(
                semantics: upstreamContext != null
                    ? BlueprintEventContextSemanticUtility.BuildSemantics(upstreamContext)
                    : null,
                fallbackEventKind: completionResult.EventKind,
                fallbackSignalTag: !string.IsNullOrWhiteSpace(match.MatchedSignalTag)
                    ? match.MatchedSignalTag
                    : waitState.SignalTag,
                payload: payload,
                fallbackSubjectRefSerialized: subjectSerialized,
                fallbackSubjectSummary: subjectSummary,
                fallbackInstigatorRefSerialized: upstreamContext?.InstigatorRefSerialized,
                fallbackInstigatorSummary: upstreamContext?.InstigatorSummary,
                fallbackTargetRefSerialized: upstreamContext?.TargetRefSerialized,
                fallbackTargetSummary: !string.IsNullOrWhiteSpace(upstreamContext?.TargetSummary)
                    ? upstreamContext!.TargetSummary
                    : waitState.SubjectRefFilterSummary);
            var completionContract = GraphNodeExecutionTemplate.CreateCompletionContract(
                completionResult,
                new GraphNodeEventEmissionOptions(eventSemantics));
            GraphNodeExecutionTemplate.RecordCompletionEvent(
                frame,
                actionIndex,
                currentTick,
                waitState,
                completionContract,
                completionPayload =>
                {
                    foreach (var key in payload.Keys)
                    {
                        completionPayload[key] = payload[key];
                    }
                });
        }

        private static string BuildWaitSignalExecutionSummary(WaitSignalNodeState waitState)
        {
            var summary = string.IsNullOrWhiteSpace(waitState.SignalTag)
                ? "等待未配置的信号"
                : $"等待信号 {waitState.SignalTag}";
            if (waitState.IsWildcardPattern)
            {
                summary += " | 通配匹配";
            }

            if (!string.IsNullOrWhiteSpace(waitState.SubjectRefFilterSummary))
            {
                summary += $" | 主体 {waitState.SubjectRefFilterSummary}";
            }

            if (BlueprintTime.HasDeadline(waitState.TimeoutTargetTick))
            {
                summary += $" | 超时 T={waitState.TimeoutTargetTick}";
            }

            return summary;
        }

        private static string BuildWaitSignalMatchedSummary(
            WaitSignalNodeState waitState,
            string matchedSignalTag)
        {
            var effectiveTag = string.IsNullOrWhiteSpace(matchedSignalTag)
                ? waitState.SignalTag
                : matchedSignalTag;
            return string.IsNullOrWhiteSpace(effectiveTag)
                ? "已匹配信号"
                : $"已匹配信号 {effectiveTag}";
        }

        private static string BuildWaitSignalTimeoutSummary(WaitSignalNodeState waitState)
        {
            return string.IsNullOrWhiteSpace(waitState.SignalTag)
                ? "等待信号超时"
                : $"等待信号 {waitState.SignalTag} 超时";
        }

        private static SourceRef CreateSourceRef(string signalTag)
        {
            return new SourceRef(SourceDomain, SourceKind.Signal, signalTag);
        }

        private static MatchTokenRef CreateMatchToken(
            SourceRef sourceRef,
            string matchedSignalTag,
            int currentTick,
            int matchedSignalIndex)
        {
            var occurrenceTag = string.IsNullOrWhiteSpace(matchedSignalTag)
                ? sourceRef.LogicalKey
                : matchedSignalTag;
            var occurrenceKey = $"{currentTick}:{Math.Max(matchedSignalIndex, 0)}:{occurrenceTag}";
            return new MatchTokenRef(sourceRef, TokenKind.SignalOccurrence, occurrenceKey);
        }

        private static string GetMatchedSignalTag(SignalEntry entry, string fallbackTag)
        {
#if UNITY_EDITOR || DEBUG
            return string.IsNullOrEmpty(entry.DebugTag) ? fallbackTag : entry.DebugTag;
#else
            return fallbackTag;
#endif
        }

        private static void ResetAuxiliaryRefs(WaitSignalNodeState waitState)
        {
            waitState.ReactiveWaitEntryRef = RuntimeEntryRef.Invalid;
            waitState.ReactiveSubscriptionEntryRef = RuntimeEntryRef.Invalid;
            waitState.SchedulingEntryRef = RuntimeEntryRef.Invalid;
        }
    }
}
