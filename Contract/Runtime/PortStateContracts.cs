#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    public enum PortVisibility
    {
        Unknown = 0,
        Owner = 1,
        Scope = 2,
        Runtime = 3,
    }

    public readonly struct PortValuePayload
    {
        public PortValuePayload(string? valueKind = null, string? valueSummary = null, string? opaqueValueRef = null)
        {
            ValueKind = string.IsNullOrWhiteSpace(valueKind) ? null : valueKind;
            ValueSummary = string.IsNullOrWhiteSpace(valueSummary) ? null : valueSummary;
            OpaqueValueRef = string.IsNullOrWhiteSpace(opaqueValueRef) ? null : opaqueValueRef;
        }

        public string? ValueKind { get; }

        public string? ValueSummary { get; }

        public string? OpaqueValueRef { get; }
    }

    public readonly struct PortEmissionPayload
    {
        public PortEmissionPayload(string? payloadKind = null, string? payloadSummary = null, string? opaquePayloadRef = null)
        {
            PayloadKind = string.IsNullOrWhiteSpace(payloadKind) ? null : payloadKind;
            PayloadSummary = string.IsNullOrWhiteSpace(payloadSummary) ? null : payloadSummary;
            OpaquePayloadRef = string.IsNullOrWhiteSpace(opaquePayloadRef) ? null : opaquePayloadRef;
        }

        public string? PayloadKind { get; }

        public string? PayloadSummary { get; }

        public string? OpaquePayloadRef { get; }
    }

    public readonly struct PortValueReadRequest
    {
        public static PortValueReadRequest Invalid => default;

        public PortValueReadRequest(StateOwnerRef ownerRef, string portKey)
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(portKey))
            {
                throw new ArgumentException("Port key cannot be null or whitespace.", nameof(portKey));
            }

            OwnerRef = ownerRef;
            PortKey = portKey;
        }

        public StateOwnerRef OwnerRef { get; }

        public string PortKey { get; }

        public bool IsValid => OwnerRef.IsValid && !string.IsNullOrWhiteSpace(PortKey);
    }

    public readonly struct PortValueReadResult
    {
        public static PortValueReadResult Invalid => default;

        public PortValueReadResult(
            StateOwnerRef ownerRef,
            string portKey,
            PortVisibility visibility,
            string? valueKind = null,
            string? valueSummary = null,
            RuntimeEntryRef? producedByEntryRef = null)
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(portKey))
            {
                throw new ArgumentException("Port key cannot be null or whitespace.", nameof(portKey));
            }

            if (visibility == PortVisibility.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Port visibility cannot be Unknown.");
            }

            if (producedByEntryRef.HasValue && !producedByEntryRef.Value.IsValid)
            {
                throw new ArgumentException("Produced-by entry ref must be valid when provided.", nameof(producedByEntryRef));
            }

            OwnerRef = ownerRef;
            PortKey = portKey;
            Visibility = visibility;
            ValueKind = string.IsNullOrWhiteSpace(valueKind) ? null : valueKind;
            ValueSummary = string.IsNullOrWhiteSpace(valueSummary) ? null : valueSummary;
            ProducedByEntryRef = producedByEntryRef;
        }

        public StateOwnerRef OwnerRef { get; }

        public string PortKey { get; }

        public PortVisibility Visibility { get; }

        public string? ValueKind { get; }

        public string? ValueSummary { get; }

        public RuntimeEntryRef? ProducedByEntryRef { get; }

        public bool IsValid => OwnerRef.IsValid
            && !string.IsNullOrWhiteSpace(PortKey)
            && Visibility != PortVisibility.Unknown;
    }

    public readonly struct PortValueWriteRequest
    {
        public static PortValueWriteRequest Invalid => default;

        public PortValueWriteRequest(
            StateOwnerRef ownerRef,
            string portKey,
            PortValuePayload value,
            PortVisibility visibility,
            RuntimeEntryRef? producerEntryRef = null)
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(portKey))
            {
                throw new ArgumentException("Port key cannot be null or whitespace.", nameof(portKey));
            }

            if (visibility == PortVisibility.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Port visibility cannot be Unknown.");
            }

            if (producerEntryRef.HasValue && !producerEntryRef.Value.IsValid)
            {
                throw new ArgumentException("Producer entry ref must be valid when provided.", nameof(producerEntryRef));
            }

            OwnerRef = ownerRef;
            PortKey = portKey;
            Value = value;
            Visibility = visibility;
            ProducerEntryRef = producerEntryRef;
        }

        public StateOwnerRef OwnerRef { get; }

        public string PortKey { get; }

        public PortValuePayload Value { get; }

        public PortVisibility Visibility { get; }

        public RuntimeEntryRef? ProducerEntryRef { get; }

        public bool IsValid => OwnerRef.IsValid
            && !string.IsNullOrWhiteSpace(PortKey)
            && Visibility != PortVisibility.Unknown;
    }

    public readonly struct PortEmissionRequest
    {
        public static PortEmissionRequest Invalid => default;

        public PortEmissionRequest(
            StateOwnerRef ownerRef,
            string portKey,
            PortEmissionPayload payload,
            PortVisibility visibility,
            RuntimeEntryRef? producerEntryRef = null)
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(portKey))
            {
                throw new ArgumentException("Port key cannot be null or whitespace.", nameof(portKey));
            }

            if (visibility == PortVisibility.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Port visibility cannot be Unknown.");
            }

            if (producerEntryRef.HasValue && !producerEntryRef.Value.IsValid)
            {
                throw new ArgumentException("Producer entry ref must be valid when provided.", nameof(producerEntryRef));
            }

            OwnerRef = ownerRef;
            PortKey = portKey;
            Payload = payload;
            Visibility = visibility;
            ProducerEntryRef = producerEntryRef;
        }

        public StateOwnerRef OwnerRef { get; }

        public string PortKey { get; }

        public PortEmissionPayload Payload { get; }

        public PortVisibility Visibility { get; }

        public RuntimeEntryRef? ProducerEntryRef { get; }

        public bool IsValid => OwnerRef.IsValid
            && !string.IsNullOrWhiteSpace(PortKey)
            && Visibility != PortVisibility.Unknown;
    }
}
