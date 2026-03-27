#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public readonly struct RuntimeSnapshotPolicy
    {
        public static RuntimeSnapshotPolicy Invalid => default;

        public RuntimeSnapshotPolicy(
            SnapshotExportMode exportMode,
            bool isExplicitPolicy = true,
            string? preferredExporterId = null,
            string? policySourceId = null,
            bool allowExporterFallback = true,
            bool allowSummaryFallback = true)
        {
            ExportMode = exportMode;
            IsExplicitPolicy = isExplicitPolicy;
            PreferredExporterId = string.IsNullOrWhiteSpace(preferredExporterId) ? null : preferredExporterId;
            PolicySourceId = string.IsNullOrWhiteSpace(policySourceId) ? null : policySourceId;
            AllowExporterFallback = allowExporterFallback;
            AllowSummaryFallback = allowSummaryFallback;
        }

        public SnapshotExportMode ExportMode { get; }

        public bool IsExplicitPolicy { get; }

        public string? PreferredExporterId { get; }

        public string? PolicySourceId { get; }

        public bool AllowExporterFallback { get; }

        public bool AllowSummaryFallback { get; }

        public bool IsValid => ExportMode is SnapshotExportMode.None or SnapshotExportMode.Summary or SnapshotExportMode.State;
    }

    public interface IRuntimeStateSnapshotPolicyProvider
    {
        bool TryGetSnapshotPolicy(RuntimeStateEntryRecord entry, out RuntimeSnapshotPolicy policy);
    }
}
