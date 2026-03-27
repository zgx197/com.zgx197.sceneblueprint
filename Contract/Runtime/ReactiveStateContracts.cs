#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    public enum ReactiveWaitKind
    {
        Unknown = 0,
        Signal = 1,
        Condition = 2,
        CompositeCondition = 3,
        ExternalCallback = 4,
    }

    public enum ResolvePolicy
    {
        Unknown = 0,
        Any = 1,
        All = 2,
    }

    public enum SourceKind
    {
        Unknown = 0,
        Signal = 1,
        Condition = 2,
        PortEmission = 3,
        ExternalCallback = 4,
    }

    public enum TokenKind
    {
        Unknown = 0,
        SignalOccurrence = 1,
        ConditionOccurrence = 2,
        EmissionOccurrence = 3,
        CallbackOccurrence = 4,
    }

    public enum ResumeKind
    {
        Unknown = 0,
        ContinueExecution = 1,
        ConsumeMatch = 2,
        ResumeBranch = 3,
    }

    public readonly struct SourceRef : IEquatable<SourceRef>
    {
        public static SourceRef Invalid => default;

        public SourceRef(string refDomain, SourceKind sourceKind, string logicalKey, string? runtimeInstanceId = null)
        {
            if (string.IsNullOrWhiteSpace(refDomain))
            {
                throw new ArgumentException("Ref domain cannot be null or whitespace.", nameof(refDomain));
            }

            if (sourceKind == SourceKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Source kind cannot be Unknown.");
            }

            if (string.IsNullOrWhiteSpace(logicalKey))
            {
                throw new ArgumentException("Logical key cannot be null or whitespace.", nameof(logicalKey));
            }

            RefDomain = refDomain;
            SourceKind = sourceKind;
            LogicalKey = logicalKey;
            RuntimeInstanceId = string.IsNullOrWhiteSpace(runtimeInstanceId) ? null : runtimeInstanceId;
        }

        public string RefDomain { get; }

        public SourceKind SourceKind { get; }

        public string LogicalKey { get; }

        public string? RuntimeInstanceId { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(RefDomain)
            && SourceKind != SourceKind.Unknown
            && !string.IsNullOrWhiteSpace(LogicalKey);

        public bool Equals(SourceRef other)
        {
            return string.Equals(RefDomain, other.RefDomain, StringComparison.Ordinal)
                && SourceKind == other.SourceKind
                && string.Equals(LogicalKey, other.LogicalKey, StringComparison.Ordinal)
                && string.Equals(RuntimeInstanceId, other.RuntimeInstanceId, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is SourceRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(RefDomain ?? string.Empty),
                (int)SourceKind,
                StringComparer.Ordinal.GetHashCode(LogicalKey ?? string.Empty),
                StringComparer.Ordinal.GetHashCode(RuntimeInstanceId ?? string.Empty));
        }

        public override string ToString()
        {
            return RuntimeInstanceId is null
                ? $"{RefDomain}:{SourceKind}:{LogicalKey}"
                : $"{RefDomain}:{SourceKind}:{LogicalKey}@{RuntimeInstanceId}";
        }

        public static bool operator ==(SourceRef left, SourceRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SourceRef left, SourceRef right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct MatchTokenRef : IEquatable<MatchTokenRef>
    {
        public static MatchTokenRef Invalid => default;

        public MatchTokenRef(SourceRef sourceRef, TokenKind tokenKind, string occurrenceKey)
        {
            if (!sourceRef.IsValid)
            {
                throw new ArgumentException("Source ref must be valid.", nameof(sourceRef));
            }

            if (tokenKind == TokenKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(tokenKind), tokenKind, "Token kind cannot be Unknown.");
            }

            if (string.IsNullOrWhiteSpace(occurrenceKey))
            {
                throw new ArgumentException("Occurrence key cannot be null or whitespace.", nameof(occurrenceKey));
            }

            SourceRef = sourceRef;
            TokenKind = tokenKind;
            OccurrenceKey = occurrenceKey;
        }

        public SourceRef SourceRef { get; }

        public TokenKind TokenKind { get; }

        public string OccurrenceKey { get; }

        public bool IsValid => SourceRef.IsValid && TokenKind != TokenKind.Unknown && !string.IsNullOrWhiteSpace(OccurrenceKey);

        public bool Equals(MatchTokenRef other)
        {
            return SourceRef.Equals(other.SourceRef)
                && TokenKind == other.TokenKind
                && string.Equals(OccurrenceKey, other.OccurrenceKey, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is MatchTokenRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SourceRef, (int)TokenKind, StringComparer.Ordinal.GetHashCode(OccurrenceKey ?? string.Empty));
        }

        public override string ToString()
        {
            return IsValid ? $"{TokenKind}:{OccurrenceKey}" : string.Empty;
        }

        public static bool operator ==(MatchTokenRef left, MatchTokenRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MatchTokenRef left, MatchTokenRef right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ResumeContextRef
    {
        public static ResumeContextRef Invalid => default;

        public ResumeContextRef(StateOwnerRef ownerRef, ResumeKind resumeKind, string continuationKey, IReadOnlyList<RuntimeEntryRef>? capturedRefs = null)
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (resumeKind == ResumeKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(resumeKind), resumeKind, "Resume kind cannot be Unknown.");
            }

            if (string.IsNullOrWhiteSpace(continuationKey))
            {
                throw new ArgumentException("Continuation key cannot be null or whitespace.", nameof(continuationKey));
            }

            OwnerRef = ownerRef;
            ResumeKind = resumeKind;
            ContinuationKey = continuationKey;
            CapturedRefs = capturedRefs;
        }

        public StateOwnerRef OwnerRef { get; }

        public ResumeKind ResumeKind { get; }

        public string ContinuationKey { get; }

        public IReadOnlyList<RuntimeEntryRef>? CapturedRefs { get; }

        public bool IsValid => OwnerRef.IsValid && ResumeKind != ResumeKind.Unknown && !string.IsNullOrWhiteSpace(ContinuationKey);
    }

    public readonly struct ReactiveWaitRequest
    {
        public static ReactiveWaitRequest Invalid => default;

        public ReactiveWaitRequest(
            StateOwnerRef ownerRef,
            string slotKey,
            ReactiveWaitKind waitKind,
            StateLifetime lifetime,
            ResolvePolicy resolvePolicy,
            ResumeContextRef? resumeContextRef = null)
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("Slot key cannot be null or whitespace.", nameof(slotKey));
            }

            if (waitKind == ReactiveWaitKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(waitKind), waitKind, "Wait kind cannot be Unknown.");
            }

            if (lifetime == StateLifetime.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "State lifetime cannot be Unknown.");
            }

            if (resolvePolicy == ResolvePolicy.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(resolvePolicy), resolvePolicy, "Resolve policy cannot be Unknown.");
            }

            if (resumeContextRef.HasValue && !resumeContextRef.Value.IsValid)
            {
                throw new ArgumentException("Resume context ref must be valid when provided.", nameof(resumeContextRef));
            }

            OwnerRef = ownerRef;
            SlotKey = slotKey;
            WaitKind = waitKind;
            Lifetime = lifetime;
            ResolvePolicy = resolvePolicy;
            ResumeContextRef = resumeContextRef;
        }

        public StateOwnerRef OwnerRef { get; }

        public string SlotKey { get; }

        public ReactiveWaitKind WaitKind { get; }

        public StateLifetime Lifetime { get; }

        public ResolvePolicy ResolvePolicy { get; }

        public ResumeContextRef? ResumeContextRef { get; }

        public bool IsValid => OwnerRef.IsValid
            && !string.IsNullOrWhiteSpace(SlotKey)
            && WaitKind != ReactiveWaitKind.Unknown
            && Lifetime != StateLifetime.Unknown
            && ResolvePolicy != ResolvePolicy.Unknown;
    }

    public readonly struct ReactiveSubscriptionRequest
    {
        public static ReactiveSubscriptionRequest Invalid => default;

        public ReactiveSubscriptionRequest(RuntimeEntryRef waitEntryRef, string bindingKey, SourceKind sourceKind, SourceRef sourceRef)
        {
            if (!waitEntryRef.IsValid)
            {
                throw new ArgumentException("Wait entry ref must be valid.", nameof(waitEntryRef));
            }

            if (string.IsNullOrWhiteSpace(bindingKey))
            {
                throw new ArgumentException("Binding key cannot be null or whitespace.", nameof(bindingKey));
            }

            if (sourceKind == SourceKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Source kind cannot be Unknown.");
            }

            if (!sourceRef.IsValid)
            {
                throw new ArgumentException("Source ref must be valid.", nameof(sourceRef));
            }

            if (sourceRef.SourceKind != sourceKind)
            {
                throw new ArgumentException("Source kind must match source ref kind.", nameof(sourceKind));
            }

            WaitEntryRef = waitEntryRef;
            BindingKey = bindingKey;
            SourceKind = sourceKind;
            SourceRef = sourceRef;
        }

        public RuntimeEntryRef WaitEntryRef { get; }

        public string BindingKey { get; }

        public SourceKind SourceKind { get; }

        public SourceRef SourceRef { get; }

        public bool IsValid => WaitEntryRef.IsValid
            && !string.IsNullOrWhiteSpace(BindingKey)
            && SourceKind != SourceKind.Unknown
            && SourceRef.IsValid;
    }

    public readonly struct ReactiveSourceNotification
    {
        public static ReactiveSourceNotification Invalid => default;

        public ReactiveSourceNotification(SourceRef sourceRef, MatchTokenRef matchTokenRef)
        {
            if (!sourceRef.IsValid)
            {
                throw new ArgumentException("Source ref must be valid.", nameof(sourceRef));
            }

            if (!matchTokenRef.IsValid)
            {
                throw new ArgumentException("Match token ref must be valid.", nameof(matchTokenRef));
            }

            if (matchTokenRef.SourceRef != sourceRef)
            {
                throw new ArgumentException("Match token ref must belong to the same source ref.", nameof(matchTokenRef));
            }

            SourceRef = sourceRef;
            MatchTokenRef = matchTokenRef;
        }

        public SourceRef SourceRef { get; }

        public MatchTokenRef MatchTokenRef { get; }

        public bool IsValid => SourceRef.IsValid && MatchTokenRef.IsValid;
    }
}
