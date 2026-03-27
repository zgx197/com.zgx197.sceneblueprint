#nullable enable
using System;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal static class SignalExecutionSupportUtility
    {
        public static void RecordEvent(BlueprintFrame? frame, BlueprintEventContext? eventContext)
        {
            BlueprintEventHistoryRuntimeSupport.RecordEvent(frame?.Runner, eventContext, EventHistoryRecordKind.Emit);
        }

        public static bool MatchesEntityRefFilter(string filterSerialized, string candidateSerialized)
        {
            var normalizedFilter = NormalizeSerializedEntityRef(filterSerialized);
            if (string.IsNullOrEmpty(normalizedFilter))
            {
                return true;
            }

            var normalizedCandidate = NormalizeSerializedEntityRef(candidateSerialized);
            if (string.IsNullOrEmpty(normalizedCandidate))
            {
                return false;
            }

            return string.Equals(normalizedFilter, normalizedCandidate, StringComparison.Ordinal);
        }

        public static bool TryGetState<TState>(
            BlueprintFrame frame,
            int actionIndex,
            NodeStateDescriptor<TState> descriptor,
            out TState? state)
            where TState : class
        {
            var entryRef = NodePrivateExecutionStateSupport.TryGetEntryRef(frame, actionIndex, descriptor);
            if (!entryRef.HasValue)
            {
                state = null;
                return false;
            }

            return TryGetEntryState(frame, entryRef.Value, out state);
        }

        public static bool TryGetEntryState<TState>(
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

        public static ConditionWatchRegistration CreateConditionWatchRegistration(
            int actionIndex,
            ConditionWatchDescriptor? descriptor)
        {
            return new ConditionWatchRegistration(actionIndex, descriptor ?? new ConditionWatchDescriptor());
        }

        public static ConditionWatchRegistration CreateConditionWatchRegistration(
            int actionIndex,
            WatchConditionRuntimeConfig runtimeConfig)
        {
            return CreateConditionWatchRegistration(actionIndex, runtimeConfig.Descriptor);
        }

        public static ConditionWatchHandle BeginConditionWatch(
            ISignalBus? bus,
            ConditionWatchRegistration? registration)
        {
            if (bus == null || registration == null)
            {
                return default;
            }

            return bus.BeginConditionWatch(registration);
        }

        public static void EndConditionWatch(
            ISignalBus? bus,
            ConditionWatchHandle watchHandle)
        {
            if (bus == null || !watchHandle.IsValid)
            {
                return;
            }

            bus.EndConditionWatch(watchHandle);
        }

        private static string NormalizeSerializedEntityRef(string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return string.Empty;
            }

            return EntityRefCodec.Serialize(EntityRefCodec.Parse(serialized));
        }
    }
}
