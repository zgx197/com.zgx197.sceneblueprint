#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public readonly struct RuntimeStateSnapshotExport
    {
        public static RuntimeStateSnapshotExport Invalid => default;

        public RuntimeStateSnapshotExport(
            RuntimeStateSnapshotPayload payload,
            SnapshotRestoreMode restoreMode,
            bool isExplicitPolicy = true)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (restoreMode == SnapshotRestoreMode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(restoreMode), restoreMode, "State payload export must define a restore mode.");
            }

            RestoreMode = restoreMode;
            IsExplicitPolicy = isExplicitPolicy;
        }

        public RuntimeStateSnapshotPayload? Payload { get; }

        public SnapshotRestoreMode RestoreMode { get; }

        public bool IsExplicitPolicy { get; }

        public bool IsValid => Payload is not null && RestoreMode != SnapshotRestoreMode.None;
    }

    public sealed class RuntimeStateSnapshotPayload
    {
        public RuntimeStateSnapshotPayload(
            string payloadTypeId,
            object payload,
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
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            ExporterId = string.IsNullOrWhiteSpace(exporterId) ? null : exporterId;
            SchemaId = string.IsNullOrWhiteSpace(schemaId) ? payloadTypeId : schemaId;
            SchemaVersion = schemaVersion;
        }

        public string PayloadTypeId { get; }

        public string? ExporterId { get; }

        public string SchemaId { get; }

        public int SchemaVersion { get; }

        public object Payload { get; }
    }
}
