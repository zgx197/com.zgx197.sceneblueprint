#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 统一 compiled payload 访问层。
    /// 负责把“按 actionId 扫 Metadata + 解析 json”的细节收口到同一入口，
    /// 并提供 frame 级弱引用缓存，避免 runtime system 各自重复做 key 扫描。
    /// </summary>
    internal static class CompiledActionResolver
    {
        public delegate bool CompiledPayloadReader<TPayload>(
            PropertyValue[]? metadata,
            string? actionId,
            out TPayload? payload)
            where TPayload : class;

        private static readonly ConditionalWeakTable<BlueprintFrame, ResolverCache> Caches = new();

        public static bool TryResolve<TPayload>(
            BlueprintFrame? frame,
            int actionIndex,
            string cacheKind,
            CompiledPayloadReader<TPayload> reader,
            out TPayload? payload)
            where TPayload : class
        {
            payload = null;
            if (!TryGetActionId(frame, actionIndex, out var actionId))
            {
                return false;
            }

            return TryResolve(frame, actionId, cacheKind, reader, out payload);
        }

        public static bool TryResolve<TPayload>(
            BlueprintFrame? frame,
            string? actionId,
            string cacheKind,
            CompiledPayloadReader<TPayload> reader,
            out TPayload? payload)
            where TPayload : class
        {
            payload = null;
            if (frame == null
                || string.IsNullOrWhiteSpace(actionId)
                || string.IsNullOrWhiteSpace(cacheKind))
            {
                return false;
            }

            var cacheKey = BuildCacheKey(cacheKind, actionId);
            var cache = Caches.GetOrCreateValue(frame);
            if (cache.TryGetValue(cacheKey, out var cachedEntry))
            {
                payload = cachedEntry.Payload as TPayload;
                return cachedEntry.HasValue && payload != null;
            }

            var resolved = reader(frame.TransportMetadata, actionId, out payload) && payload != null;
            cache[cacheKey] = new ResolverCacheEntry(resolved, payload);
            return resolved;
        }

        public static bool TryGetSignalAction(BlueprintFrame? frame, int actionIndex, out SignalCompiledAction? compiledAction)
        {
            return TryResolve(frame, actionIndex, CompiledActionCacheKinds.SignalAction, SignalCompiledActionMetadata.TryRead, out compiledAction);
        }

        public static bool TryGetFlowAction(BlueprintFrame? frame, int actionIndex, out FlowCompiledAction? compiledAction)
        {
            return TryResolve(frame, actionIndex, CompiledActionCacheKinds.FlowAction, FlowCompiledActionMetadata.TryRead, out compiledAction);
        }

        public static bool TryGetBlackboardAction(BlueprintFrame? frame, int actionIndex, out BlackboardCompiledAction? compiledAction)
        {
            return TryResolve(frame, actionIndex, CompiledActionCacheKinds.BlackboardAction, BlackboardCompiledActionMetadata.TryRead, out compiledAction);
        }

        public static bool TryGetEntityRefAction(BlueprintFrame? frame, int actionIndex, out EntityRefCompiledAction? compiledAction)
        {
            return TryResolve(frame, actionIndex, CompiledActionCacheKinds.EntityRefAction, EntityRefActionCompiledMetadata.TryRead, out compiledAction);
        }

        public static SignalEmitCompiledData? TryGetSignalEmit(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetSignalAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.Emit
                : null;
        }

        public static SignalWaitSignalCompiledData? TryGetSignalWaitSignal(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetSignalAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.WaitSignal
                : null;
        }

        public static SignalWatchConditionCompiledData? TryGetSignalWatchCondition(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetSignalAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.WatchCondition
                : null;
        }

        public static SignalCompositeConditionCompiledData? TryGetSignalCompositeCondition(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetSignalAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.CompositeCondition
                : null;
        }

        public static FlowJoinCompiledData? TryGetFlowJoin(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetFlowAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.Join
                : null;
        }

        public static FlowFilterCompiledData? TryGetFlowFilter(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetFlowAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.Filter
                : null;
        }

        public static FlowBranchCompiledData? TryGetFlowBranch(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetFlowAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.Branch
                : null;
        }

        public static FlowDelayCompiledData? TryGetFlowDelay(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetFlowAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.Delay
                : null;
        }

        public static BlackboardGetCompiledData? TryGetBlackboardGet(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetBlackboardAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.Get
                : null;
        }

        public static BlackboardSetCompiledData? TryGetBlackboardSet(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetBlackboardAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.Set
                : null;
        }

        public static TriggerEnterAreaCompiledData? TryGetTriggerEnterArea(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetEntityRefAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.TriggerEnterArea
                : null;
        }

        public static InteractionApproachTargetCompiledData? TryGetInteractionApproachTarget(BlueprintFrame? frame, int actionIndex)
        {
            return TryGetEntityRefAction(frame, actionIndex, out var compiledAction)
                ? compiledAction?.InteractionApproachTarget
                : null;
        }

        private static bool TryGetActionId(BlueprintFrame? frame, int actionIndex, out string actionId)
        {
            actionId = string.Empty;
            if (frame == null
                || actionIndex < 0
                || actionIndex >= frame.Actions.Length)
            {
                return false;
            }

            actionId = frame.Actions[actionIndex].Id ?? string.Empty;
            return !string.IsNullOrWhiteSpace(actionId);
        }

        private static string BuildCacheKey(string cacheKind, string actionId)
        {
            return $"{cacheKind}:{actionId.Trim()}";
        }

        private sealed class ResolverCache : Dictionary<string, ResolverCacheEntry>
        {
            public ResolverCache()
                : base(StringComparer.Ordinal)
            {
            }
        }

        private sealed class ResolverCacheEntry
        {
            public ResolverCacheEntry(bool hasValue, object? payload)
            {
                HasValue = hasValue;
                Payload = payload;
            }

            public bool HasValue { get; }

            public object? Payload { get; }
        }
    }
}
