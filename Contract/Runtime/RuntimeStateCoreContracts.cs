#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    public enum OwnerKind
    {
        Unknown = 0,
        Action = 1,
        Scope = 2,
        Runtime = 3,
    }

    public enum StateLifetime
    {
        Unknown = 0,
        Execution = 1,
        ActionPersistent = 2,
        ScopePersistent = 3,
        RuntimePersistent = 4,
        Manual = 5,
    }

    public readonly struct StateDomainId : IEquatable<StateDomainId>
    {
        public static StateDomainId Invalid => default;

        public StateDomainId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Domain id cannot be null or whitespace.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(StateDomainId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is StateDomainId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(StateDomainId left, StateDomainId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StateDomainId left, StateDomainId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct RuntimeTick : IEquatable<RuntimeTick>, IComparable<RuntimeTick>
    {
        public RuntimeTick(long value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Runtime tick cannot be negative.");
            }

            Value = value;
        }

        public long Value { get; }

        public int CompareTo(RuntimeTick other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(RuntimeTick other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is RuntimeTick other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(RuntimeTick left, RuntimeTick right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeTick left, RuntimeTick right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(RuntimeTick left, RuntimeTick right)
        {
            return left.Value < right.Value;
        }

        public static bool operator <=(RuntimeTick left, RuntimeTick right)
        {
            return left.Value <= right.Value;
        }

        public static bool operator >(RuntimeTick left, RuntimeTick right)
        {
            return left.Value > right.Value;
        }

        public static bool operator >=(RuntimeTick left, RuntimeTick right)
        {
            return left.Value >= right.Value;
        }
    }

    public readonly struct StateOwnerRef : IEquatable<StateOwnerRef>
    {
        public static StateOwnerRef Invalid => default;

        public StateOwnerRef(OwnerKind kind, string logicalKey, string? runtimeInstanceId = null)
        {
            if (kind == OwnerKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Owner kind cannot be Unknown.");
            }

            if (string.IsNullOrWhiteSpace(logicalKey))
            {
                throw new ArgumentException("Logical key cannot be null or whitespace.", nameof(logicalKey));
            }

            Kind = kind;
            LogicalKey = logicalKey;
            RuntimeInstanceId = string.IsNullOrWhiteSpace(runtimeInstanceId) ? null : runtimeInstanceId;
        }

        public OwnerKind Kind { get; }

        public string LogicalKey { get; }

        public string? RuntimeInstanceId { get; }

        public bool IsValid => Kind != OwnerKind.Unknown && !string.IsNullOrWhiteSpace(LogicalKey);

        public bool Equals(StateOwnerRef other)
        {
            return Kind == other.Kind
                && string.Equals(LogicalKey, other.LogicalKey, StringComparison.Ordinal)
                && string.Equals(RuntimeInstanceId, other.RuntimeInstanceId, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is StateOwnerRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                (int)Kind,
                StringComparer.Ordinal.GetHashCode(LogicalKey ?? string.Empty),
                StringComparer.Ordinal.GetHashCode(RuntimeInstanceId ?? string.Empty));
        }

        public override string ToString()
        {
            return RuntimeInstanceId is null
                ? $"{Kind}:{LogicalKey}"
                : $"{Kind}:{LogicalKey}@{RuntimeInstanceId}";
        }

        public static bool operator ==(StateOwnerRef left, StateOwnerRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StateOwnerRef left, StateOwnerRef right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct RuntimeEntryRef : IEquatable<RuntimeEntryRef>
    {
        public static RuntimeEntryRef Invalid => default;

        public RuntimeEntryRef(StateDomainId domainId, string entryId)
        {
            if (!domainId.IsValid)
            {
                throw new ArgumentException("Domain id must be valid.", nameof(domainId));
            }

            if (string.IsNullOrWhiteSpace(entryId))
            {
                throw new ArgumentException("Entry id cannot be null or whitespace.", nameof(entryId));
            }

            DomainId = domainId;
            EntryId = entryId;
        }

        public RuntimeEntryRef(string domainId, string entryId)
            : this(new StateDomainId(domainId), entryId)
        {
        }

        public StateDomainId DomainId { get; }

        public string EntryId { get; }

        public bool IsValid => DomainId.IsValid && !string.IsNullOrWhiteSpace(EntryId);

        public bool Equals(RuntimeEntryRef other)
        {
            return DomainId == other.DomainId
                && string.Equals(EntryId, other.EntryId, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is RuntimeEntryRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DomainId, StringComparer.Ordinal.GetHashCode(EntryId ?? string.Empty));
        }

        public override string ToString()
        {
            return IsValid ? $"{DomainId}:{EntryId}" : string.Empty;
        }

        public static bool operator ==(RuntimeEntryRef left, RuntimeEntryRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeEntryRef left, RuntimeEntryRef right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct StateDescriptor : IEquatable<StateDescriptor>
    {
        public static StateDescriptor Invalid => default;

        public StateDescriptor(
            string id,
            StateDomainId domainId,
            StateLifetime lifetime,
            string? debugName = null,
            bool isInspectable = true,
            bool allowSnapshot = true)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Descriptor id cannot be null or whitespace.", nameof(id));
            }

            if (!domainId.IsValid)
            {
                throw new ArgumentException("Domain id must be valid.", nameof(domainId));
            }

            if (lifetime == StateLifetime.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "State lifetime cannot be Unknown.");
            }

            Id = id;
            DebugName = string.IsNullOrWhiteSpace(debugName) ? id : debugName;
            DomainId = domainId;
            Lifetime = lifetime;
            IsInspectable = isInspectable;
            AllowSnapshot = allowSnapshot;
        }

        public string Id { get; }

        public string DebugName { get; }

        public StateDomainId DomainId { get; }

        public StateLifetime Lifetime { get; }

        public bool IsInspectable { get; }

        public bool AllowSnapshot { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Id) && DomainId.IsValid && Lifetime != StateLifetime.Unknown;

        public bool Equals(StateDescriptor other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal)
                && DomainId == other.DomainId
                && Lifetime == other.Lifetime
                && IsInspectable == other.IsInspectable
                && AllowSnapshot == other.AllowSnapshot;
        }

        public override bool Equals(object? obj)
        {
            return obj is StateDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(Id ?? string.Empty),
                DomainId,
                (int)Lifetime,
                IsInspectable,
                AllowSnapshot);
        }

        public override string ToString()
        {
            return IsValid ? $"{Id}@{DomainId}" : string.Empty;
        }

        public static bool operator ==(StateDescriptor left, StateDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StateDescriptor left, StateDescriptor right)
        {
            return !left.Equals(right);
        }
    }
}
