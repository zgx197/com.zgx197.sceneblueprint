#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Runtime.State
{
    public sealed class RuntimeSnapshotSchemaRegistry
    {
        private readonly Dictionary<string, SortedSet<int>> _registeredVersions = new(StringComparer.Ordinal);
        private readonly Dictionary<MigrationKey, Func<RuntimeStateSnapshotPayload, RuntimeStateSnapshotPayload>> _migrators = new();

        public void RegisterSchema(string schemaId, int version)
        {
            if (string.IsNullOrWhiteSpace(schemaId))
            {
                throw new ArgumentException("Schema id cannot be null or whitespace.", nameof(schemaId));
            }

            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version), version, "Schema version must be greater than zero.");
            }

            if (!_registeredVersions.TryGetValue(schemaId, out var versions))
            {
                versions = new SortedSet<int>();
                _registeredVersions.Add(schemaId, versions);
            }

            versions.Add(version);
        }

        public void RegisterMigrator(
            string schemaId,
            int fromVersion,
            int toVersion,
            Func<RuntimeStateSnapshotPayload, RuntimeStateSnapshotPayload> migrator)
        {
            if (string.IsNullOrWhiteSpace(schemaId))
            {
                throw new ArgumentException("Schema id cannot be null or whitespace.", nameof(schemaId));
            }

            if (fromVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromVersion), fromVersion, "Schema version must be greater than zero.");
            }

            if (toVersion <= fromVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(toVersion), toVersion, "Target version must be greater than source version.");
            }

            _migrators[new MigrationKey(schemaId, fromVersion, toVersion)] = migrator
                ?? throw new ArgumentNullException(nameof(migrator));

            RegisterSchema(schemaId, fromVersion);
            RegisterSchema(schemaId, toVersion);
        }

        public bool TryGetLatestVersion(string schemaId, out int latestVersion)
        {
            if (!string.IsNullOrWhiteSpace(schemaId)
                && _registeredVersions.TryGetValue(schemaId, out var versions)
                && versions.Count > 0)
            {
                latestVersion = versions.Max;
                return true;
            }

            latestVersion = 0;
            return false;
        }

        public bool TryMigrateToLatest(
            RuntimeStateSnapshotPayload payload,
            out RuntimeStateSnapshotPayload migratedPayload,
            out string? errorMessage)
        {
            if (payload is null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (!TryGetLatestVersion(payload.SchemaId, out var latestVersion)
                || latestVersion == payload.SchemaVersion)
            {
                migratedPayload = payload;
                errorMessage = null;
                return true;
            }

            return TryMigrate(payload, latestVersion, out migratedPayload, out errorMessage);
        }

        public bool TryMigrate(
            RuntimeStateSnapshotPayload payload,
            int targetVersion,
            out RuntimeStateSnapshotPayload migratedPayload,
            out string? errorMessage)
        {
            if (payload is null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (targetVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetVersion), targetVersion, "Target version must be greater than zero.");
            }

            if (targetVersion < payload.SchemaVersion)
            {
                migratedPayload = payload;
                errorMessage = $"Schema downgrade is not supported: {payload.SchemaId} {payload.SchemaVersion} -> {targetVersion}.";
                return false;
            }

            var currentPayload = payload;
            var currentVersion = payload.SchemaVersion;
            while (currentVersion < targetVersion)
            {
                if (!TryResolveNextStep(payload.SchemaId, currentVersion, targetVersion, out var nextVersion)
                    || !_migrators.TryGetValue(new MigrationKey(payload.SchemaId, currentVersion, nextVersion), out var migrator))
                {
                    migratedPayload = payload;
                    errorMessage = $"Schema migration path was not found: {payload.SchemaId} {currentVersion} -> {targetVersion}.";
                    return false;
                }

                currentPayload = migrator(currentPayload);
                if (currentPayload.SchemaVersion <= currentVersion)
                {
                    migratedPayload = payload;
                    errorMessage = $"Schema migrator did not advance version: {payload.SchemaId} {currentVersion} -> {currentPayload.SchemaVersion}.";
                    return false;
                }

                currentVersion = currentPayload.SchemaVersion;
            }

            migratedPayload = currentPayload;
            errorMessage = null;
            return true;
        }

        private bool TryResolveNextStep(string schemaId, int currentVersion, int targetVersion, out int nextVersion)
        {
            nextVersion = 0;
            foreach (var pair in _migrators)
            {
                if (!string.Equals(pair.Key.SchemaId, schemaId, StringComparison.Ordinal)
                    || pair.Key.FromVersion != currentVersion
                    || pair.Key.ToVersion > targetVersion)
                {
                    continue;
                }

                if (nextVersion == 0 || pair.Key.ToVersion < nextVersion)
                {
                    nextVersion = pair.Key.ToVersion;
                }
            }

            return nextVersion > currentVersion;
        }

        private readonly struct MigrationKey : IEquatable<MigrationKey>
        {
            public MigrationKey(string schemaId, int fromVersion, int toVersion)
            {
                SchemaId = schemaId;
                FromVersion = fromVersion;
                ToVersion = toVersion;
            }

            public string SchemaId { get; }

            public int FromVersion { get; }

            public int ToVersion { get; }

            public bool Equals(MigrationKey other)
            {
                return string.Equals(SchemaId, other.SchemaId, StringComparison.Ordinal)
                    && FromVersion == other.FromVersion
                    && ToVersion == other.ToVersion;
            }

            public override bool Equals(object? obj)
            {
                return obj is MigrationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(
                    StringComparer.Ordinal.GetHashCode(SchemaId ?? string.Empty),
                    FromVersion,
                    ToVersion);
            }
        }
    }
}
