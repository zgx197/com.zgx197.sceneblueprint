#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public enum EventHistoryRecordKind
    {
        Unknown = 0,
        Emit = 1,
        Inject = 2,
    }

    [Serializable]
    public sealed class BlueprintEventHistoryState
    {
        public long Sequence { get; set; }

        public EventHistoryRecordKind RecordKind { get; set; }

        public string EventKind { get; set; } = string.Empty;

        public string ActionId { get; set; } = string.Empty;

        public int ActionIndex { get; set; } = -1;

        public int Tick { get; set; }

        public string SignalTag { get; set; } = string.Empty;

        public string SubjectRefSerialized { get; set; } = string.Empty;

        public string SubjectSummary { get; set; } = string.Empty;

        public string InstigatorRefSerialized { get; set; } = string.Empty;

        public string InstigatorSummary { get; set; } = string.Empty;

        public string TargetRefSerialized { get; set; } = string.Empty;

        public string TargetSummary { get; set; } = string.Empty;

        public string PayloadSummary { get; set; } = string.Empty;

        public BlueprintEventContext EventContext { get; set; } = new();
    }

    public sealed class EventHistoryProjectedEntry
    {
        public EventHistoryProjectedEntry(
            RuntimeEntryRef entryRef,
            string slotKey,
            BlueprintEventHistoryState state,
            EventHistoryProjectionModel projection)
        {
            if (!entryRef.IsValid)
            {
                throw new ArgumentException("Entry ref must be valid.", nameof(entryRef));
            }

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("Slot key cannot be null or whitespace.", nameof(slotKey));
            }

            EntryRef = entryRef;
            SlotKey = slotKey;
            State = state ?? throw new ArgumentNullException(nameof(state));
            Projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }

        public RuntimeEntryRef EntryRef { get; }

        public string SlotKey { get; }

        public BlueprintEventHistoryState State { get; }

        public EventHistoryProjectionModel Projection { get; }

        public string LocatorText => Projection.LocatorText;
    }

    public sealed class EventHistoryStateDomain : IRuntimeStateDomain
    {
        public static readonly StateDomainId EventHistoryDomainId = new("event.history");
        public static readonly StateOwnerRef EventHistoryOwnerRef = new(OwnerKind.Runtime, "event.history");
        public static readonly StateDescriptor EventHistoryDescriptor = new(
            "event.history.entry",
            EventHistoryDomainId,
            StateLifetime.RuntimePersistent,
            debugName: "Event.History Entry",
            allowSnapshot: true);

        private const string SlotKeyPrefix = "event";

        private readonly IRuntimeStateBackend _backend;
        private readonly Queue<string> _slotOrder = new();
        private readonly HashSet<string> _trackedSlots = new(StringComparer.Ordinal);
        private long _nextSequence;

        public EventHistoryStateDomain(IRuntimeStateBackend backend, int capacity = 128)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
            }

            Capacity = capacity;
        }

        public StateDomainId DomainId => EventHistoryDomainId;

        public int Capacity { get; }

        public long NextSequence => _nextSequence + 1;

        public RuntimeEntryRef RecordEmitted(BlueprintEventContext eventContext)
        {
            return Record(EventHistoryRecordKind.Emit, eventContext);
        }

        public RuntimeEntryRef RecordInjected(BlueprintEventContext eventContext)
        {
            return Record(EventHistoryRecordKind.Inject, eventContext);
        }

        public RuntimeEntryRef Import(BlueprintEventHistoryState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var normalizedEventContext = BlueprintEventContextSemanticUtility.Normalize(state.EventContext) ?? new BlueprintEventContext();
            var sequence = state.Sequence > 0 ? state.Sequence : NextSequence;
            var normalizedState = new BlueprintEventHistoryState
            {
                Sequence = sequence,
                RecordKind = state.RecordKind == EventHistoryRecordKind.Unknown
                    ? InferRecordKind(normalizedEventContext.EventKind)
                    : state.RecordKind,
                EventKind = SelectFirstNonEmpty(state.EventKind, normalizedEventContext.EventKind),
                ActionId = SelectFirstNonEmpty(state.ActionId, normalizedEventContext.ActionId),
                ActionIndex = state.ActionIndex >= 0 ? state.ActionIndex : normalizedEventContext.ActionIndex,
                Tick = state.Tick != 0 ? state.Tick : normalizedEventContext.Tick,
                SignalTag = SelectFirstNonEmpty(state.SignalTag, normalizedEventContext.SignalTag),
                SubjectRefSerialized = SelectFirstNonEmpty(state.SubjectRefSerialized, normalizedEventContext.SubjectRefSerialized),
                SubjectSummary = SelectFirstNonEmpty(state.SubjectSummary, normalizedEventContext.SubjectSummary),
                InstigatorRefSerialized = SelectFirstNonEmpty(state.InstigatorRefSerialized, normalizedEventContext.InstigatorRefSerialized),
                InstigatorSummary = SelectFirstNonEmpty(state.InstigatorSummary, normalizedEventContext.InstigatorSummary),
                TargetRefSerialized = SelectFirstNonEmpty(state.TargetRefSerialized, normalizedEventContext.TargetRefSerialized),
                TargetSummary = SelectFirstNonEmpty(state.TargetSummary, normalizedEventContext.TargetSummary),
                PayloadSummary = SelectFirstNonEmpty(state.PayloadSummary, normalizedEventContext.PayloadSummary),
                EventContext = normalizedEventContext,
            };

            return UpsertState(normalizedState);
        }

        public RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey)
        {
            EnsureDescriptorMatchesDomain(descriptor);
            return _backend.EnsureEntry(descriptor, ownerRef, slotKey);
        }

        public bool TryLocateEntry(StateOwnerRef ownerRef, string slotKey, out RuntimeEntryRef entryRef)
        {
            return _backend.TryLocateEntry(DomainId, ownerRef, slotKey, out entryRef);
        }

        public bool TryLocateEntry(long sequence, out RuntimeEntryRef entryRef)
        {
            if (sequence <= 0)
            {
                entryRef = default;
                return false;
            }

            return TryLocateEntry(EventHistoryOwnerRef, CreateSlotKey(sequence), out entryRef);
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

        public IReadOnlyList<RuntimeEntryRef> QueryEntries(EventHistoryQuery? query = null, ObservationFilter? filter = null)
        {
            var matchedEntries = new List<(RuntimeEntryRef EntryRef, long Sequence)>();
            var entries = _backend.EnumerateEntries(DomainId, filter);
            for (var index = 0; index < entries.Count; index++)
            {
                var entryRef = entries[index];
                if (!_backend.TryGetEntry(entryRef, out var entry)
                    || entry?.State is not BlueprintEventHistoryState state
                    || !EventHistoryQueryUtility.Matches(state, query))
                {
                    continue;
                }

                matchedEntries.Add((entryRef, state.Sequence));
            }

            var newestFirst = query?.NewestFirst ?? true;
            matchedEntries.Sort((left, right) =>
                newestFirst
                    ? right.Sequence.CompareTo(left.Sequence)
                    : left.Sequence.CompareTo(right.Sequence));

            var limit = query?.Limit ?? 0;
            if (limit > 0 && matchedEntries.Count > limit)
            {
                matchedEntries.RemoveRange(limit, matchedEntries.Count - limit);
            }

            var results = new List<RuntimeEntryRef>(matchedEntries.Count);
            for (var index = 0; index < matchedEntries.Count; index++)
            {
                results.Add(matchedEntries[index].EntryRef);
            }

            return results;
        }

        public IReadOnlyList<BlueprintEventHistoryState> ReadHistory(EventHistoryQuery? query = null, ObservationFilter? filter = null)
        {
            var entryRefs = QueryEntries(query, filter);
            var states = new List<BlueprintEventHistoryState>(entryRefs.Count);
            for (var index = 0; index < entryRefs.Count; index++)
            {
                if (_backend.TryGetEntry(entryRefs[index], out var entry)
                    && entry?.State is BlueprintEventHistoryState state)
                {
                    states.Add(state);
                }
            }

            return states;
        }

        public IReadOnlyList<EventHistoryProjectionModel> ReadProjectedHistory(EventHistoryQuery? query = null, ObservationFilter? filter = null)
        {
            var entries = ReadProjectedEntries(query, filter);
            var projections = new List<EventHistoryProjectionModel>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                projections.Add(entries[index].Projection);
            }

            return projections;
        }

        public IReadOnlyList<EventHistoryProjectedEntry> ReadProjectedEntries(EventHistoryQuery? query = null, ObservationFilter? filter = null)
        {
            var entryRefs = QueryEntries(query, filter);
            var projections = new List<EventHistoryProjectedEntry>(entryRefs.Count);
            for (var index = 0; index < entryRefs.Count; index++)
            {
                if (!_backend.TryGetEntry(entryRefs[index], out var entry)
                    || entry?.State is not BlueprintEventHistoryState state)
                {
                    continue;
                }

                projections.Add(new EventHistoryProjectedEntry(
                    entryRefs[index],
                    entry.SlotKey,
                    state,
                    EventHistoryProjectionUtility.Build(state)));
            }

            return projections;
        }

        public static string CreateSlotKey(long sequence)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must be greater than zero.");
            }

            return $"{SlotKeyPrefix}.{sequence:D8}";
        }

        private RuntimeEntryRef Record(EventHistoryRecordKind recordKind, BlueprintEventContext eventContext)
        {
            if (eventContext == null)
            {
                throw new ArgumentNullException(nameof(eventContext));
            }

            var normalizedEventContext = BlueprintEventContextSemanticUtility.Normalize(eventContext) ?? new BlueprintEventContext();
            var state = new BlueprintEventHistoryState
            {
                Sequence = NextSequence,
                RecordKind = recordKind,
                EventKind = normalizedEventContext.EventKind,
                ActionId = normalizedEventContext.ActionId,
                ActionIndex = normalizedEventContext.ActionIndex,
                Tick = normalizedEventContext.Tick,
                SignalTag = normalizedEventContext.SignalTag,
                SubjectRefSerialized = normalizedEventContext.SubjectRefSerialized,
                SubjectSummary = normalizedEventContext.SubjectSummary,
                InstigatorRefSerialized = normalizedEventContext.InstigatorRefSerialized,
                InstigatorSummary = normalizedEventContext.InstigatorSummary,
                TargetRefSerialized = normalizedEventContext.TargetRefSerialized,
                TargetSummary = normalizedEventContext.TargetSummary,
                PayloadSummary = normalizedEventContext.PayloadSummary,
                EventContext = normalizedEventContext,
            };

            return UpsertState(state);
        }

        private RuntimeEntryRef UpsertState(BlueprintEventHistoryState state)
        {
            var slotKey = CreateSlotKey(state.Sequence);
            var entryRef = EnsureEntry(EventHistoryDescriptor, EventHistoryOwnerRef, slotKey);
            if (!_backend.TryGetEntry(entryRef, out var entry) || entry == null)
            {
                throw new InvalidOperationException($"Event history entry could not be read after creation: {entryRef}");
            }

            entry.State = state;
            TrackSlot(slotKey);
            _nextSequence = Math.Max(_nextSequence, state.Sequence);
            TrimToCapacity();
            return entryRef;
        }

        private void TrackSlot(string slotKey)
        {
            if (!_trackedSlots.Add(slotKey))
            {
                return;
            }

            _slotOrder.Enqueue(slotKey);
        }

        private void TrimToCapacity()
        {
            while (_slotOrder.Count > Capacity)
            {
                var oldestSlotKey = _slotOrder.Dequeue();
                _trackedSlots.Remove(oldestSlotKey);
                if (_backend.TryLocateEntry(DomainId, EventHistoryOwnerRef, oldestSlotKey, out var oldestEntryRef))
                {
                    _backend.ReleaseEntry(oldestEntryRef);
                }
            }
        }

        private static EventHistoryRecordKind InferRecordKind(string? eventKind)
        {
            return eventKind != null && eventKind.IndexOf("Inject", StringComparison.OrdinalIgnoreCase) >= 0
                ? EventHistoryRecordKind.Inject
                : EventHistoryRecordKind.Emit;
        }

        private static string SelectFirstNonEmpty(string primary, string fallback)
        {
            return string.IsNullOrWhiteSpace(primary) ? (fallback ?? string.Empty) : primary;
        }

        private static void EnsureDescriptorMatchesDomain(StateDescriptor descriptor)
        {
            if (!descriptor.IsValid)
            {
                throw new ArgumentException("State descriptor must be valid.", nameof(descriptor));
            }

            if (descriptor.DomainId != EventHistoryDomainId)
            {
                throw new ArgumentException(
                    $"Event history descriptor must belong to domain '{EventHistoryDomainId}', but was '{descriptor.DomainId}'.",
                    nameof(descriptor));
            }
        }
    }
}
