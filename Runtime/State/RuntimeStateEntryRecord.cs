#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class RuntimeStateEntryRecord
    {
        public RuntimeStateEntryRecord(
            RuntimeEntryRef entryRef,
            StateDescriptor descriptor,
            StateOwnerRef ownerRef,
            string slotKey,
            object? state = null)
        {
            if (!entryRef.IsValid)
            {
                throw new ArgumentException("Entry ref must be valid.", nameof(entryRef));
            }

            if (!descriptor.IsValid)
            {
                throw new ArgumentException("Descriptor must be valid.", nameof(descriptor));
            }

            if (descriptor.DomainId != entryRef.DomainId)
            {
                throw new ArgumentException("Descriptor domain must match entry domain.", nameof(descriptor));
            }

            if (!ownerRef.IsValid)
            {
                throw new ArgumentException("Owner ref must be valid.", nameof(ownerRef));
            }

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("Slot key cannot be null or whitespace.", nameof(slotKey));
            }

            EntryRef = entryRef;
            Descriptor = descriptor;
            OwnerRef = ownerRef;
            SlotKey = slotKey;
            State = state;
        }

        public RuntimeEntryRef EntryRef { get; }

        public StateDescriptor Descriptor { get; }

        public StateOwnerRef OwnerRef { get; }

        public string SlotKey { get; }

        public object? State { get; set; }

        public override string ToString()
        {
            return $"{EntryRef}<{Descriptor.Id}>[{SlotKey}]";
        }
    }
}
