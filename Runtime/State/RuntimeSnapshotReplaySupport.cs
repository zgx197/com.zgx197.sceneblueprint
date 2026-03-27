#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public static class RuntimeSnapshotReplaySupport
    {
        public static bool TryGetPayload<TPayload>(
            RuntimeSnapshotReplayContext context,
            RuntimeStateSnapshotPayload payload,
            string replayLabel,
            out TPayload typedPayload,
            out RuntimeSnapshotReplayEntryResult failureResult)
        {
            if (payload.Payload is TPayload resolvedPayload)
            {
                typedPayload = resolvedPayload;
                failureResult = null!;
                return true;
            }

            typedPayload = default!;
            failureResult = RuntimeSnapshotReplayEntryResult.Failed(
                context.SnapshotEntry.LogicalEntryKey,
                $"{replayLabel} payload type is invalid.");
            return false;
        }

        public static bool TryResolveEntry(
            RuntimeSnapshotReplayContext context,
            StateDescriptor descriptor,
            string defaultSlotKey,
            string replayLabel,
            out RuntimeEntryRef entryRef,
            out RuntimeSnapshotReplayEntryResult failureResult)
        {
            if (!context.TryResolveRoute(out var route))
            {
                entryRef = RuntimeEntryRef.Invalid;
                failureResult = RuntimeSnapshotReplayEntryResult.Failed(
                    context.SnapshotEntry.LogicalEntryKey,
                    $"{replayLabel} snapshot route is invalid.");
                return false;
            }

            var slotKey = string.IsNullOrWhiteSpace(route.SlotKey)
                ? defaultSlotKey
                : route.SlotKey;
            entryRef = context.EnsureEntry(descriptor, route.CreateOwnerRef(), slotKey);
            failureResult = null!;
            return true;
        }

        public static bool TryGetRequiredDomain<TDomain>(
            RuntimeSnapshotReplayContext context,
            StateDomainId domainId,
            string unavailableMessage,
            out TDomain domain,
            out RuntimeSnapshotReplayEntryResult failureResult)
            where TDomain : class, IRuntimeStateDomain
        {
            if (context.TryGetDomain(domainId, out var resolvedDomain)
                && resolvedDomain is TDomain typedDomain)
            {
                domain = typedDomain;
                failureResult = null!;
                return true;
            }

            domain = null!;
            failureResult = RuntimeSnapshotReplayEntryResult.Failed(
                context.SnapshotEntry.LogicalEntryKey,
                unavailableMessage);
            return false;
        }

        public static T[] ToArray<T>(IReadOnlyList<T> items)
        {
            if (items is null || items.Count == 0)
            {
                return Array.Empty<T>();
            }

            if (items is T[] array)
            {
                return array;
            }

            var result = new T[items.Count];
            for (var index = 0; index < items.Count; index++)
            {
                result[index] = items[index];
            }

            return result;
        }

        public static RuntimeSnapshotReplayEntryResult Restored(
            RuntimeSnapshotReplayContext context,
            RuntimeEntryRef entryRef,
            string? message = null)
        {
            return RuntimeSnapshotReplayEntryResult.Restored(
                context.SnapshotEntry.LogicalEntryKey,
                entryRef,
                message: message);
        }
    }
}
