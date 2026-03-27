#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class StateLifecycleCoordinator
    {
        private readonly IRuntimeStateBackend _backend;
        private readonly HashSet<RuntimeEntryRef> _deferredReleaseEntries = new();

        public StateLifecycleCoordinator(IRuntimeStateBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public int DeferredReleaseCount => _deferredReleaseEntries.Count;

        public StateLifecycleResult Handle(RuntimeLifecycleEvent runtimeEvent, StateDescriptor descriptor, string slotKey)
        {
            if (!runtimeEvent.IsValid)
            {
                throw new ArgumentException("Runtime lifecycle event must be valid.", nameof(runtimeEvent));
            }

            if (!descriptor.IsValid)
            {
                throw new ArgumentException("State descriptor must be valid.", nameof(descriptor));
            }

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("Slot key cannot be null or whitespace.", nameof(slotKey));
            }

            var ownerRef = runtimeEvent.OwnerRef;
            var hadExistingEntry = _backend.TryLocateEntry(descriptor.DomainId, ownerRef, slotKey, out var existingEntryRef);

            return runtimeEvent.Kind switch
            {
                LifecycleEventKind.Create => HandleEnsure(runtimeEvent, descriptor, slotKey, existingEntryRef, hadExistingEntry),
                LifecycleEventKind.Enter => HandleEnsure(runtimeEvent, descriptor, slotKey, existingEntryRef, hadExistingEntry),
                LifecycleEventKind.Reenter => HandleReenter(runtimeEvent, descriptor, existingEntryRef, hadExistingEntry),
                LifecycleEventKind.Suspend => StateLifecycleResult.NoOp(runtimeEvent, descriptor),
                LifecycleEventKind.Resume => StateLifecycleResult.NoOp(runtimeEvent, descriptor),
                LifecycleEventKind.Complete => HandleComplete(runtimeEvent, descriptor, existingEntryRef, hadExistingEntry),
                LifecycleEventKind.Reset => HandleReleaseOwned(runtimeEvent, descriptor),
                LifecycleEventKind.Dispose => HandleReleaseOwned(runtimeEvent, descriptor),
                _ => StateLifecycleResult.NoOp(runtimeEvent, descriptor),
            };
        }

        public bool IsDeferredRelease(RuntimeEntryRef entryRef)
        {
            return entryRef.IsValid && _deferredReleaseEntries.Contains(entryRef);
        }

        public int FlushDeferredReleases()
        {
            if (_deferredReleaseEntries.Count == 0)
            {
                return 0;
            }

            var released = 0;
            var pending = new List<RuntimeEntryRef>(_deferredReleaseEntries);
            _deferredReleaseEntries.Clear();

            for (var index = 0; index < pending.Count; index++)
            {
                if (_backend.ReleaseEntry(pending[index]))
                {
                    released++;
                }
            }

            return released;
        }

        private StateLifecycleResult HandleEnsure(
            RuntimeLifecycleEvent runtimeEvent,
            StateDescriptor descriptor,
            string slotKey,
            RuntimeEntryRef existingEntryRef,
            bool hadExistingEntry)
        {
            if (descriptor.Lifetime == StateLifetime.Execution
                && hadExistingEntry
                && _deferredReleaseEntries.Contains(existingEntryRef))
            {
                _backend.ReleaseEntry(existingEntryRef);
                _deferredReleaseEntries.Remove(existingEntryRef);
                hadExistingEntry = false;
                existingEntryRef = RuntimeEntryRef.Invalid;
            }

            var entryRef = _backend.EnsureEntry(descriptor, runtimeEvent.OwnerRef, slotKey);
            var disposition = hadExistingEntry
                ? StateLifecycleDisposition.Reused
                : StateLifecycleDisposition.Created;

            return new StateLifecycleResult(runtimeEvent, descriptor, entryRef, disposition, hadExistingEntry, 0);
        }

        private static StateLifecycleResult HandleReenter(
            RuntimeLifecycleEvent runtimeEvent,
            StateDescriptor descriptor,
            RuntimeEntryRef existingEntryRef,
            bool hadExistingEntry)
        {
            if (!hadExistingEntry)
            {
                return StateLifecycleResult.NoOp(runtimeEvent, descriptor);
            }

            return new StateLifecycleResult(
                runtimeEvent,
                descriptor,
                existingEntryRef,
                StateLifecycleDisposition.Reused,
                hadExistingEntry: true,
                releasedEntryCount: 0);
        }

        private StateLifecycleResult HandleComplete(
            RuntimeLifecycleEvent runtimeEvent,
            StateDescriptor descriptor,
            RuntimeEntryRef existingEntryRef,
            bool hadExistingEntry)
        {
            if (!hadExistingEntry)
            {
                return StateLifecycleResult.NoOp(runtimeEvent, descriptor);
            }

            if (descriptor.Lifetime != StateLifetime.Execution)
            {
                return StateLifecycleResult.NoOp(runtimeEvent, descriptor);
            }

            _deferredReleaseEntries.Add(existingEntryRef);

            return new StateLifecycleResult(
                runtimeEvent,
                descriptor,
                existingEntryRef,
                StateLifecycleDisposition.KeepReadOnlyFinal | StateLifecycleDisposition.DeferredRelease,
                hadExistingEntry: true,
                releasedEntryCount: 0);
        }

        private StateLifecycleResult HandleReleaseOwned(RuntimeLifecycleEvent runtimeEvent, StateDescriptor descriptor)
        {
            var ownerRef = runtimeEvent.OwnerRef;
            RemoveDeferredEntries(descriptor.DomainId, ownerRef);
            var releasedCount = _backend.ReleaseOwnedEntries(descriptor.DomainId, ownerRef);

            return new StateLifecycleResult(
                runtimeEvent,
                descriptor,
                entryRef: null,
                disposition: releasedCount > 0 ? StateLifecycleDisposition.Released : StateLifecycleDisposition.None,
                hadExistingEntry: releasedCount > 0,
                releasedEntryCount: releasedCount);
        }

        private void RemoveDeferredEntries(StateDomainId domainId, StateOwnerRef ownerRef)
        {
            if (_deferredReleaseEntries.Count == 0)
            {
                return;
            }

            var toRemove = new List<RuntimeEntryRef>();
            foreach (var entryRef in _deferredReleaseEntries)
            {
                if (!_backend.TryGetEntry(entryRef, out var entry) || entry is null)
                {
                    toRemove.Add(entryRef);
                    continue;
                }

                if (entry.EntryRef.DomainId == domainId && entry.OwnerRef == ownerRef)
                {
                    toRemove.Add(entryRef);
                }
            }

            for (var index = 0; index < toRemove.Count; index++)
            {
                _deferredReleaseEntries.Remove(toRemove[index]);
            }
        }
    }
}
