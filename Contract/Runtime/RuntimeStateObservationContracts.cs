#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    public enum ObservationTargetKind
    {
        Unknown = 0,
        Host = 1,
        Domain = 2,
        Entry = 3,
    }

    public enum FieldFilterMode
    {
        AllowAll = 0,
        IncludeListed = 1,
        ExcludeListed = 2,
    }

    public enum ObservationValueKind
    {
        Scalar = 0,
        Object = 1,
        Collection = 2,
        Null = 3,
        Summary = 4,
    }

    public enum SnapshotExportMode
    {
        None = 0,
        Summary = 1,
        State = 2,
    }

    public enum SnapshotRestoreMode
    {
        None = 0,
        Rebuild = 1,
        Direct = 2,
    }

    public enum SnapshotPayloadKind
    {
        None = 0,
        SummaryFields = 1,
        StatePayload = 2,
    }

    public readonly struct FieldFilter
    {
        public FieldFilter(FieldFilterMode mode, IReadOnlyList<string>? paths = null)
        {
            Paths = paths ?? Array.Empty<string>();
            Mode = mode;
        }

        public FieldFilterMode Mode { get; }

        public IReadOnlyList<string> Paths { get; }
    }

    public readonly struct ObservationFilter
    {
        public ObservationFilter(StateOwnerRef? ownerRef = null, bool includeChildren = false, int? maxDepth = null, FieldFilter? fieldFilter = null)
        {
            if (maxDepth.HasValue && maxDepth.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth.Value, "Max depth cannot be negative.");
            }

            OwnerRef = ownerRef;
            IncludeChildren = includeChildren;
            MaxDepth = maxDepth;
            FieldFilter = fieldFilter;
        }

        public StateOwnerRef? OwnerRef { get; }

        public bool IncludeChildren { get; }

        public int? MaxDepth { get; }

        public FieldFilter? FieldFilter { get; }
    }

    public readonly struct ObservationRequest
    {
        public static ObservationRequest Invalid => default;

        private ObservationRequest(
            ObservationTargetKind targetKind,
            StateDomainId? domainId,
            RuntimeEntryRef? entryRef,
            bool includeChildren,
            int? maxDepth,
            FieldFilter? fieldFilter)
        {
            if (targetKind == ObservationTargetKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Target kind cannot be Unknown.");
            }

            if (maxDepth.HasValue && maxDepth.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth.Value, "Max depth cannot be negative.");
            }

            switch (targetKind)
            {
                case ObservationTargetKind.Host:
                    if (domainId.HasValue || entryRef.HasValue)
                    {
                        throw new ArgumentException("Host observation cannot carry domain or entry scope.");
                    }
                    break;
                case ObservationTargetKind.Domain:
                    if (!domainId.HasValue || !domainId.Value.IsValid)
                    {
                        throw new ArgumentException("Domain observation must carry a valid domain id.", nameof(domainId));
                    }
                    if (entryRef.HasValue)
                    {
                        throw new ArgumentException("Domain observation cannot carry an entry reference.", nameof(entryRef));
                    }
                    break;
                case ObservationTargetKind.Entry:
                    if (!entryRef.HasValue || !entryRef.Value.IsValid)
                    {
                        throw new ArgumentException("Entry observation must carry a valid entry reference.", nameof(entryRef));
                    }
                    if (domainId.HasValue && domainId.Value != entryRef.Value.DomainId)
                    {
                        throw new ArgumentException("Explicit domain id must match entry reference domain id.", nameof(domainId));
                    }
                    domainId = entryRef.Value.DomainId;
                    break;
            }

            TargetKind = targetKind;
            DomainId = domainId;
            EntryRef = entryRef;
            IncludeChildren = includeChildren;
            MaxDepth = maxDepth;
            FieldFilter = fieldFilter;
        }

        public ObservationTargetKind TargetKind { get; }

        public StateDomainId? DomainId { get; }

        public RuntimeEntryRef? EntryRef { get; }

        public bool IncludeChildren { get; }

        public int? MaxDepth { get; }

        public FieldFilter? FieldFilter { get; }

        public bool IsValid => TargetKind != ObservationTargetKind.Unknown;

        public static ObservationRequest ForHost(bool includeChildren = false, int? maxDepth = null, FieldFilter? fieldFilter = null)
        {
            return new ObservationRequest(ObservationTargetKind.Host, null, null, includeChildren, maxDepth, fieldFilter);
        }

        public static ObservationRequest ForDomain(StateDomainId domainId, bool includeChildren = false, int? maxDepth = null, FieldFilter? fieldFilter = null)
        {
            return new ObservationRequest(ObservationTargetKind.Domain, domainId, null, includeChildren, maxDepth, fieldFilter);
        }

        public static ObservationRequest ForEntry(RuntimeEntryRef entryRef, bool includeChildren = false, int? maxDepth = null, FieldFilter? fieldFilter = null)
        {
            return new ObservationRequest(ObservationTargetKind.Entry, entryRef.DomainId, entryRef, includeChildren, maxDepth, fieldFilter);
        }

        public override string ToString()
        {
            return TargetKind switch
            {
                ObservationTargetKind.Host => "Observation(Host)",
                ObservationTargetKind.Domain => $"Observation(Domain:{DomainId})",
                ObservationTargetKind.Entry => $"Observation(Entry:{EntryRef})",
                _ => string.Empty,
            };
        }
    }

    public sealed class ObservationFieldNode
    {
        public ObservationFieldNode(
            string path,
            string fieldName,
            ObservationValueKind valueKind,
            string? valueSummary = null,
            string? typeName = null,
            IReadOnlyList<string>? tags = null,
            IReadOnlyList<ObservationFieldNode>? children = null,
            bool isTruncated = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw new ArgumentException("Field name cannot be null or whitespace.", nameof(fieldName));
            }

            Path = path;
            FieldName = fieldName;
            ValueKind = valueKind;
            ValueSummary = valueSummary;
            TypeName = typeName;
            Tags = tags;
            Children = children ?? Array.Empty<ObservationFieldNode>();
            IsTruncated = isTruncated;
        }

        public string Path { get; }

        public string FieldName { get; }

        public ObservationValueKind ValueKind { get; }

        public string? ValueSummary { get; }

        public string? TypeName { get; }

        public IReadOnlyList<string>? Tags { get; }

        public IReadOnlyList<ObservationFieldNode> Children { get; }

        public bool IsTruncated { get; }
    }

    public sealed class ObservationEntry
    {
        public ObservationEntry(
            string logicalEntryKey,
            string sampleId,
            StateDomainId domainId,
            ObservationFieldNode rootField,
            string? runtimeInstanceId = null,
            RuntimeEntryRef? entryRef = null)
        {
            if (string.IsNullOrWhiteSpace(logicalEntryKey))
            {
                throw new ArgumentException("Logical entry key cannot be null or whitespace.", nameof(logicalEntryKey));
            }

            if (string.IsNullOrWhiteSpace(sampleId))
            {
                throw new ArgumentException("Sample id cannot be null or whitespace.", nameof(sampleId));
            }

            if (!domainId.IsValid)
            {
                throw new ArgumentException("Domain id must be valid.", nameof(domainId));
            }

            LogicalEntryKey = logicalEntryKey;
            RuntimeInstanceId = runtimeInstanceId;
            SampleId = sampleId;
            DomainId = domainId;
            EntryRef = entryRef;
            RootField = rootField ?? throw new ArgumentNullException(nameof(rootField));
        }

        public string LogicalEntryKey { get; }

        public string? RuntimeInstanceId { get; }

        public string SampleId { get; }

        public StateDomainId DomainId { get; }

        public RuntimeEntryRef? EntryRef { get; }

        public ObservationFieldNode RootField { get; }
    }

    public sealed class ObservationResult
    {
        public ObservationResult(
            ObservationTargetKind targetKind,
            IReadOnlyList<ObservationEntry>? entries,
            RuntimeTick? generatedAtTick = null,
            FieldFilter? appliedFieldFilter = null)
        {
            if (targetKind == ObservationTargetKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Target kind cannot be Unknown.");
            }

            TargetKind = targetKind;
            GeneratedAtTick = generatedAtTick;
            AppliedFieldFilter = appliedFieldFilter;
            Entries = entries ?? Array.Empty<ObservationEntry>();
        }

        public ObservationTargetKind TargetKind { get; }

        public RuntimeTick? GeneratedAtTick { get; }

        public FieldFilter? AppliedFieldFilter { get; }

        public IReadOnlyList<ObservationEntry> Entries { get; }
    }

    public readonly struct SnapshotRequest
    {
        public static SnapshotRequest Invalid => default;

        private SnapshotRequest(
            ObservationTargetKind targetKind,
            StateDomainId? domainId,
            RuntimeEntryRef? entryRef,
            IReadOnlyList<StateDomainId>? includedDomains,
            FieldFilter? fieldFilter,
            string? tag)
        {
            if (targetKind == ObservationTargetKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Target kind cannot be Unknown.");
            }

            switch (targetKind)
            {
                case ObservationTargetKind.Host:
                    if (domainId.HasValue || entryRef.HasValue)
                    {
                        throw new ArgumentException("Host snapshot cannot carry domain or entry scope.");
                    }
                    break;
                case ObservationTargetKind.Domain:
                    if (!domainId.HasValue || !domainId.Value.IsValid)
                    {
                        throw new ArgumentException("Domain snapshot must carry a valid domain id.", nameof(domainId));
                    }
                    if (entryRef.HasValue)
                    {
                        throw new ArgumentException("Domain snapshot cannot carry an entry reference.", nameof(entryRef));
                    }
                    break;
                case ObservationTargetKind.Entry:
                    if (!entryRef.HasValue || !entryRef.Value.IsValid)
                    {
                        throw new ArgumentException("Entry snapshot must carry a valid entry reference.", nameof(entryRef));
                    }
                    if (domainId.HasValue && domainId.Value != entryRef.Value.DomainId)
                    {
                        throw new ArgumentException("Explicit domain id must match entry reference domain id.", nameof(domainId));
                    }
                    domainId = entryRef.Value.DomainId;
                    break;
            }

            TargetKind = targetKind;
            DomainId = domainId;
            EntryRef = entryRef;
            IncludedDomains = includedDomains;
            FieldFilter = fieldFilter;
            Tag = string.IsNullOrWhiteSpace(tag) ? null : tag;
        }

        public ObservationTargetKind TargetKind { get; }

        public StateDomainId? DomainId { get; }

        public RuntimeEntryRef? EntryRef { get; }

        public IReadOnlyList<StateDomainId>? IncludedDomains { get; }

        public FieldFilter? FieldFilter { get; }

        public string? Tag { get; }

        public bool IsValid => TargetKind != ObservationTargetKind.Unknown;

        public static SnapshotRequest ForHost(IReadOnlyList<StateDomainId>? includedDomains = null, FieldFilter? fieldFilter = null, string? tag = null)
        {
            return new SnapshotRequest(ObservationTargetKind.Host, null, null, includedDomains, fieldFilter, tag);
        }

        public static SnapshotRequest ForDomain(StateDomainId domainId, FieldFilter? fieldFilter = null, string? tag = null)
        {
            return new SnapshotRequest(ObservationTargetKind.Domain, domainId, null, null, fieldFilter, tag);
        }

        public static SnapshotRequest ForEntry(RuntimeEntryRef entryRef, FieldFilter? fieldFilter = null, string? tag = null)
        {
            return new SnapshotRequest(ObservationTargetKind.Entry, entryRef.DomainId, entryRef, null, fieldFilter, tag);
        }

        public override string ToString()
        {
            return TargetKind switch
            {
                ObservationTargetKind.Host => "Snapshot(Host)",
                ObservationTargetKind.Domain => $"Snapshot(Domain:{DomainId})",
                ObservationTargetKind.Entry => $"Snapshot(Entry:{EntryRef})",
                _ => string.Empty,
            };
        }
    }

    public readonly struct SnapshotCapability
    {
        public static SnapshotCapability Invalid => default;

        public SnapshotCapability(
            RuntimeEntryRef entryRef,
            SnapshotExportMode exportMode,
            SnapshotRestoreMode restoreMode,
            bool isExplicitPolicy,
            string? exporterId = null,
            string? payloadTypeId = null,
            string? schemaId = null,
            int? schemaVersion = null,
            string? policySourceId = null)
        {
            if (!entryRef.IsValid)
            {
                throw new ArgumentException("Entry reference must be valid.", nameof(entryRef));
            }

            if (exportMode == SnapshotExportMode.State && restoreMode == SnapshotRestoreMode.None)
            {
                throw new ArgumentException("State snapshot capability must define a restore mode.", nameof(restoreMode));
            }

            if (exportMode != SnapshotExportMode.State && restoreMode != SnapshotRestoreMode.None)
            {
                throw new ArgumentException("Non-state snapshot capability cannot define a restore mode.", nameof(restoreMode));
            }

            if (schemaVersion.HasValue && schemaVersion.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion.Value, "Schema version must be greater than zero.");
            }

            EntryRef = entryRef;
            ExportMode = exportMode;
            RestoreMode = restoreMode;
            IsExplicitPolicy = isExplicitPolicy;
            ExporterId = string.IsNullOrWhiteSpace(exporterId) ? null : exporterId;
            PayloadTypeId = string.IsNullOrWhiteSpace(payloadTypeId) ? null : payloadTypeId;
            SchemaId = string.IsNullOrWhiteSpace(schemaId) ? null : schemaId;
            SchemaVersion = schemaVersion;
            PolicySourceId = string.IsNullOrWhiteSpace(policySourceId) ? null : policySourceId;
        }

        public RuntimeEntryRef EntryRef { get; }

        public SnapshotExportMode ExportMode { get; }

        public SnapshotRestoreMode RestoreMode { get; }

        public bool IsExplicitPolicy { get; }

        public string? ExporterId { get; }

        public string? PayloadTypeId { get; }

        public string? SchemaId { get; }

        public int? SchemaVersion { get; }

        public string? PolicySourceId { get; }

        public bool IsValid => EntryRef.IsValid;
    }

    public sealed class SnapshotEntry
    {
        public SnapshotEntry(
            string logicalEntryKey,
            StateDomainId domainId,
            SnapshotExportMode exportMode,
            SnapshotRestoreMode restoreMode,
            SnapshotPayloadKind payloadKind,
            object? payload,
            string? runtimeInstanceId = null,
            RuntimeEntryRef? entryRef = null)
        {
            if (string.IsNullOrWhiteSpace(logicalEntryKey))
            {
                throw new ArgumentException("Logical entry key cannot be null or whitespace.", nameof(logicalEntryKey));
            }

            if (!domainId.IsValid)
            {
                throw new ArgumentException("Domain id must be valid.", nameof(domainId));
            }

            LogicalEntryKey = logicalEntryKey;
            RuntimeInstanceId = runtimeInstanceId;
            DomainId = domainId;
            EntryRef = entryRef;
            ExportMode = exportMode;
            RestoreMode = restoreMode;
            PayloadKind = payloadKind;
            Payload = payload;
        }

        public string LogicalEntryKey { get; }

        public string? RuntimeInstanceId { get; }

        public StateDomainId DomainId { get; }

        public RuntimeEntryRef? EntryRef { get; }

        public SnapshotExportMode ExportMode { get; }

        public SnapshotRestoreMode RestoreMode { get; }

        public SnapshotPayloadKind PayloadKind { get; }

        public object? Payload { get; }
    }

    public sealed class RuntimeSnapshot
    {
        public RuntimeSnapshot(
            string snapshotId,
            ObservationTargetKind targetKind,
            IReadOnlyList<SnapshotEntry>? entries,
            RuntimeTick? capturedAtTick = null,
            string? tag = null,
            FieldFilter? appliedFieldFilter = null)
        {
            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                throw new ArgumentException("Snapshot id cannot be null or whitespace.", nameof(snapshotId));
            }

            if (targetKind == ObservationTargetKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Target kind cannot be Unknown.");
            }

            SnapshotId = snapshotId;
            TargetKind = targetKind;
            CapturedAtTick = capturedAtTick;
            Tag = string.IsNullOrWhiteSpace(tag) ? null : tag;
            AppliedFieldFilter = appliedFieldFilter;
            Entries = entries ?? Array.Empty<SnapshotEntry>();
        }

        public string SnapshotId { get; }

        public ObservationTargetKind TargetKind { get; }

        public RuntimeTick? CapturedAtTick { get; }

        public string? Tag { get; }

        public FieldFilter? AppliedFieldFilter { get; }

        public IReadOnlyList<SnapshotEntry> Entries { get; }
    }
}
