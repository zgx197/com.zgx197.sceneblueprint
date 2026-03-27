#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public interface IRuntimeStateBackend
    {
        RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey);

        bool TryLocateEntry(StateDomainId domainId, StateOwnerRef ownerRef, string slotKey, out RuntimeEntryRef entryRef);

        bool TryGetEntry(RuntimeEntryRef entryRef, out RuntimeStateEntryRecord? entry);

        bool ReleaseEntry(RuntimeEntryRef entryRef);

        int ReleaseOwnedEntries(StateDomainId domainId, StateOwnerRef ownerRef);

        IReadOnlyList<RuntimeEntryRef> EnumerateEntries(StateDomainId domainId, ObservationFilter? filter = null);

        int Clear();
    }
}
