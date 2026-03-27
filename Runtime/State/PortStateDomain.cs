#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class PortStateDomain : IPortStateDomain
    {
        public static readonly StateDomainId PortDomainId = new("port");

        private const string ValueDescriptorId = "port.value";
        private const string EmissionDescriptorId = "port.emission";
        private const string ValueSlotKeyPrefix = "value:";
        private const string EmissionSlotKeyPrefix = "emission:";

        private readonly IRuntimeStateBackend _backend;
        private readonly IReactiveStateDomain? _reactiveStateDomain;

        public PortStateDomain(IRuntimeStateBackend backend, IReactiveStateDomain? reactiveStateDomain = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _reactiveStateDomain = reactiveStateDomain;
        }

        public StateDomainId DomainId => PortDomainId;

        public RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey)
        {
            EnsureDescriptorMatchesDomain(descriptor);
            return _backend.EnsureEntry(descriptor, ownerRef, slotKey);
        }

        public bool TryReadValue(PortValueReadRequest request, out PortValueReadResult readResult)
        {
            if (!request.IsValid)
            {
                readResult = PortValueReadResult.Invalid;
                return false;
            }

            if (!_backend.TryLocateEntry(DomainId, request.OwnerRef, CreateValueSlotKey(request.PortKey), out var entryRef)
                || !_backend.TryGetEntry(entryRef, out var entry)
                || entry?.State is not PortValueState state)
            {
                readResult = PortValueReadResult.Invalid;
                return false;
            }

            readResult = new PortValueReadResult(
                request.OwnerRef,
                request.PortKey,
                state.Visibility,
                state.ValueKind,
                state.ValueSummary,
                state.ProducerEntryRef);
            return true;
        }

        public void WriteValue(PortValueWriteRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Port value write request must be valid.", nameof(request));
            }

            var descriptor = CreateValueDescriptor();
            var entryRef = EnsureEntry(descriptor, request.OwnerRef, CreateValueSlotKey(request.PortKey));
            var entry = GetRequiredEntry(entryRef);

            if (entry.State is null)
            {
                entry.State = new PortValueState();
            }
            else if (entry.State is not PortValueState)
            {
                throw CreateStateTypeMismatchException(entryRef, entry.State, typeof(PortValueState));
            }

            var state = (PortValueState)entry.State;
            state.PortKey = request.PortKey;
            state.Visibility = request.Visibility;
            state.ValueKind = request.Value.ValueKind;
            state.ValueSummary = request.Value.ValueSummary;
            state.OpaqueValueRef = request.Value.OpaqueValueRef;
            state.ProducerEntryRef = request.ProducerEntryRef;
            state.Version++;
        }

        public void Emit(PortEmissionRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Port emission request must be valid.", nameof(request));
            }

            var descriptor = CreateEmissionDescriptor();
            var entryRef = EnsureEntry(descriptor, request.OwnerRef, CreateEmissionSlotKey(request.PortKey));
            var entry = GetRequiredEntry(entryRef);

            if (entry.State is null)
            {
                entry.State = new PortEmissionState();
            }
            else if (entry.State is not PortEmissionState)
            {
                throw CreateStateTypeMismatchException(entryRef, entry.State, typeof(PortEmissionState));
            }

            var state = (PortEmissionState)entry.State;
            state.PortKey = request.PortKey;
            state.Visibility = request.Visibility;
            state.PayloadKind = request.Payload.PayloadKind;
            state.PayloadSummary = request.Payload.PayloadSummary;
            state.OpaquePayloadRef = request.Payload.OpaquePayloadRef;
            state.ProducerEntryRef = request.ProducerEntryRef;
            state.EmissionCount++;
            state.LastOccurrenceKey = $"emit.{state.EmissionCount}";
            state.LastSourceRef = CreateEmissionSourceRef(request.OwnerRef, request.PortKey);
            state.LastMatchTokenRef = new MatchTokenRef(
                state.LastSourceRef,
                TokenKind.EmissionOccurrence,
                state.LastOccurrenceKey);

            if (_reactiveStateDomain is not null)
            {
                _reactiveStateDomain.NotifySource(new ReactiveSourceNotification(
                    state.LastSourceRef,
                    state.LastMatchTokenRef.Value));
            }
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

        public static string CreateValueSlotKey(string portKey)
        {
            if (string.IsNullOrWhiteSpace(portKey))
            {
                throw new ArgumentException("Port key cannot be null or whitespace.", nameof(portKey));
            }

            return ValueSlotKeyPrefix + portKey;
        }

        public static string CreateEmissionSlotKey(string portKey)
        {
            if (string.IsNullOrWhiteSpace(portKey))
            {
                throw new ArgumentException("Port key cannot be null or whitespace.", nameof(portKey));
            }

            return EmissionSlotKeyPrefix + portKey;
        }

        public static SourceRef CreateEmissionSourceRef(StateOwnerRef ownerRef, string portKey)
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(portKey))
            {
                throw new ArgumentException("Port key cannot be null or whitespace.", nameof(portKey));
            }

            return new SourceRef(
                PortDomainId.Value,
                SourceKind.PortEmission,
                $"{ownerRef.Kind}:{ownerRef.LogicalKey}:{portKey}",
                ownerRef.RuntimeInstanceId);
        }

        private static StateDescriptor CreateValueDescriptor()
        {
            return new StateDescriptor(
                ValueDescriptorId,
                PortDomainId,
                StateLifetime.Manual,
                debugName: "Port.Value");
        }

        private static StateDescriptor CreateEmissionDescriptor()
        {
            return new StateDescriptor(
                EmissionDescriptorId,
                PortDomainId,
                StateLifetime.Manual,
                debugName: "Port.Emission");
        }

        private static void EnsureDescriptorMatchesDomain(StateDescriptor descriptor)
        {
            if (!descriptor.IsValid)
            {
                throw new ArgumentException("State descriptor must be valid.", nameof(descriptor));
            }

            if (descriptor.DomainId != PortDomainId)
            {
                throw new ArgumentException(
                    $"Port state descriptor must belong to domain '{PortDomainId}', but was '{descriptor.DomainId}'.",
                    nameof(descriptor));
            }
        }

        private RuntimeStateEntryRecord GetRequiredEntry(RuntimeEntryRef entryRef)
        {
            if (!entryRef.IsValid || entryRef.DomainId != DomainId)
            {
                throw new KeyNotFoundException($"Port state entry was not found: {entryRef}");
            }

            if (!_backend.TryGetEntry(entryRef, out var entry) || entry is null)
            {
                throw new KeyNotFoundException($"Port state entry was not found: {entryRef}");
            }

            return entry;
        }

        private static InvalidOperationException CreateStateTypeMismatchException(RuntimeEntryRef entryRef, object state, Type expectedType)
        {
            return new InvalidOperationException(
                $"Port state entry {entryRef} already holds {state.GetType().FullName}, not {expectedType.FullName}.");
        }

        internal sealed class PortValueState
        {
            public string PortKey { get; set; } = string.Empty;

            public PortVisibility Visibility { get; set; }

            public string? ValueKind { get; set; }

            public string? ValueSummary { get; set; }

            public string? OpaqueValueRef { get; set; }

            public RuntimeEntryRef? ProducerEntryRef { get; set; }

            public long Version { get; set; }
        }

        internal sealed class PortEmissionState
        {
            public string PortKey { get; set; } = string.Empty;

            public PortVisibility Visibility { get; set; }

            public string? PayloadKind { get; set; }

            public string? PayloadSummary { get; set; }

            public string? OpaquePayloadRef { get; set; }

            public RuntimeEntryRef? ProducerEntryRef { get; set; }

            public long EmissionCount { get; set; }

            public string? LastOccurrenceKey { get; set; }

            public SourceRef LastSourceRef { get; set; }

            public MatchTokenRef? LastMatchTokenRef { get; set; }
        }
    }
}
