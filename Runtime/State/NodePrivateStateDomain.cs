#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class NodePrivateStateDomain : IRuntimeStateDomain
    {
        public static readonly StateDomainId NodePrivateDomainId = new("node.private");

        private readonly IRuntimeStateBackend _backend;

        public NodePrivateStateDomain(IRuntimeStateBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public StateDomainId DomainId => NodePrivateDomainId;

        public RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey)
        {
            EnsureDescriptorMatchesDomain(descriptor);
            return _backend.EnsureEntry(descriptor, ownerRef, slotKey);
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

        public TState GetOrCreateState<TState>(StateOwnerRef ownerRef, NodeStateDescriptor<TState> descriptor, string? slotKey = null)
            where TState : class
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var resolvedSlotKey = ResolveSlotKey(descriptor.DefaultSlotKey, slotKey);
            var entryRef = EnsureEntry(descriptor.Descriptor, ownerRef, resolvedSlotKey);

            if (!_backend.TryGetEntry(entryRef, out var entry) || entry is null)
            {
                throw new InvalidOperationException($"Runtime state entry could not be read after creation: {entryRef}");
            }

            if (entry.State is TState typedState)
            {
                return typedState;
            }

            if (entry.State is not null)
            {
                throw new InvalidOperationException(
                    $"Node private state entry {entryRef} already holds {entry.State.GetType().FullName}, not {typeof(TState).FullName}.");
            }

            var created = descriptor.Factory();
            if (created is null)
            {
                throw new InvalidOperationException($"Node state factory returned null for entry {entryRef}.");
            }

            entry.State = created;
            return created;
        }

        public bool TryGetState<TState>(StateOwnerRef ownerRef, NodeStateDescriptor<TState> descriptor, out TState? state, string? slotKey = null)
            where TState : class
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (!ownerRef.IsValid)
            {
                state = null;
                return false;
            }

            var resolvedSlotKey = ResolveSlotKey(descriptor.DefaultSlotKey, slotKey);
            if (!_backend.TryLocateEntry(DomainId, ownerRef, resolvedSlotKey, out var entryRef))
            {
                state = null;
                return false;
            }

            if (_backend.TryGetEntry(entryRef, out var entry) && entry?.State is TState typedState)
            {
                state = typedState;
                return true;
            }

            state = null;
            return false;
        }

        public bool RemoveState<TState>(StateOwnerRef ownerRef, NodeStateDescriptor<TState> descriptor, string? slotKey = null)
            where TState : class
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (!ownerRef.IsValid)
            {
                return false;
            }

            var resolvedSlotKey = ResolveSlotKey(descriptor.DefaultSlotKey, slotKey);
            return _backend.TryLocateEntry(DomainId, ownerRef, resolvedSlotKey, out var entryRef)
                && _backend.ReleaseEntry(entryRef);
        }

        private static string ResolveSlotKey(string defaultSlotKey, string? slotKey)
        {
            return string.IsNullOrWhiteSpace(slotKey) ? defaultSlotKey : slotKey;
        }

        private static void EnsureDescriptorMatchesDomain(StateDescriptor descriptor)
        {
            if (!descriptor.IsValid)
            {
                throw new ArgumentException("State descriptor must be valid.", nameof(descriptor));
            }

            if (descriptor.DomainId != NodePrivateDomainId)
            {
                throw new ArgumentException(
                    $"Node private state descriptor must belong to domain '{NodePrivateDomainId}', but was '{descriptor.DomainId}'.",
                    nameof(descriptor));
            }
        }
    }
}
