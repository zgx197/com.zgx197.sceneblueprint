#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    public interface IRuntimeStateHost
    {
        IReadOnlyList<IRuntimeStateDomain> Domains { get; }

        IRuntimeStateInspector Inspector { get; }

        IRuntimeSnapshotService Snapshot { get; }

        bool TryGetDomain(StateDomainId domainId, out IRuntimeStateDomain domain);

        TDomain GetRequiredDomain<TDomain>() where TDomain : class, IRuntimeStateDomain;

        void HandleLifecycle(RuntimeLifecycleEvent runtimeEvent);
    }

    public interface IRuntimeStateDomain
    {
        StateDomainId DomainId { get; }

        RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey);

        bool TryLocateEntry(StateOwnerRef ownerRef, string slotKey, out RuntimeEntryRef entryRef);

        void ReleaseEntry(RuntimeEntryRef entryRef);

        void ReleaseOwnedEntries(StateOwnerRef ownerRef);

        IReadOnlyList<RuntimeEntryRef> EnumerateEntries(ObservationFilter? filter = null);
    }

    public interface IRuntimeStateInspector
    {
        ObservationResult Inspect(ObservationRequest request);
    }

    public interface IRuntimeSnapshotService
    {
        RuntimeSnapshot Capture(SnapshotRequest request);

        SnapshotCapability DescribeCapability(RuntimeEntryRef entryRef);
    }

    public interface IReactiveStateDomain : IRuntimeStateDomain
    {
        RuntimeEntryRef EnsureWait(ReactiveWaitRequest request);

        RuntimeEntryRef AttachSubscription(ReactiveSubscriptionRequest request);

        void CancelWait(RuntimeEntryRef waitEntryRef);

        void NotifySource(ReactiveSourceNotification notification);
    }

    public interface ISchedulingStateDomain : IRuntimeStateDomain
    {
        RuntimeEntryRef Schedule(SchedulingEntryRequest request);

        void Cancel(RuntimeEntryRef scheduleEntryRef);

        void Pause(RuntimeEntryRef scheduleEntryRef);

        void Resume(RuntimeEntryRef scheduleEntryRef);
    }

    public interface IPortStateDomain : IRuntimeStateDomain
    {
        bool TryReadValue(PortValueReadRequest request, out PortValueReadResult readResult);

        void WriteValue(PortValueWriteRequest request);

        void Emit(PortEmissionRequest request);
    }
}
