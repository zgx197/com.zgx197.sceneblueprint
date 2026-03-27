#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class SchedulingStateDomain : ISchedulingStateDomain
    {
        public static readonly StateDomainId SchedulingDomainId = new("scheduling");

        private const string SchedulingDescriptorId = "scheduling.entry";

        private readonly IRuntimeStateBackend _backend;

        public SchedulingStateDomain(IRuntimeStateBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public StateDomainId DomainId => SchedulingDomainId;

        public RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey)
        {
            EnsureDescriptorMatchesDomain(descriptor);
            return _backend.EnsureEntry(descriptor, ownerRef, slotKey);
        }

        public RuntimeEntryRef Schedule(SchedulingEntryRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Scheduling entry request must be valid.", nameof(request));
            }

            var descriptor = CreateSchedulingDescriptor(request.Lifetime);
            var entryRef = EnsureEntry(descriptor, request.OwnerRef, request.SlotKey);
            var entry = GetRequiredEntry(entryRef);

            if (entry.State is SchedulingEntryState existingState)
            {
                EnsureCompatible(existingState, request);
                return entryRef;
            }

            if (entry.State is not null)
            {
                throw CreateStateTypeMismatchException(entryRef, entry.State, typeof(SchedulingEntryState));
            }

            entry.State = new SchedulingEntryState(
                request.Kind,
                request.Lifetime,
                request.StartTick,
                request.TargetTick,
                request.PausePolicy);
            return entryRef;
        }

        public void Cancel(RuntimeEntryRef scheduleEntryRef)
        {
            var state = GetRequiredSchedulingState(scheduleEntryRef);
            state.Status = SchedulingEntryStatus.Cancelled;
        }

        public void Pause(RuntimeEntryRef scheduleEntryRef)
        {
            var state = GetRequiredSchedulingState(scheduleEntryRef);
            if (state.Status == SchedulingEntryStatus.Cancelled)
            {
                return;
            }

            state.Status = SchedulingEntryStatus.Paused;
        }

        public void Resume(RuntimeEntryRef scheduleEntryRef)
        {
            var state = GetRequiredSchedulingState(scheduleEntryRef);
            if (state.Status != SchedulingEntryStatus.Paused)
            {
                return;
            }

            state.Status = SchedulingEntryStatus.Scheduled;
        }

        public bool TryLocateEntry(StateOwnerRef ownerRef, string slotKey, out RuntimeEntryRef entryRef)
        {
            return _backend.TryLocateEntry(DomainId, ownerRef, slotKey, out entryRef);
        }

        public void ReleaseEntry(RuntimeEntryRef entryRef)
        {
            _backend.ReleaseEntry(entryRef);
        }

        public void ReleaseOwnedEntries(StateOwnerRef ownerRef)
        {
            _backend.ReleaseOwnedEntries(DomainId, ownerRef);
        }

        public IReadOnlyList<RuntimeEntryRef> EnumerateEntries(ObservationFilter? filter = null)
        {
            return _backend.EnumerateEntries(DomainId, filter);
        }

        private static StateDescriptor CreateSchedulingDescriptor(StateLifetime lifetime)
        {
            return new StateDescriptor(
                SchedulingDescriptorId,
                SchedulingDomainId,
                lifetime,
                debugName: "Scheduling.Entry");
        }

        private static void EnsureDescriptorMatchesDomain(StateDescriptor descriptor)
        {
            if (!descriptor.IsValid)
            {
                throw new ArgumentException("State descriptor must be valid.", nameof(descriptor));
            }

            if (descriptor.DomainId != SchedulingDomainId)
            {
                throw new ArgumentException(
                    $"Scheduling state descriptor must belong to domain '{SchedulingDomainId}', but was '{descriptor.DomainId}'.",
                    nameof(descriptor));
            }
        }

        private static void EnsureCompatible(SchedulingEntryState state, SchedulingEntryRequest request)
        {
            if (state.Kind != request.Kind
                || state.Lifetime != request.Lifetime
                || state.StartTick != request.StartTick
                || state.TargetTick != request.TargetTick
                || state.PausePolicy != request.PausePolicy)
            {
                throw new InvalidOperationException("Scheduling request conflicts with the existing schedule entry.");
            }
        }

        private RuntimeStateEntryRecord GetRequiredEntry(RuntimeEntryRef entryRef)
        {
            if (!entryRef.IsValid || entryRef.DomainId != DomainId)
            {
                throw new KeyNotFoundException($"Scheduling state entry was not found: {entryRef}");
            }

            if (!_backend.TryGetEntry(entryRef, out var entry) || entry is null)
            {
                throw new KeyNotFoundException($"Scheduling state entry was not found: {entryRef}");
            }

            return entry;
        }

        private SchedulingEntryState GetRequiredSchedulingState(RuntimeEntryRef entryRef)
        {
            var entry = GetRequiredEntry(entryRef);
            if (entry.State is SchedulingEntryState state)
            {
                return state;
            }

            throw new InvalidOperationException($"Scheduling entry is invalid: {entryRef}");
        }

        private static InvalidOperationException CreateStateTypeMismatchException(RuntimeEntryRef entryRef, object state, Type expectedType)
        {
            return new InvalidOperationException(
                $"Scheduling state entry {entryRef} already holds {state.GetType().FullName}, not {expectedType.FullName}.");
        }

        internal enum SchedulingEntryStatus
        {
            Scheduled = 0,
            Paused = 1,
            Cancelled = 2,
        }

        internal sealed class SchedulingEntryState
        {
            public SchedulingEntryState(
                SchedulingKind kind,
                StateLifetime lifetime,
                RuntimeTick startTick,
                RuntimeTick targetTick,
                PausePolicy pausePolicy)
            {
                Kind = kind;
                Lifetime = lifetime;
                StartTick = startTick;
                TargetTick = targetTick;
                PausePolicy = pausePolicy;
                Status = SchedulingEntryStatus.Scheduled;
            }

            public SchedulingKind Kind { get; }

            public StateLifetime Lifetime { get; }

            public RuntimeTick StartTick { get; }

            public RuntimeTick TargetTick { get; }

            public PausePolicy PausePolicy { get; }

            public SchedulingEntryStatus Status { get; set; }
        }
    }
}
