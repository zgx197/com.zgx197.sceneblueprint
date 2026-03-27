#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class RuntimeSnapshotService : IRuntimeSnapshotService
    {
        private readonly IRuntimeStateBackend _backend;
        private readonly Func<IReadOnlyList<IRuntimeStateDomain>> _domainsProvider;
        private readonly RuntimeSnapshotExporterRegistry _exporterRegistry;
        private readonly RuntimeSnapshotSchemaRegistry _schemaRegistry;
        private readonly RuntimeSnapshotReplayRegistry _replayRegistry;

        public RuntimeSnapshotService(
            IRuntimeStateBackend backend,
            Func<IReadOnlyList<IRuntimeStateDomain>>? domainsProvider = null,
            RuntimeSnapshotExporterRegistry? exporterRegistry = null,
            RuntimeSnapshotSchemaRegistry? schemaRegistry = null,
            RuntimeSnapshotReplayRegistry? replayRegistry = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _domainsProvider = domainsProvider ?? (() => Array.Empty<IRuntimeStateDomain>());
            _exporterRegistry = exporterRegistry ?? new RuntimeSnapshotExporterRegistry();
            _schemaRegistry = schemaRegistry ?? new RuntimeSnapshotSchemaRegistry();
            _replayRegistry = replayRegistry ?? new RuntimeSnapshotReplayRegistry();
        }

        public RuntimeSnapshot Capture(SnapshotRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Snapshot request must be valid.", nameof(request));
            }

            var domains = _domainsProvider();
            var records = RuntimeStateProjectionSupport.ResolveEntries(
                request.TargetKind,
                request.DomainId,
                request.EntryRef,
                request.IncludedDomains,
                _backend,
                () => domains);

            var entries = new List<SnapshotEntry>(records.Count);
            for (var index = 0; index < records.Count; index++)
            {
                entries.Add(RuntimeStateProjectionSupport.CreateSnapshotEntry(records[index], request.FieldFilter, domains, _exporterRegistry));
            }

            return new RuntimeSnapshot(
                CreateSnapshotId(),
                request.TargetKind,
                entries,
                capturedAtTick: null,
                tag: request.Tag,
                appliedFieldFilter: request.FieldFilter);
        }

        public SnapshotCapability DescribeCapability(RuntimeEntryRef entryRef)
        {
            if (!entryRef.IsValid)
            {
                return SnapshotCapability.Invalid;
            }

            if (!_backend.TryGetEntry(entryRef, out var entry) || entry is null)
            {
                return SnapshotCapability.Invalid;
            }

            var domains = _domainsProvider();
            return RuntimeStateProjectionSupport.CreateCapability(entry, domains, _exporterRegistry);
        }

        public RuntimeSnapshotReplayResult Replay(RuntimeSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var domains = _domainsProvider();
            var session = new RuntimeSnapshotReplaySession(snapshot);
            var results = new RuntimeSnapshotReplayEntryResult?[snapshot.Entries.Count];
            var preparedEntries = new List<PreparedReplayEntry>(snapshot.Entries.Count);
            var logicalKeys = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                var entry = snapshot.Entries[index];
                if (!logicalKeys.Add(entry.LogicalEntryKey))
                {
                    results[index] = RuntimeSnapshotReplayEntryResult.Failed(
                        entry.LogicalEntryKey,
                        $"Duplicate snapshot logical entry key was detected: {entry.LogicalEntryKey}.");
                    session.RecordResult(results[index]!);
                    continue;
                }

                if (!TryPrepareReplayEntry(entry, index, domains, session, out var preparedEntry, out var preparationFailure))
                {
                    results[index] = preparationFailure!;
                    session.RecordResult(preparationFailure!);
                    continue;
                }

                preparedEntries.Add(preparedEntry!);
            }

            ExecuteReplayPlan(preparedEntries, domains, results, session);

            var finalizedResults = new RuntimeSnapshotReplayEntryResult[results.Length];
            for (var index = 0; index < results.Length; index++)
            {
                finalizedResults[index] = results[index]
                    ?? RuntimeSnapshotReplayEntryResult.Skipped(
                        snapshot.Entries[index].LogicalEntryKey,
                        "Snapshot entry was not replayed.");
            }

            return new RuntimeSnapshotReplayResult(snapshot.SnapshotId, finalizedResults, snapshot.Tag);
        }

        private static string CreateSnapshotId()
        {
            return $"snapshot:{Guid.NewGuid():N}";
        }

        private bool TryPrepareReplayEntry(
            SnapshotEntry entry,
            int originalIndex,
            IReadOnlyList<IRuntimeStateDomain> domains,
            RuntimeSnapshotReplaySession session,
            out PreparedReplayEntry? preparedEntry,
            out RuntimeSnapshotReplayEntryResult? failureResult)
        {
            if (entry.ExportMode != SnapshotExportMode.State)
            {
                preparedEntry = null;
                failureResult = RuntimeSnapshotReplayEntryResult.Skipped(
                    entry.LogicalEntryKey,
                    $"Snapshot entry export mode '{entry.ExportMode}' is not replayable.");
                return false;
            }

            if (entry.RestoreMode == SnapshotRestoreMode.None)
            {
                preparedEntry = null;
                failureResult = RuntimeSnapshotReplayEntryResult.Skipped(
                    entry.LogicalEntryKey,
                    "Snapshot entry restore mode is None.");
                return false;
            }

            if (entry.PayloadKind != SnapshotPayloadKind.StatePayload || entry.Payload is not RuntimeStateSnapshotPayload payload)
            {
                preparedEntry = null;
                failureResult = RuntimeSnapshotReplayEntryResult.Skipped(
                    entry.LogicalEntryKey,
                    "Snapshot entry does not carry a state payload.");
                return false;
            }

            var sourceVersion = payload.SchemaVersion;
            var migratedPayload = payload;
            if (!_schemaRegistry.TryMigrateToLatest(payload, out migratedPayload, out var migrationError))
            {
                preparedEntry = null;
                failureResult = RuntimeSnapshotReplayEntryResult.Failed(
                    entry.LogicalEntryKey,
                    migrationError,
                    sourceVersion,
                    sourceVersion);
                return false;
            }

            if (!_replayRegistry.TryResolveReplayer(migratedPayload, entry.RestoreMode, out var replayer))
            {
                preparedEntry = null;
                failureResult = RuntimeSnapshotReplayEntryResult.Failed(
                    entry.LogicalEntryKey,
                    $"No replay handler was found for schema '{migratedPayload.SchemaId}'.",
                    sourceVersion,
                    migratedPayload.SchemaVersion);
                return false;
            }

            var context = new RuntimeSnapshotReplayContext(_backend, domains, entry, session);
            var priority = 0;
            IReadOnlyList<string>? dependencies = null;
            if (replayer is IRuntimeStateSnapshotReplayPlanner planner)
            {
                priority = planner.GetReplayPriority(context, migratedPayload, entry.RestoreMode);
                dependencies = planner.GetReplayDependencies(context, migratedPayload, entry.RestoreMode);
            }

            preparedEntry = new PreparedReplayEntry(
                originalIndex,
                entry,
                migratedPayload,
                sourceVersion,
                migratedPayload.SchemaVersion,
                replayer,
                priority,
                NormalizeDependencies(dependencies));
            failureResult = null;
            return true;
        }

        private void ExecuteReplayPlan(
            IReadOnlyList<PreparedReplayEntry> preparedEntries,
            IReadOnlyList<IRuntimeStateDomain> domains,
            RuntimeSnapshotReplayEntryResult?[] results,
            RuntimeSnapshotReplaySession session)
        {
            var pending = new List<PreparedReplayEntry>(preparedEntries.Count);
            var preparedByKey = new Dictionary<string, PreparedReplayEntry>(StringComparer.Ordinal);
            for (var index = 0; index < preparedEntries.Count; index++)
            {
                var prepared = preparedEntries[index];
                preparedByKey[prepared.Entry.LogicalEntryKey] = prepared;
                pending.Add(prepared);
            }

            var valid = new List<PreparedReplayEntry>(pending.Count);
            for (var index = 0; index < pending.Count; index++)
            {
                var prepared = pending[index];
                var missingDependency = FindMissingDependency(prepared, preparedByKey);
                if (missingDependency is not null)
                {
                    var failure = RuntimeSnapshotReplayEntryResult.Failed(
                        prepared.Entry.LogicalEntryKey,
                        $"Replay dependency was not found: {missingDependency}.",
                        prepared.SourceVersion,
                        prepared.TargetVersion);
                    results[prepared.OriginalIndex] = failure;
                    session.RecordResult(failure);
                    continue;
                }

                valid.Add(prepared);
            }

            var ordered = OrderPreparedEntries(valid, results, session);
            for (var index = 0; index < ordered.Count; index++)
            {
                var prepared = ordered[index];
                if (results[prepared.OriginalIndex] is not null)
                {
                    continue;
                }

                if (TryGetFailedDependency(prepared, session, out var failedDependency))
                {
                    var dependencyFailure = RuntimeSnapshotReplayEntryResult.Failed(
                        prepared.Entry.LogicalEntryKey,
                        $"Replay dependency was not restored: {failedDependency}.",
                        prepared.SourceVersion,
                        prepared.TargetVersion);
                    results[prepared.OriginalIndex] = dependencyFailure;
                    session.RecordResult(dependencyFailure);
                    continue;
                }

                var context = new RuntimeSnapshotReplayContext(_backend, domains, prepared.Entry, session);
                try
                {
                    var replayResult = prepared.Replayer.Replay(context, prepared.Payload, prepared.Entry.RestoreMode)
                        .WithSchemaVersions(prepared.SourceVersion, prepared.TargetVersion);
                    results[prepared.OriginalIndex] = replayResult;
                    session.RecordResult(replayResult);
                }
                catch (Exception ex)
                {
                    var failure = RuntimeSnapshotReplayEntryResult.Failed(
                        prepared.Entry.LogicalEntryKey,
                        ex.Message,
                        prepared.SourceVersion,
                        prepared.TargetVersion);
                    results[prepared.OriginalIndex] = failure;
                    session.RecordResult(failure);
                }
            }

        }

        private static string? FindMissingDependency(
            PreparedReplayEntry prepared,
            IReadOnlyDictionary<string, PreparedReplayEntry> preparedByKey)
        {
            for (var index = 0; index < prepared.Dependencies.Count; index++)
            {
                var dependency = prepared.Dependencies[index];
                if (string.Equals(dependency, prepared.Entry.LogicalEntryKey, StringComparison.Ordinal)
                    || preparedByKey.ContainsKey(dependency))
                {
                    continue;
                }

                return dependency;
            }

            return null;
        }

        private static bool TryGetFailedDependency(
            PreparedReplayEntry prepared,
            RuntimeSnapshotReplaySession session,
            out string failedDependency)
        {
            for (var index = 0; index < prepared.Dependencies.Count; index++)
            {
                var dependency = prepared.Dependencies[index];
                if (!session.TryGetReplayResult(dependency, out var dependencyResult)
                    || dependencyResult.Status != SnapshotReplayStatus.Restored)
                {
                    failedDependency = dependency;
                    return true;
                }
            }

            failedDependency = string.Empty;
            return false;
        }

        private static List<PreparedReplayEntry> OrderPreparedEntries(
            IReadOnlyList<PreparedReplayEntry> preparedEntries,
            RuntimeSnapshotReplayEntryResult?[] results,
            RuntimeSnapshotReplaySession session)
        {
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var indegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var entriesByKey = new Dictionary<string, PreparedReplayEntry>(StringComparer.Ordinal);
            for (var index = 0; index < preparedEntries.Count; index++)
            {
                var prepared = preparedEntries[index];
                entriesByKey[prepared.Entry.LogicalEntryKey] = prepared;
                indegree[prepared.Entry.LogicalEntryKey] = 0;
            }

            for (var index = 0; index < preparedEntries.Count; index++)
            {
                var prepared = preparedEntries[index];
                for (var dependencyIndex = 0; dependencyIndex < prepared.Dependencies.Count; dependencyIndex++)
                {
                    var dependency = prepared.Dependencies[dependencyIndex];
                    if (!entriesByKey.ContainsKey(dependency))
                    {
                        continue;
                    }

                    if (!dependents.TryGetValue(dependency, out var outgoing))
                    {
                        outgoing = new List<string>();
                        dependents[dependency] = outgoing;
                    }

                    outgoing.Add(prepared.Entry.LogicalEntryKey);
                    indegree[prepared.Entry.LogicalEntryKey]++;
                }
            }

            var ready = new List<PreparedReplayEntry>();
            foreach (var pair in indegree)
            {
                if (pair.Value == 0)
                {
                    ready.Add(entriesByKey[pair.Key]);
                }
            }

            ready.Sort(PreparedReplayEntryComparer.Instance);

            var ordered = new List<PreparedReplayEntry>(preparedEntries.Count);
            while (ready.Count > 0)
            {
                var current = ready[0];
                ready.RemoveAt(0);
                ordered.Add(current);

                if (!dependents.TryGetValue(current.Entry.LogicalEntryKey, out var outgoing))
                {
                    continue;
                }

                for (var index = 0; index < outgoing.Count; index++)
                {
                    var dependentKey = outgoing[index];
                    indegree[dependentKey]--;
                    if (indegree[dependentKey] == 0)
                    {
                        ready.Add(entriesByKey[dependentKey]);
                    }
                }

                ready.Sort(PreparedReplayEntryComparer.Instance);
            }

            if (ordered.Count == preparedEntries.Count)
            {
                return ordered;
            }

            foreach (var prepared in preparedEntries)
            {
                if (ordered.Contains(prepared))
                {
                    continue;
                }

                var failure = RuntimeSnapshotReplayEntryResult.Failed(
                    prepared.Entry.LogicalEntryKey,
                    "Replay dependency cycle was detected.",
                    prepared.SourceVersion,
                    prepared.TargetVersion);
                results[prepared.OriginalIndex] = failure;
                session.RecordResult(failure);
            }

            return ordered;
        }

        private static IReadOnlyList<string> NormalizeDependencies(IReadOnlyList<string>? dependencies)
        {
            if (dependencies is null || dependencies.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(dependencies.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < dependencies.Count; index++)
            {
                var dependency = dependencies[index];
                if (string.IsNullOrWhiteSpace(dependency) || !seen.Add(dependency))
                {
                    continue;
                }

                result.Add(dependency);
            }

            return result;
        }

        private sealed class PreparedReplayEntry
        {
            public PreparedReplayEntry(
                int originalIndex,
                SnapshotEntry entry,
                RuntimeStateSnapshotPayload payload,
                int sourceVersion,
                int targetVersion,
                IRuntimeStateSnapshotReplayer replayer,
                int priority,
                IReadOnlyList<string> dependencies)
            {
                OriginalIndex = originalIndex;
                Entry = entry;
                Payload = payload;
                SourceVersion = sourceVersion;
                TargetVersion = targetVersion;
                Replayer = replayer;
                Priority = priority;
                Dependencies = dependencies;
            }

            public int OriginalIndex { get; }

            public SnapshotEntry Entry { get; }

            public RuntimeStateSnapshotPayload Payload { get; }

            public int SourceVersion { get; }

            public int TargetVersion { get; }

            public IRuntimeStateSnapshotReplayer Replayer { get; }

            public int Priority { get; }

            public IReadOnlyList<string> Dependencies { get; }
        }

        private sealed class PreparedReplayEntryComparer : IComparer<PreparedReplayEntry>
        {
            public static readonly PreparedReplayEntryComparer Instance = new();

            public int Compare(PreparedReplayEntry? x, PreparedReplayEntry? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x is null)
                {
                    return -1;
                }

                if (y is null)
                {
                    return 1;
                }

                var priorityComparison = x.Priority.CompareTo(y.Priority);
                if (priorityComparison != 0)
                {
                    return priorityComparison;
                }

                return x.OriginalIndex.CompareTo(y.OriginalIndex);
            }
        }
    }
}
