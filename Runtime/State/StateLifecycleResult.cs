#nullable enable
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public readonly struct StateLifecycleResult
    {
        public static StateLifecycleResult NoOp(RuntimeLifecycleEvent runtimeEvent, StateDescriptor descriptor)
        {
            return new StateLifecycleResult(runtimeEvent, descriptor, null, StateLifecycleDisposition.None, false, 0);
        }

        public StateLifecycleResult(
            RuntimeLifecycleEvent runtimeEvent,
            StateDescriptor descriptor,
            RuntimeEntryRef? entryRef,
            StateLifecycleDisposition disposition,
            bool hadExistingEntry,
            int releasedEntryCount)
        {
            RuntimeEvent = runtimeEvent;
            Descriptor = descriptor;
            EntryRef = entryRef;
            Disposition = disposition;
            HadExistingEntry = hadExistingEntry;
            ReleasedEntryCount = releasedEntryCount;
        }

        public RuntimeLifecycleEvent RuntimeEvent { get; }

        public StateDescriptor Descriptor { get; }

        public RuntimeEntryRef? EntryRef { get; }

        public StateLifecycleDisposition Disposition { get; }

        public bool HadExistingEntry { get; }

        public int ReleasedEntryCount { get; }

        public bool IsNoOp => Disposition == StateLifecycleDisposition.None && ReleasedEntryCount == 0;

        public bool Created => (Disposition & StateLifecycleDisposition.Created) != 0;

        public bool Reused => (Disposition & StateLifecycleDisposition.Reused) != 0;

        public bool KeepReadOnlyFinal => (Disposition & StateLifecycleDisposition.KeepReadOnlyFinal) != 0;

        public bool DeferredRelease => (Disposition & StateLifecycleDisposition.DeferredRelease) != 0;

        public bool Released => (Disposition & StateLifecycleDisposition.Released) != 0;
    }
}
