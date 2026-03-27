#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    public enum LifecycleEventKind
    {
        Unknown = 0,
        Create = 1,
        Enter = 2,
        Reenter = 3,
        Suspend = 4,
        Resume = 5,
        Complete = 6,
        Reset = 7,
        Dispose = 8,
    }

    public readonly struct RuntimeLifecycleEvent
    {
        public static RuntimeLifecycleEvent Invalid => default;

        private RuntimeLifecycleEvent(
            LifecycleEventKind kind,
            StateOwnerRef ownerRef,
            RuntimeTick? runtimeTick,
            string? descriptorId,
            RuntimeEntryRef? entryRef)
        {
            if (kind == LifecycleEventKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Lifecycle event kind cannot be Unknown.");
            }

            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            Kind = kind;
            OwnerRef = ownerRef;
            RuntimeTick = runtimeTick;
            DescriptorId = string.IsNullOrWhiteSpace(descriptorId) ? null : descriptorId;
            EntryRef = entryRef;
        }

        public LifecycleEventKind Kind { get; }

        public StateOwnerRef OwnerRef { get; }

        public RuntimeTick? RuntimeTick { get; }

        public string? DescriptorId { get; }

        public RuntimeEntryRef? EntryRef { get; }

        public bool IsValid => Kind != LifecycleEventKind.Unknown && OwnerRef.IsValid;

        public static RuntimeLifecycleEvent Create(StateOwnerRef ownerRef, RuntimeTick? runtimeTick = null, string? descriptorId = null, RuntimeEntryRef? entryRef = null)
        {
            return new RuntimeLifecycleEvent(LifecycleEventKind.Create, ownerRef, runtimeTick, descriptorId, entryRef);
        }

        public static RuntimeLifecycleEvent Enter(StateOwnerRef ownerRef, RuntimeTick? runtimeTick = null, string? descriptorId = null, RuntimeEntryRef? entryRef = null)
        {
            return new RuntimeLifecycleEvent(LifecycleEventKind.Enter, ownerRef, runtimeTick, descriptorId, entryRef);
        }

        public static RuntimeLifecycleEvent Reenter(StateOwnerRef ownerRef, RuntimeTick? runtimeTick = null, string? descriptorId = null, RuntimeEntryRef? entryRef = null)
        {
            return new RuntimeLifecycleEvent(LifecycleEventKind.Reenter, ownerRef, runtimeTick, descriptorId, entryRef);
        }

        public static RuntimeLifecycleEvent Suspend(StateOwnerRef ownerRef, RuntimeTick? runtimeTick = null, string? descriptorId = null, RuntimeEntryRef? entryRef = null)
        {
            return new RuntimeLifecycleEvent(LifecycleEventKind.Suspend, ownerRef, runtimeTick, descriptorId, entryRef);
        }

        public static RuntimeLifecycleEvent Resume(StateOwnerRef ownerRef, RuntimeTick? runtimeTick = null, string? descriptorId = null, RuntimeEntryRef? entryRef = null)
        {
            return new RuntimeLifecycleEvent(LifecycleEventKind.Resume, ownerRef, runtimeTick, descriptorId, entryRef);
        }

        public static RuntimeLifecycleEvent Complete(StateOwnerRef ownerRef, RuntimeTick? runtimeTick = null, string? descriptorId = null, RuntimeEntryRef? entryRef = null)
        {
            return new RuntimeLifecycleEvent(LifecycleEventKind.Complete, ownerRef, runtimeTick, descriptorId, entryRef);
        }

        public static RuntimeLifecycleEvent Reset(StateOwnerRef ownerRef, RuntimeTick? runtimeTick = null, string? descriptorId = null, RuntimeEntryRef? entryRef = null)
        {
            return new RuntimeLifecycleEvent(LifecycleEventKind.Reset, ownerRef, runtimeTick, descriptorId, entryRef);
        }

        public static RuntimeLifecycleEvent Dispose(StateOwnerRef ownerRef, RuntimeTick? runtimeTick = null, string? descriptorId = null, RuntimeEntryRef? entryRef = null)
        {
            return new RuntimeLifecycleEvent(LifecycleEventKind.Dispose, ownerRef, runtimeTick, descriptorId, entryRef);
        }

        public override string ToString()
        {
            return IsValid ? $"{Kind}({OwnerRef})" : string.Empty;
        }
    }
}
