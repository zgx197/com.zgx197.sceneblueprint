#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class RuntimeSnapshotExporterRegistry
    {
        private readonly List<IRuntimeStateSnapshotExporter> _exporters = new();

        public void Register(IRuntimeStateSnapshotExporter exporter)
        {
            if (exporter is null)
            {
                throw new ArgumentNullException(nameof(exporter));
            }

            _exporters.Add(exporter);
        }

        public bool HasExporter<TExporter>()
            where TExporter : class, IRuntimeStateSnapshotExporter
        {
            for (var index = 0; index < _exporters.Count; index++)
            {
                if (_exporters[index] is TExporter)
                {
                    return true;
                }
            }

            return false;
        }

        public bool RegisterUnique<TExporter>(TExporter exporter)
            where TExporter : class, IRuntimeStateSnapshotExporter
        {
            if (exporter is null)
            {
                throw new ArgumentNullException(nameof(exporter));
            }

            if (HasExporter<TExporter>())
            {
                return false;
            }

            _exporters.Add(exporter);
            return true;
        }

        public bool TryCreateCapability(
            RuntimeStateEntryRecord entry,
            string? preferredExporterId,
            bool allowExporterFallback,
            bool isExplicitPolicy,
            string? policySourceId,
            out SnapshotCapability capability)
        {
            if (!entry.Descriptor.AllowSnapshot)
            {
                capability = new SnapshotCapability(
                    entry.EntryRef,
                    SnapshotExportMode.None,
                    SnapshotRestoreMode.None,
                    isExplicitPolicy: isExplicitPolicy,
                    policySourceId: policySourceId);
                return true;
            }

            if (TryResolveExporter(entry, preferredExporterId, allowExporterFallback, out var selection))
            {
                capability = new SnapshotCapability(
                    entry.EntryRef,
                    SnapshotExportMode.State,
                    selection.RestoreMode,
                    isExplicitPolicy,
                    exporterId: selection.PayloadDescriptor?.ExporterId,
                    payloadTypeId: selection.PayloadDescriptor?.PayloadTypeId,
                    schemaId: selection.PayloadDescriptor?.SchemaId,
                    schemaVersion: selection.PayloadDescriptor?.SchemaVersion,
                    policySourceId: policySourceId);
                return true;
            }

            capability = SnapshotCapability.Invalid;
            return false;
        }

        public bool TryCreateCapability(RuntimeStateEntryRecord entry, out SnapshotCapability capability)
        {
            return TryCreateCapability(
                entry,
                preferredExporterId: null,
                allowExporterFallback: true,
                isExplicitPolicy: true,
                policySourceId: null,
                out capability);
        }

        public bool TryExport(
            RuntimeStateEntryRecord entry,
            FieldFilter? fieldFilter,
            string? preferredExporterId,
            bool allowExporterFallback,
            out RuntimeStateSnapshotExport export)
        {
            if (!entry.Descriptor.AllowSnapshot)
            {
                export = RuntimeStateSnapshotExport.Invalid;
                return false;
            }

            var candidates = GetOrderedCandidates(entry, preferredExporterId, allowExporterFallback);
            for (var index = 0; index < candidates.Count; index++)
            {
                export = candidates[index].Exporter.Export(entry, fieldFilter);
                if (export.IsValid)
                {
                    return true;
                }
            }

            export = RuntimeStateSnapshotExport.Invalid;
            return false;
        }

        public bool TryExport(
            RuntimeStateEntryRecord entry,
            FieldFilter? fieldFilter,
            out RuntimeStateSnapshotExport export)
        {
            return TryExport(
                entry,
                fieldFilter,
                preferredExporterId: null,
                allowExporterFallback: true,
                out export);
        }

        private bool TryResolveExporter(
            RuntimeStateEntryRecord entry,
            string? preferredExporterId,
            bool allowExporterFallback,
            out RuntimeStateSnapshotExporterCandidate selection)
        {
            var candidates = GetOrderedCandidates(entry, preferredExporterId, allowExporterFallback);
            if (candidates.Count > 0)
            {
                selection = candidates[0];
                return true;
            }

            selection = default;
            return false;
        }

        private List<RuntimeStateSnapshotExporterCandidate> GetOrderedCandidates(
            RuntimeStateEntryRecord entry,
            string? preferredExporterId,
            bool allowExporterFallback)
        {
            var allCandidates = new List<RuntimeStateSnapshotExporterCandidate>();
            for (var index = 0; index < _exporters.Count; index++)
            {
                var exporter = _exporters[index];
                if (!exporter.CanExport(entry))
                {
                    continue;
                }

                var payloadDescriptor = TryDescribePayload(exporter, entry, out var descriptor)
                    ? descriptor
                    : (RuntimeStateSnapshotPayloadDescriptor?)null;
                var priority = exporter is IRuntimeStateSnapshotExporterMetadata metadata
                    ? metadata.Priority
                    : 0;
                var restoreMode = exporter.DescribeRestoreMode(entry);
                if (restoreMode == SnapshotRestoreMode.None)
                {
                    continue;
                }

                allCandidates.Add(new RuntimeStateSnapshotExporterCandidate(
                    exporter,
                    payloadDescriptor,
                    priority,
                    restoreMode,
                    registrationIndex: index));
            }

            allCandidates.Sort(RuntimeStateSnapshotExporterCandidateComparer.Instance);

            if (string.IsNullOrWhiteSpace(preferredExporterId))
            {
                return allCandidates;
            }

            var preferredCandidates = new List<RuntimeStateSnapshotExporterCandidate>();
            for (var index = 0; index < allCandidates.Count; index++)
            {
                var candidate = allCandidates[index];
                if (string.Equals(candidate.PayloadDescriptor?.ExporterId, preferredExporterId, StringComparison.Ordinal))
                {
                    preferredCandidates.Add(candidate);
                }
            }

            if (preferredCandidates.Count > 0)
            {
                return preferredCandidates;
            }

            return allowExporterFallback ? allCandidates : new List<RuntimeStateSnapshotExporterCandidate>();
        }

        private static bool TryDescribePayload(
            IRuntimeStateSnapshotExporter exporter,
            RuntimeStateEntryRecord entry,
            out RuntimeStateSnapshotPayloadDescriptor payloadDescriptor)
        {
            if (exporter is IRuntimeStateSnapshotExporterMetadata metadata
                && metadata.TryDescribePayload(entry, out payloadDescriptor))
            {
                return true;
            }

            payloadDescriptor = default;
            return false;
        }

        private readonly struct RuntimeStateSnapshotExporterCandidate
        {
            public RuntimeStateSnapshotExporterCandidate(
                IRuntimeStateSnapshotExporter exporter,
                RuntimeStateSnapshotPayloadDescriptor? payloadDescriptor,
                int priority,
                SnapshotRestoreMode restoreMode,
                int registrationIndex)
            {
                Exporter = exporter;
                PayloadDescriptor = payloadDescriptor;
                Priority = priority;
                RestoreMode = restoreMode;
                RegistrationIndex = registrationIndex;
            }

            public IRuntimeStateSnapshotExporter Exporter { get; }

            public RuntimeStateSnapshotPayloadDescriptor? PayloadDescriptor { get; }

            public int Priority { get; }

            public SnapshotRestoreMode RestoreMode { get; }

            public int RegistrationIndex { get; }
        }

        private sealed class RuntimeStateSnapshotExporterCandidateComparer : IComparer<RuntimeStateSnapshotExporterCandidate>
        {
            public static readonly RuntimeStateSnapshotExporterCandidateComparer Instance = new();

            public int Compare(RuntimeStateSnapshotExporterCandidate x, RuntimeStateSnapshotExporterCandidate y)
            {
                var priorityComparison = y.Priority.CompareTo(x.Priority);
                if (priorityComparison != 0)
                {
                    return priorityComparison;
                }

                return x.RegistrationIndex.CompareTo(y.RegistrationIndex);
            }
        }
    }
}
