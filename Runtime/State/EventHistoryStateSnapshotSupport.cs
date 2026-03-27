#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    [Serializable]
    public sealed class EventHistoryStatePayload
    {
        public EventHistoryStatePayload(
            long sequence,
            EventHistoryRecordKind recordKind,
            string eventKind,
            string actionId,
            int actionIndex,
            int tick,
            string signalTag,
            string subjectRefSerialized,
            string subjectSummary,
            string instigatorRefSerialized,
            string instigatorSummary,
            string targetRefSerialized,
            string targetSummary,
            string payloadSummary,
            BlueprintEventContext eventContext)
        {
            Sequence = sequence;
            RecordKind = recordKind;
            EventKind = eventKind ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            ActionIndex = actionIndex;
            Tick = tick;
            SignalTag = signalTag ?? string.Empty;
            SubjectRefSerialized = subjectRefSerialized ?? string.Empty;
            SubjectSummary = subjectSummary ?? string.Empty;
            InstigatorRefSerialized = instigatorRefSerialized ?? string.Empty;
            InstigatorSummary = instigatorSummary ?? string.Empty;
            TargetRefSerialized = targetRefSerialized ?? string.Empty;
            TargetSummary = targetSummary ?? string.Empty;
            PayloadSummary = payloadSummary ?? string.Empty;
            EventContext = BlueprintEventContextSemanticUtility.Normalize(eventContext) ?? new BlueprintEventContext();
        }

        public long Sequence { get; }

        public EventHistoryRecordKind RecordKind { get; }

        public string EventKind { get; }

        public string ActionId { get; }

        public int ActionIndex { get; }

        public int Tick { get; }

        public string SignalTag { get; }

        public string SubjectRefSerialized { get; }

        public string SubjectSummary { get; }

        public string InstigatorRefSerialized { get; }

        public string InstigatorSummary { get; }

        public string TargetRefSerialized { get; }

        public string TargetSummary { get; }

        public string PayloadSummary { get; }

        public BlueprintEventContext EventContext { get; }

        public BlueprintEventHistoryState ToState()
        {
            return new BlueprintEventHistoryState
            {
                Sequence = Sequence,
                RecordKind = RecordKind,
                EventKind = EventKind,
                ActionId = ActionId,
                ActionIndex = ActionIndex,
                Tick = Tick,
                SignalTag = SignalTag,
                SubjectRefSerialized = SubjectRefSerialized,
                SubjectSummary = SubjectSummary,
                InstigatorRefSerialized = InstigatorRefSerialized,
                InstigatorSummary = InstigatorSummary,
                TargetRefSerialized = TargetRefSerialized,
                TargetSummary = TargetSummary,
                PayloadSummary = PayloadSummary,
                EventContext = BlueprintEventContextSemanticUtility.Normalize(EventContext) ?? new BlueprintEventContext(),
            };
        }
    }

    public sealed class EventHistoryStateSnapshotExporter : IRuntimeStateSnapshotExporter, IRuntimeStateSnapshotExporterMetadata
    {
        public const string ExporterId = "sceneblueprint.event-history.exporter";
        public const string PayloadTypeId = "sceneblueprint.event-history.state";
        public const string SchemaId = "sceneblueprint.event-history.state.schema";
        public const int SchemaVersion = 1;

        public int Priority => 100;

        public bool CanExport(RuntimeStateEntryRecord entry)
        {
            return entry.State is BlueprintEventHistoryState
                && string.Equals(entry.Descriptor.Id, EventHistoryStateDomain.EventHistoryDescriptor.Id, StringComparison.Ordinal);
        }

        public SnapshotRestoreMode DescribeRestoreMode(RuntimeStateEntryRecord entry)
        {
            return SnapshotRestoreMode.Rebuild;
        }

        public bool TryDescribePayload(RuntimeStateEntryRecord entry, out RuntimeStateSnapshotPayloadDescriptor payloadDescriptor)
        {
            if (!CanExport(entry))
            {
                payloadDescriptor = default;
                return false;
            }

            payloadDescriptor = new RuntimeStateSnapshotPayloadDescriptor(
                PayloadTypeId,
                exporterId: ExporterId,
                schemaId: SchemaId,
                schemaVersion: SchemaVersion);
            return true;
        }

        public RuntimeStateSnapshotExport Export(RuntimeStateEntryRecord entry, FieldFilter? fieldFilter = null)
        {
            if (entry.State is not BlueprintEventHistoryState state)
            {
                return RuntimeStateSnapshotExport.Invalid;
            }

            var payload = new EventHistoryStatePayload(
                state.Sequence,
                state.RecordKind,
                state.EventKind,
                state.ActionId,
                state.ActionIndex,
                state.Tick,
                state.SignalTag,
                state.SubjectRefSerialized,
                state.SubjectSummary,
                state.InstigatorRefSerialized,
                state.InstigatorSummary,
                state.TargetRefSerialized,
                state.TargetSummary,
                state.PayloadSummary,
                state.EventContext);

            return new RuntimeStateSnapshotExport(
                new RuntimeStateSnapshotPayload(
                    PayloadTypeId,
                    payload,
                    exporterId: ExporterId,
                    schemaId: SchemaId,
                    schemaVersion: SchemaVersion),
                SnapshotRestoreMode.Rebuild);
        }
    }

    public sealed class EventHistoryStateSnapshotReplayer : IRuntimeStateSnapshotReplayer
    {
        public bool CanReplay(RuntimeStateSnapshotPayload payload, SnapshotRestoreMode restoreMode)
        {
            return string.Equals(payload.SchemaId, EventHistoryStateSnapshotExporter.SchemaId, StringComparison.Ordinal)
                && restoreMode == SnapshotRestoreMode.Rebuild;
        }

        public RuntimeSnapshotReplayEntryResult Replay(
            RuntimeSnapshotReplayContext context,
            RuntimeStateSnapshotPayload payload,
            SnapshotRestoreMode restoreMode)
        {
            if (!RuntimeSnapshotReplaySupport.TryGetPayload(
                    context,
                    payload,
                    "Event history",
                    out EventHistoryStatePayload statePayload,
                    out var failureResult))
            {
                return failureResult;
            }

            if (!RuntimeSnapshotReplaySupport.TryGetRequiredDomain(
                    context,
                    EventHistoryStateDomain.EventHistoryDomainId,
                    "Event history domain is unavailable.",
                    out EventHistoryStateDomain eventHistoryDomain,
                    out failureResult))
            {
                return failureResult;
            }

            var entryRef = eventHistoryDomain.Import(statePayload.ToState());
            return RuntimeSnapshotReplaySupport.Restored(
                context,
                entryRef,
                "Event history state replayed via rebuild.");
        }
    }
}
