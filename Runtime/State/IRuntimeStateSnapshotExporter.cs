#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public readonly struct RuntimeStateSnapshotPayloadDescriptor
    {
        public RuntimeStateSnapshotPayloadDescriptor(
            string payloadTypeId,
            string? exporterId = null,
            string? schemaId = null,
            int schemaVersion = 1)
        {
            if (string.IsNullOrWhiteSpace(payloadTypeId))
            {
                throw new ArgumentException("Payload type id cannot be null or whitespace.", nameof(payloadTypeId));
            }

            if (schemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Schema version must be greater than zero.");
            }

            PayloadTypeId = payloadTypeId;
            ExporterId = string.IsNullOrWhiteSpace(exporterId) ? null : exporterId;
            SchemaId = string.IsNullOrWhiteSpace(schemaId) ? payloadTypeId : schemaId;
            SchemaVersion = schemaVersion;
        }

        public string PayloadTypeId { get; }

        public string? ExporterId { get; }

        public string SchemaId { get; }

        public int SchemaVersion { get; }
    }

    public interface IRuntimeStateSnapshotExporter
    {
        bool CanExport(RuntimeStateEntryRecord entry);

        SnapshotRestoreMode DescribeRestoreMode(RuntimeStateEntryRecord entry);

        RuntimeStateSnapshotExport Export(RuntimeStateEntryRecord entry, FieldFilter? fieldFilter = null);
    }

    public interface IRuntimeStateSnapshotExporterMetadata
    {
        int Priority { get; }

        bool TryDescribePayload(RuntimeStateEntryRecord entry, out RuntimeStateSnapshotPayloadDescriptor payloadDescriptor);
    }
}
