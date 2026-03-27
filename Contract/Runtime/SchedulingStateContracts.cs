#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    [Flags]
    public enum PausePolicy
    {
        None = 0,
        RespectRuntimePause = 1 << 0,
        RespectOwnerSuspend = 1 << 1,
    }

    public enum SchedulingKind
    {
        Unknown = 0,
        Timeout = 1,
        Delay = 2,
        Cooldown = 3,
        Retry = 4,
    }

    public readonly struct SchedulingEntryRequest
    {
        public static SchedulingEntryRequest Invalid => default;

        public SchedulingEntryRequest(
            StateOwnerRef ownerRef,
            string slotKey,
            SchedulingKind kind,
            StateLifetime lifetime,
            RuntimeTick startTick,
            RuntimeTick targetTick,
            PausePolicy pausePolicy)
        {
            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("Slot key cannot be null or whitespace.", nameof(slotKey));
            }

            if (kind == SchedulingKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Scheduling kind cannot be Unknown.");
            }

            if (lifetime == StateLifetime.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "State lifetime cannot be Unknown.");
            }

            if (targetTick < startTick)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTick), targetTick, "Target tick cannot be earlier than start tick.");
            }

            OwnerRef = ownerRef;
            SlotKey = slotKey;
            Kind = kind;
            Lifetime = lifetime;
            StartTick = startTick;
            TargetTick = targetTick;
            PausePolicy = pausePolicy;
        }

        public StateOwnerRef OwnerRef { get; }

        public string SlotKey { get; }

        public SchedulingKind Kind { get; }

        public StateLifetime Lifetime { get; }

        public RuntimeTick StartTick { get; }

        public RuntimeTick TargetTick { get; }

        public PausePolicy PausePolicy { get; }

        public bool IsValid => OwnerRef.IsValid
            && !string.IsNullOrWhiteSpace(SlotKey)
            && Kind != SchedulingKind.Unknown
            && Lifetime != StateLifetime.Unknown
            && TargetTick >= StartTick;
    }
}
