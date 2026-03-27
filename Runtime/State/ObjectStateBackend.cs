#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class ObjectStateBackend : IRuntimeStateBackend
    {
        private readonly Dictionary<RuntimeEntryRef, RuntimeStateEntryRecord> _entriesByRef = new();
        private readonly Dictionary<BackendEntryKey, RuntimeEntryRef> _entryRefsByKey = new();
        private int _nextEntrySequence = 1;

        public RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey)
        {
            ValidateEnsureArguments(descriptor, ownerRef, slotKey);

            var key = new BackendEntryKey(descriptor.DomainId, ownerRef, slotKey);
            if (_entryRefsByKey.TryGetValue(key, out var existingEntryRef))
            {
                return existingEntryRef;
            }

            var entryRef = new RuntimeEntryRef(descriptor.DomainId, CreateEntryId(descriptor));
            var entry = new RuntimeStateEntryRecord(entryRef, descriptor, ownerRef, slotKey);

            _entryRefsByKey[key] = entryRef;
            _entriesByRef[entryRef] = entry;
            return entryRef;
        }

        public bool TryLocateEntry(StateDomainId domainId, StateOwnerRef ownerRef, string slotKey, out RuntimeEntryRef entryRef)
        {
            if (!domainId.IsValid || !ownerRef.IsValid || string.IsNullOrWhiteSpace(slotKey))
            {
                entryRef = RuntimeEntryRef.Invalid;
                return false;
            }

            return _entryRefsByKey.TryGetValue(new BackendEntryKey(domainId, ownerRef, slotKey), out entryRef);
        }

        public bool TryGetEntry(RuntimeEntryRef entryRef, out RuntimeStateEntryRecord? entry)
        {
            if (!entryRef.IsValid)
            {
                entry = null;
                return false;
            }

            return _entriesByRef.TryGetValue(entryRef, out entry);
        }

        public bool ReleaseEntry(RuntimeEntryRef entryRef)
        {
            if (!TryGetEntry(entryRef, out var entry) || entry is null)
            {
                return false;
            }

            _entriesByRef.Remove(entryRef);
            _entryRefsByKey.Remove(new BackendEntryKey(entryRef.DomainId, entry.OwnerRef, entry.SlotKey));
            return true;
        }

        public int ReleaseOwnedEntries(StateDomainId domainId, StateOwnerRef ownerRef)
        {
            if (!domainId.IsValid || !ownerRef.IsValid)
            {
                return 0;
            }

            var toRelease = new List<RuntimeEntryRef>();
            foreach (var pair in _entriesByRef)
            {
                var entry = pair.Value;
                if (entry.EntryRef.DomainId == domainId && entry.OwnerRef == ownerRef)
                {
                    toRelease.Add(pair.Key);
                }
            }

            for (int index = 0; index < toRelease.Count; index++)
            {
                ReleaseEntry(toRelease[index]);
            }

            return toRelease.Count;
        }

        public IReadOnlyList<RuntimeEntryRef> EnumerateEntries(StateDomainId domainId, ObservationFilter? filter = null)
        {
            if (!domainId.IsValid)
            {
                return Array.Empty<RuntimeEntryRef>();
            }

            var result = new List<RuntimeEntryRef>();
            foreach (var pair in _entriesByRef)
            {
                var entry = pair.Value;
                if (entry.EntryRef.DomainId != domainId)
                {
                    continue;
                }

                if (!MatchesFilter(entry, filter))
                {
                    continue;
                }

                result.Add(pair.Key);
            }

            result.Sort(static (left, right) => string.CompareOrdinal(left.EntryId, right.EntryId));
            return result;
        }

        public int Clear()
        {
            var count = _entriesByRef.Count;
            _entriesByRef.Clear();
            _entryRefsByKey.Clear();
            _nextEntrySequence = 1;
            return count;
        }

        public bool TryGetState(RuntimeEntryRef entryRef, out object? state)
        {
            if (TryGetEntry(entryRef, out var entry) && entry is not null)
            {
                state = entry.State;
                return true;
            }

            state = null;
            return false;
        }

        public bool TryGetState<TState>(RuntimeEntryRef entryRef, out TState? state)
            where TState : class
        {
            if (TryGetEntry(entryRef, out var entry) && entry?.State is TState typedState)
            {
                state = typedState;
                return true;
            }

            state = null;
            return false;
        }

        public void SetState(RuntimeEntryRef entryRef, object? state)
        {
            if (!TryGetEntry(entryRef, out var entry) || entry is null)
            {
                throw new KeyNotFoundException($"Runtime state entry was not found: {entryRef}");
            }

            entry.State = state;
        }

        public TState GetOrCreateState<TState>(RuntimeEntryRef entryRef, Func<TState> stateFactory)
            where TState : class
        {
            if (stateFactory is null)
            {
                throw new ArgumentNullException(nameof(stateFactory));
            }

            if (!TryGetEntry(entryRef, out var entry) || entry is null)
            {
                throw new KeyNotFoundException($"Runtime state entry was not found: {entryRef}");
            }

            if (entry.State is TState existingState)
            {
                return existingState;
            }

            if (entry.State is not null)
            {
                throw new InvalidOperationException(
                    $"Runtime state entry {entryRef} already holds {entry.State.GetType().FullName}, not {typeof(TState).FullName}.");
            }

            var createdState = stateFactory();
            if (createdState is null)
            {
                throw new InvalidOperationException($"State factory returned null for entry {entryRef}.");
            }

            entry.State = createdState;
            return createdState;
        }

        private static bool MatchesFilter(RuntimeStateEntryRecord entry, ObservationFilter? filter)
        {
            if (!filter.HasValue)
            {
                return true;
            }

            var filterValue = filter.Value;
            if (filterValue.OwnerRef.HasValue && entry.OwnerRef != filterValue.OwnerRef.Value)
            {
                return false;
            }

            return true;
        }

        private static void ValidateEnsureArguments(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey)
        {
            if (!descriptor.IsValid)
            {
                throw new ArgumentException("Descriptor must be valid.", nameof(descriptor));
            }

            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("Slot key cannot be null or whitespace.", nameof(slotKey));
            }
        }

        private string CreateEntryId(StateDescriptor descriptor)
        {
            return $"{descriptor.Id}:{_nextEntrySequence++}";
        }

        private readonly struct BackendEntryKey : IEquatable<BackendEntryKey>
        {
            public BackendEntryKey(StateDomainId domainId, StateOwnerRef ownerRef, string slotKey)
            {
                DomainId = domainId;
                OwnerRef = ownerRef;
                SlotKey = slotKey;
            }

            public StateDomainId DomainId { get; }

            public StateOwnerRef OwnerRef { get; }

            public string SlotKey { get; }

            public bool Equals(BackendEntryKey other)
            {
                return DomainId == other.DomainId
                    && OwnerRef == other.OwnerRef
                    && string.Equals(SlotKey, other.SlotKey, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is BackendEntryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(DomainId, OwnerRef, StringComparer.Ordinal.GetHashCode(SlotKey ?? string.Empty));
            }
        }
    }
}
