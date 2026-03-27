#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public readonly struct RuntimeStateLifecycleBinding : IEquatable<RuntimeStateLifecycleBinding>
    {
        public RuntimeStateLifecycleBinding(StateDescriptor descriptor, string slotKey)
        {
            if (!descriptor.IsValid)
            {
                throw new ArgumentException("State descriptor must be valid.", nameof(descriptor));
            }

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("Slot key cannot be null or whitespace.", nameof(slotKey));
            }

            Descriptor = descriptor;
            SlotKey = slotKey;
        }

        public StateDescriptor Descriptor { get; }

        public string SlotKey { get; }

        public bool Equals(RuntimeStateLifecycleBinding other)
        {
            return Descriptor == other.Descriptor
                && string.Equals(SlotKey, other.SlotKey, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is RuntimeStateLifecycleBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Descriptor, StringComparer.Ordinal.GetHashCode(SlotKey ?? string.Empty));
        }

        public static bool operator ==(RuntimeStateLifecycleBinding left, RuntimeStateLifecycleBinding right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeStateLifecycleBinding left, RuntimeStateLifecycleBinding right)
        {
            return !left.Equals(right);
        }
    }
}
