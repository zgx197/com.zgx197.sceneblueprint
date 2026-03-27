#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class ReactiveStateDomain : IReactiveStateDomain
    {
        public static readonly StateDomainId ReactiveDomainId = new("reactive");

        private const string WaitDescriptorId = "reactive.wait";
        private const string SubscriptionDescriptorId = "reactive.subscription";
        private const string SubscriptionSlotKeyPrefix = "subscription:";

        private readonly IRuntimeStateBackend _backend;

        public ReactiveStateDomain(IRuntimeStateBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public StateDomainId DomainId => ReactiveDomainId;

        public RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey)
        {
            EnsureDescriptorMatchesDomain(descriptor);
            return _backend.EnsureEntry(descriptor, ownerRef, slotKey);
        }

        public RuntimeEntryRef EnsureWait(ReactiveWaitRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Reactive wait request must be valid.", nameof(request));
            }

            var descriptor = CreateWaitDescriptor(request.Lifetime);
            var entryRef = EnsureEntry(descriptor, request.OwnerRef, request.SlotKey);
            var entry = GetRequiredEntry(entryRef);

            if (entry.State is ReactiveWaitState existingState)
            {
                EnsureCompatible(existingState, request);
                existingState.ResumeContextRef = request.ResumeContextRef;
                RefreshWaitState(entry, existingState);
                return entryRef;
            }

            if (entry.State is not null)
            {
                throw CreateStateTypeMismatchException(entryRef, entry.State, typeof(ReactiveWaitState));
            }

            entry.State = new ReactiveWaitState(
                request.WaitKind,
                request.Lifetime,
                request.ResolvePolicy,
                request.ResumeContextRef);
            return entryRef;
        }

        public RuntimeEntryRef AttachSubscription(ReactiveSubscriptionRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Reactive subscription request must be valid.", nameof(request));
            }

            var waitEntry = GetRequiredWaitEntry(request.WaitEntryRef, out var waitState);
            var slotKey = CreateSubscriptionSlotKey(request.WaitEntryRef, request.BindingKey);
            var descriptor = CreateSubscriptionDescriptor(waitEntry.Descriptor.Lifetime);
            var entryRef = EnsureEntry(descriptor, waitEntry.OwnerRef, slotKey);
            var entry = GetRequiredEntry(entryRef);

            if (entry.State is ReactiveSubscriptionState existingState)
            {
                EnsureCompatible(existingState, request);
                RefreshWaitState(waitEntry, waitState);
                return entryRef;
            }

            if (entry.State is not null)
            {
                throw CreateStateTypeMismatchException(entryRef, entry.State, typeof(ReactiveSubscriptionState));
            }

            entry.State = new ReactiveSubscriptionState(
                request.WaitEntryRef,
                request.BindingKey,
                request.SourceKind,
                request.SourceRef);
            RefreshWaitState(waitEntry, waitState);
            return entryRef;
        }

        public void CancelWait(RuntimeEntryRef waitEntryRef)
        {
            var waitEntry = GetRequiredWaitEntry(waitEntryRef, out var waitState);
            if (waitState.Status == ReactiveWaitStatus.Resolved
                || waitState.Status == ReactiveWaitStatus.Cancelled)
            {
                return;
            }

            var subscriptions = CollectSubscriptions(waitEntryRef, waitEntry.OwnerRef);
            for (var index = 0; index < subscriptions.Count; index++)
            {
                subscriptions[index].State.Status = ReactiveSubscriptionStatus.Cancelled;
            }

            waitState.Status = ReactiveWaitStatus.Cancelled;
            waitState.ResolvedBySubscriptionEntryRef = null;
            RefreshWaitState(waitEntry, waitState);
        }

        public void NotifySource(ReactiveSourceNotification notification)
        {
            if (!notification.IsValid)
            {
                throw new ArgumentException("Reactive source notification must be valid.", nameof(notification));
            }

            var touchedWaits = new HashSet<RuntimeEntryRef>();
            var entryRefs = _backend.EnumerateEntries(DomainId);
            for (var index = 0; index < entryRefs.Count; index++)
            {
                if (!_backend.TryGetEntry(entryRefs[index], out var entry)
                    || entry is null
                    || entry.State is not ReactiveSubscriptionState subscriptionState
                    || subscriptionState.Status == ReactiveSubscriptionStatus.Cancelled
                    || subscriptionState.SourceRef != notification.SourceRef)
                {
                    continue;
                }

                if (!TryGetWaitState(subscriptionState.WaitEntryRef, out _, out var waitState))
                {
                    continue;
                }

                if (waitState!.Status == ReactiveWaitStatus.Cancelled
                    || waitState.Status == ReactiveWaitStatus.Resolved)
                {
                    continue;
                }

                subscriptionState.Status = ReactiveSubscriptionStatus.Matched;
                subscriptionState.LastMatchTokenRef = notification.MatchTokenRef;
                touchedWaits.Add(subscriptionState.WaitEntryRef);
            }

            foreach (var waitEntryRef in touchedWaits)
            {
                if (TryGetWaitState(waitEntryRef, out var waitEntry, out var waitState))
                {
                    RefreshWaitState(waitEntry!, waitState!);
                }
            }
        }

        public bool TryLocateEntry(StateOwnerRef ownerRef, string slotKey, out RuntimeEntryRef entryRef)
        {
            return _backend.TryLocateEntry(DomainId, ownerRef, slotKey, out entryRef);
        }

        public void ReleaseEntry(RuntimeEntryRef entryRef)
        {
            if (!TryGetDomainEntry(entryRef, out var entry) || entry is null)
            {
                return;
            }

            if (entry.State is ReactiveWaitState)
            {
                var subscriptions = CollectSubscriptions(entryRef, entry.OwnerRef);
                for (var index = 0; index < subscriptions.Count; index++)
                {
                    _backend.ReleaseEntry(subscriptions[index].EntryRef);
                }

                _backend.ReleaseEntry(entryRef);
                return;
            }

            if (entry.State is ReactiveSubscriptionState subscriptionState)
            {
                var waitEntryRef = subscriptionState.WaitEntryRef;
                _backend.ReleaseEntry(entryRef);

                if (TryGetWaitState(waitEntryRef, out var waitEntry, out var waitState))
                {
                    RefreshWaitState(waitEntry!, waitState!);
                }

                return;
            }

            _backend.ReleaseEntry(entryRef);
        }

        public void ReleaseOwnedEntries(StateOwnerRef ownerRef)
        {
            _backend.ReleaseOwnedEntries(DomainId, ownerRef);
        }

        public IReadOnlyList<RuntimeEntryRef> EnumerateEntries(ObservationFilter? filter = null)
        {
            return _backend.EnumerateEntries(DomainId, filter);
        }

        public static string CreateSubscriptionSlotKey(RuntimeEntryRef waitEntryRef, string bindingKey)
        {
            if (!waitEntryRef.IsValid)
            {
                throw new ArgumentException("Wait entry ref must be valid.", nameof(waitEntryRef));
            }

            if (string.IsNullOrWhiteSpace(bindingKey))
            {
                throw new ArgumentException("Binding key cannot be null or whitespace.", nameof(bindingKey));
            }

            return string.Concat(
                SubscriptionSlotKeyPrefix,
                waitEntryRef.EntryId,
                ":",
                bindingKey);
        }

        private static StateDescriptor CreateWaitDescriptor(StateLifetime lifetime)
        {
            return new StateDescriptor(
                WaitDescriptorId,
                ReactiveDomainId,
                lifetime,
                debugName: "Reactive.Wait");
        }

        private static StateDescriptor CreateSubscriptionDescriptor(StateLifetime lifetime)
        {
            return new StateDescriptor(
                SubscriptionDescriptorId,
                ReactiveDomainId,
                lifetime,
                debugName: "Reactive.Subscription");
        }

        private static void EnsureDescriptorMatchesDomain(StateDescriptor descriptor)
        {
            if (!descriptor.IsValid)
            {
                throw new ArgumentException("State descriptor must be valid.", nameof(descriptor));
            }

            if (descriptor.DomainId != ReactiveDomainId)
            {
                throw new ArgumentException(
                    $"Reactive state descriptor must belong to domain '{ReactiveDomainId}', but was '{descriptor.DomainId}'.",
                    nameof(descriptor));
            }
        }

        private static void EnsureCompatible(ReactiveWaitState state, ReactiveWaitRequest request)
        {
            if (state.WaitKind != request.WaitKind
                || state.Lifetime != request.Lifetime
                || state.ResolvePolicy != request.ResolvePolicy)
            {
                throw new InvalidOperationException("Reactive wait request conflicts with the existing wait entry.");
            }
        }

        private static void EnsureCompatible(ReactiveSubscriptionState state, ReactiveSubscriptionRequest request)
        {
            if (state.WaitEntryRef != request.WaitEntryRef
                || !string.Equals(state.BindingKey, request.BindingKey, StringComparison.Ordinal)
                || state.SourceKind != request.SourceKind
                || state.SourceRef != request.SourceRef)
            {
                throw new InvalidOperationException("Reactive subscription request conflicts with the existing subscription entry.");
            }
        }

        private RuntimeStateEntryRecord GetRequiredEntry(RuntimeEntryRef entryRef)
        {
            if (!TryGetDomainEntry(entryRef, out var entry) || entry is null)
            {
                throw new KeyNotFoundException($"Reactive state entry was not found: {entryRef}");
            }

            return entry;
        }

        private RuntimeStateEntryRecord GetRequiredWaitEntry(RuntimeEntryRef waitEntryRef, out ReactiveWaitState waitState)
        {
            var entry = GetRequiredEntry(waitEntryRef);
            if (entry.State is not ReactiveWaitState typedState)
            {
                throw new InvalidOperationException($"Reactive wait entry is invalid: {waitEntryRef}");
            }

            waitState = typedState;
            return entry;
        }

        private bool TryGetDomainEntry(RuntimeEntryRef entryRef, out RuntimeStateEntryRecord? entry)
        {
            if (!entryRef.IsValid || entryRef.DomainId != DomainId)
            {
                entry = null;
                return false;
            }

            return _backend.TryGetEntry(entryRef, out entry);
        }

        private bool TryGetWaitState(
            RuntimeEntryRef waitEntryRef,
            out RuntimeStateEntryRecord? waitEntry,
            out ReactiveWaitState? waitState)
        {
            if (TryGetDomainEntry(waitEntryRef, out waitEntry)
                && waitEntry?.State is ReactiveWaitState typedState)
            {
                waitState = typedState;
                return true;
            }

            waitState = null;
            return false;
        }

        private List<ReactiveSubscriptionRecord> CollectSubscriptions(RuntimeEntryRef waitEntryRef, StateOwnerRef ownerRef)
        {
            var entryRefs = _backend.EnumerateEntries(DomainId, new ObservationFilter(ownerRef));
            var result = new List<ReactiveSubscriptionRecord>();
            for (var index = 0; index < entryRefs.Count; index++)
            {
                if (!_backend.TryGetEntry(entryRefs[index], out var entry)
                    || entry is null
                    || entry.State is not ReactiveSubscriptionState subscriptionState
                    || subscriptionState.WaitEntryRef != waitEntryRef)
                {
                    continue;
                }

                result.Add(new ReactiveSubscriptionRecord(entryRefs[index], subscriptionState));
            }

            return result;
        }

        private void RefreshWaitState(RuntimeStateEntryRecord waitEntry, ReactiveWaitState waitState)
        {
            var subscriptions = CollectSubscriptions(waitEntry.EntryRef, waitEntry.OwnerRef);
            waitState.SubscriptionCount = subscriptions.Count;

            var matchedCount = 0;
            ReactiveSubscriptionRecord? anyMatchedSubscription = null;
            ReactiveSubscriptionRecord? lastMatchedSubscription = null;

            for (var index = 0; index < subscriptions.Count; index++)
            {
                if (subscriptions[index].State.Status != ReactiveSubscriptionStatus.Matched)
                {
                    continue;
                }

                matchedCount++;
                anyMatchedSubscription ??= subscriptions[index];
                lastMatchedSubscription = subscriptions[index];
            }

            waitState.MatchedSubscriptionCount = matchedCount;

            if (waitState.Status == ReactiveWaitStatus.Cancelled)
            {
                return;
            }

            if (waitState.Status == ReactiveWaitStatus.Resolved)
            {
                if (waitState.ResolvedBySubscriptionEntryRef.HasValue)
                {
                    return;
                }

                if (lastMatchedSubscription.HasValue)
                {
                    waitState.ResolvedBySubscriptionEntryRef = lastMatchedSubscription.Value.EntryRef;
                    waitState.LastMatchTokenRef = lastMatchedSubscription.Value.State.LastMatchTokenRef;
                }

                return;
            }

            switch (waitState.ResolvePolicy)
            {
                case ResolvePolicy.Any:
                    if (anyMatchedSubscription.HasValue)
                    {
                        ResolveWait(waitState, anyMatchedSubscription.Value);
                    }
                    else
                    {
                        waitState.Status = ReactiveWaitStatus.Waiting;
                    }

                    break;

                case ResolvePolicy.All:
                    if (subscriptions.Count > 0
                        && matchedCount == subscriptions.Count
                        && lastMatchedSubscription.HasValue)
                    {
                        ResolveWait(waitState, lastMatchedSubscription.Value);
                    }
                    else
                    {
                        waitState.Status = ReactiveWaitStatus.Waiting;
                    }

                    break;

                default:
                    waitState.Status = ReactiveWaitStatus.Waiting;
                    break;
            }
        }

        private static void ResolveWait(ReactiveWaitState waitState, ReactiveSubscriptionRecord subscription)
        {
            waitState.Status = ReactiveWaitStatus.Resolved;
            waitState.ResolvedBySubscriptionEntryRef = subscription.EntryRef;
            waitState.LastMatchTokenRef = subscription.State.LastMatchTokenRef;
        }

        private static InvalidOperationException CreateStateTypeMismatchException(RuntimeEntryRef entryRef, object state, Type expectedType)
        {
            return new InvalidOperationException(
                $"Reactive state entry {entryRef} already holds {state.GetType().FullName}, not {expectedType.FullName}.");
        }

        private readonly struct ReactiveSubscriptionRecord
        {
            public ReactiveSubscriptionRecord(RuntimeEntryRef entryRef, ReactiveSubscriptionState state)
            {
                EntryRef = entryRef;
                State = state;
            }

            public RuntimeEntryRef EntryRef { get; }

            public ReactiveSubscriptionState State { get; }
        }

        internal enum ReactiveWaitStatus
        {
            Waiting = 0,
            Resolved = 1,
            Cancelled = 2,
        }

        internal enum ReactiveSubscriptionStatus
        {
            Listening = 0,
            Matched = 1,
            Cancelled = 2,
        }

        internal sealed class ReactiveWaitState
        {
            public ReactiveWaitState(
                ReactiveWaitKind waitKind,
                StateLifetime lifetime,
                ResolvePolicy resolvePolicy,
                ResumeContextRef? resumeContextRef)
            {
                WaitKind = waitKind;
                Lifetime = lifetime;
                ResolvePolicy = resolvePolicy;
                ResumeContextRef = resumeContextRef;
                Status = ReactiveWaitStatus.Waiting;
            }

            public ReactiveWaitKind WaitKind { get; }

            public StateLifetime Lifetime { get; }

            public ResolvePolicy ResolvePolicy { get; }

            public ResumeContextRef? ResumeContextRef { get; set; }

            public ReactiveWaitStatus Status { get; set; }

            public int SubscriptionCount { get; set; }

            public int MatchedSubscriptionCount { get; set; }

            public RuntimeEntryRef? ResolvedBySubscriptionEntryRef { get; set; }

            public MatchTokenRef? LastMatchTokenRef { get; set; }

            public RuntimeEntryRef? ScheduleEntryRef { get; set; }
        }

        internal sealed class ReactiveSubscriptionState
        {
            public ReactiveSubscriptionState(
                RuntimeEntryRef waitEntryRef,
                string bindingKey,
                SourceKind sourceKind,
                SourceRef sourceRef)
            {
                WaitEntryRef = waitEntryRef;
                BindingKey = bindingKey;
                SourceKind = sourceKind;
                SourceRef = sourceRef;
                Status = ReactiveSubscriptionStatus.Listening;
            }

            public RuntimeEntryRef WaitEntryRef { get; }

            public string BindingKey { get; }

            public SourceKind SourceKind { get; }

            public SourceRef SourceRef { get; }

            public ReactiveSubscriptionStatus Status { get; set; }

            public MatchTokenRef? LastMatchTokenRef { get; set; }
        }
    }
}
