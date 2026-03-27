#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public enum SnapshotReplayStatus
    {
        Restored = 0,
        Skipped = 1,
        Failed = 2,
    }

    public sealed class RuntimeSnapshotReplayEntryResult
    {
        public RuntimeSnapshotReplayEntryResult(
            string logicalEntryKey,
            SnapshotReplayStatus status,
            string? message = null,
            RuntimeEntryRef? restoredEntryRef = null,
            int? sourceSchemaVersion = null,
            int? targetSchemaVersion = null)
        {
            if (string.IsNullOrWhiteSpace(logicalEntryKey))
            {
                throw new ArgumentException("Logical entry key cannot be null or whitespace.", nameof(logicalEntryKey));
            }

            LogicalEntryKey = logicalEntryKey;
            Status = status;
            Message = string.IsNullOrWhiteSpace(message) ? null : message;
            RestoredEntryRef = restoredEntryRef;
            SourceSchemaVersion = sourceSchemaVersion;
            TargetSchemaVersion = targetSchemaVersion;
        }

        public string LogicalEntryKey { get; }

        public SnapshotReplayStatus Status { get; }

        public string? Message { get; }

        public RuntimeEntryRef? RestoredEntryRef { get; }

        public int? SourceSchemaVersion { get; }

        public int? TargetSchemaVersion { get; }

        public static RuntimeSnapshotReplayEntryResult Restored(
            string logicalEntryKey,
            RuntimeEntryRef restoredEntryRef,
            int? sourceSchemaVersion = null,
            int? targetSchemaVersion = null,
            string? message = null)
        {
            return new RuntimeSnapshotReplayEntryResult(
                logicalEntryKey,
                SnapshotReplayStatus.Restored,
                message,
                restoredEntryRef,
                sourceSchemaVersion,
                targetSchemaVersion);
        }

        public static RuntimeSnapshotReplayEntryResult Skipped(
            string logicalEntryKey,
            string? message = null,
            int? sourceSchemaVersion = null,
            int? targetSchemaVersion = null)
        {
            return new RuntimeSnapshotReplayEntryResult(
                logicalEntryKey,
                SnapshotReplayStatus.Skipped,
                message,
                restoredEntryRef: null,
                sourceSchemaVersion,
                targetSchemaVersion);
        }

        public static RuntimeSnapshotReplayEntryResult Failed(
            string logicalEntryKey,
            string? message = null,
            int? sourceSchemaVersion = null,
            int? targetSchemaVersion = null)
        {
            return new RuntimeSnapshotReplayEntryResult(
                logicalEntryKey,
                SnapshotReplayStatus.Failed,
                message,
                restoredEntryRef: null,
                sourceSchemaVersion,
                targetSchemaVersion);
        }

        public RuntimeSnapshotReplayEntryResult WithSchemaVersions(
            int? sourceSchemaVersion,
            int? targetSchemaVersion)
        {
            return new RuntimeSnapshotReplayEntryResult(
                LogicalEntryKey,
                Status,
                Message,
                RestoredEntryRef,
                sourceSchemaVersion,
                targetSchemaVersion);
        }
    }

    public sealed class RuntimeSnapshotReplayResult
    {
        public RuntimeSnapshotReplayResult(
            string snapshotId,
            IReadOnlyList<RuntimeSnapshotReplayEntryResult>? entries,
            string? tag = null)
        {
            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                throw new ArgumentException("Snapshot id cannot be null or whitespace.", nameof(snapshotId));
            }

            SnapshotId = snapshotId;
            Entries = entries ?? Array.Empty<RuntimeSnapshotReplayEntryResult>();
            Tag = string.IsNullOrWhiteSpace(tag) ? null : tag;
        }

        public string SnapshotId { get; }

        public string? Tag { get; }

        public IReadOnlyList<RuntimeSnapshotReplayEntryResult> Entries { get; }

        public int RestoredCount => CountByStatus(SnapshotReplayStatus.Restored);

        public int SkippedCount => CountByStatus(SnapshotReplayStatus.Skipped);

        public int FailedCount => CountByStatus(SnapshotReplayStatus.Failed);

        public bool HasFailures => FailedCount > 0;

        private int CountByStatus(SnapshotReplayStatus status)
        {
            var count = 0;
            for (var index = 0; index < Entries.Count; index++)
            {
                if (Entries[index].Status == status)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public readonly struct SnapshotLogicalEntryRoute
    {
        public SnapshotLogicalEntryRoute(
            StateDomainId domainId,
            OwnerKind ownerKind,
            string ownerLogicalKey,
            string slotKey,
            string? runtimeInstanceId = null)
        {
            if (!domainId.IsValid)
            {
                throw new ArgumentException("Domain id must be valid.", nameof(domainId));
            }

            if (ownerKind == OwnerKind.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "Owner kind cannot be Unknown.");
            }

            if (string.IsNullOrWhiteSpace(ownerLogicalKey))
            {
                throw new ArgumentException("Owner logical key cannot be null or whitespace.", nameof(ownerLogicalKey));
            }

            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("Slot key cannot be null or whitespace.", nameof(slotKey));
            }

            DomainId = domainId;
            OwnerKind = ownerKind;
            OwnerLogicalKey = ownerLogicalKey;
            SlotKey = slotKey;
            RuntimeInstanceId = string.IsNullOrWhiteSpace(runtimeInstanceId) ? null : runtimeInstanceId;
        }

        public StateDomainId DomainId { get; }

        public OwnerKind OwnerKind { get; }

        public string OwnerLogicalKey { get; }

        public string SlotKey { get; }

        public string? RuntimeInstanceId { get; }

        public StateOwnerRef CreateOwnerRef()
        {
            return new StateOwnerRef(OwnerKind, OwnerLogicalKey, RuntimeInstanceId);
        }

        public static bool TryParse(SnapshotEntry entry, out SnapshotLogicalEntryRoute route)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var parts = entry.LogicalEntryKey.Split('|');
            if (parts.Length != 4
                || !Enum.TryParse(parts[1], ignoreCase: false, out OwnerKind ownerKind)
                || ownerKind == OwnerKind.Unknown)
            {
                route = default;
                return false;
            }

            try
            {
                var domainId = new StateDomainId(parts[0]);
                var runtimeInstanceId = string.IsNullOrWhiteSpace(entry.RuntimeInstanceId) ? null : entry.RuntimeInstanceId;
                route = new SnapshotLogicalEntryRoute(domainId, ownerKind, parts[2], parts[3], runtimeInstanceId);
                return true;
            }
            catch
            {
                route = default;
                return false;
            }
        }
    }

    public sealed class RuntimeSnapshotReplayContext
    {
        private readonly IRuntimeStateBackend _backend;
        private readonly IReadOnlyList<IRuntimeStateDomain> _domains;
        private readonly RuntimeSnapshotReplaySession? _session;

        public RuntimeSnapshotReplayContext(
            IRuntimeStateBackend backend,
            IReadOnlyList<IRuntimeStateDomain> domains,
            SnapshotEntry snapshotEntry)
            : this(backend, domains, snapshotEntry, session: null)
        {
        }

        internal RuntimeSnapshotReplayContext(
            IRuntimeStateBackend backend,
            IReadOnlyList<IRuntimeStateDomain> domains,
            SnapshotEntry snapshotEntry,
            RuntimeSnapshotReplaySession? session)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _domains = domains ?? throw new ArgumentNullException(nameof(domains));
            SnapshotEntry = snapshotEntry ?? throw new ArgumentNullException(nameof(snapshotEntry));
            _session = session;
        }

        public SnapshotEntry SnapshotEntry { get; }

        public bool TryResolveRoute(out SnapshotLogicalEntryRoute route)
        {
            return SnapshotLogicalEntryRoute.TryParse(SnapshotEntry, out route);
        }

        public bool TryGetSnapshotEntry(string logicalEntryKey, out SnapshotEntry snapshotEntry)
        {
            if (_session is not null && _session.TryGetSnapshotEntry(logicalEntryKey, out snapshotEntry))
            {
                return true;
            }

            snapshotEntry = null!;
            return false;
        }

        public bool TryResolveRoute(string logicalEntryKey, out SnapshotLogicalEntryRoute route)
        {
            if (_session is not null && _session.TryResolveRoute(logicalEntryKey, out route))
            {
                return true;
            }

            route = default;
            return false;
        }

        public bool TryGetReplayResult(string logicalEntryKey, out RuntimeSnapshotReplayEntryResult replayResult)
        {
            if (_session is not null && _session.TryGetResult(logicalEntryKey, out replayResult))
            {
                return true;
            }

            replayResult = null!;
            return false;
        }

        public bool TryGetRestoredEntryRef(string logicalEntryKey, out RuntimeEntryRef entryRef)
        {
            if (_session is not null && _session.TryGetRestoredEntryRef(logicalEntryKey, out entryRef))
            {
                return true;
            }

            entryRef = default;
            return false;
        }

        public bool TryGetDomain(StateDomainId domainId, out IRuntimeStateDomain domain)
        {
            for (var index = 0; index < _domains.Count; index++)
            {
                if (_domains[index].DomainId == domainId)
                {
                    domain = _domains[index];
                    return true;
                }
            }

            domain = null!;
            return false;
        }

        public RuntimeEntryRef EnsureEntry(StateDescriptor descriptor, StateOwnerRef ownerRef, string slotKey)
        {
            if (!TryGetDomain(descriptor.DomainId, out var domain))
            {
                throw new InvalidOperationException($"Runtime state domain is not registered: {descriptor.DomainId}");
            }

            return domain.EnsureEntry(descriptor, ownerRef, slotKey);
        }

        public void SetState(RuntimeEntryRef entryRef, object? state)
        {
            if (!_backend.TryGetEntry(entryRef, out var entry) || entry is null)
            {
                throw new KeyNotFoundException($"Runtime state entry was not found: {entryRef}");
            }

            entry.State = state;
        }
    }

    internal sealed class RuntimeSnapshotReplaySession
    {
        private readonly Dictionary<string, SnapshotEntry> _entriesByLogicalKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeSnapshotReplayEntryResult> _resultsByLogicalKey = new(StringComparer.Ordinal);

        public RuntimeSnapshotReplaySession(RuntimeSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                var entry = snapshot.Entries[index];
                if (!_entriesByLogicalKey.ContainsKey(entry.LogicalEntryKey))
                {
                    _entriesByLogicalKey.Add(entry.LogicalEntryKey, entry);
                }
            }
        }

        public bool TryGetSnapshotEntry(string logicalEntryKey, out SnapshotEntry snapshotEntry)
        {
            return _entriesByLogicalKey.TryGetValue(logicalEntryKey, out snapshotEntry!);
        }

        public bool TryResolveRoute(string logicalEntryKey, out SnapshotLogicalEntryRoute route)
        {
            if (_entriesByLogicalKey.TryGetValue(logicalEntryKey, out var entry))
            {
                return SnapshotLogicalEntryRoute.TryParse(entry, out route);
            }

            route = default;
            return false;
        }

        public void RecordResult(RuntimeSnapshotReplayEntryResult result)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _resultsByLogicalKey[result.LogicalEntryKey] = result;
        }

        public bool TryGetResult(string logicalEntryKey, out RuntimeSnapshotReplayEntryResult replayResult)
        {
            return _resultsByLogicalKey.TryGetValue(logicalEntryKey, out replayResult!);
        }

        public bool TryGetReplayResult(string logicalEntryKey, out RuntimeSnapshotReplayEntryResult replayResult)
        {
            return TryGetResult(logicalEntryKey, out replayResult);
        }

        public bool TryGetRestoredEntryRef(string logicalEntryKey, out RuntimeEntryRef entryRef)
        {
            if (_resultsByLogicalKey.TryGetValue(logicalEntryKey, out var result)
                && result.Status == SnapshotReplayStatus.Restored
                && result.RestoredEntryRef.HasValue)
            {
                entryRef = result.RestoredEntryRef.Value;
                return true;
            }

            entryRef = default;
            return false;
        }
    }
}
