#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Editor.Compilation
{
    internal enum ActionCompilationTransportMetadataKind
    {
        Unknown = 0,
        CompiledPayload = 1,
        Auxiliary = 2,
    }

    internal sealed class ActionCompilationTransportMetadataEntry
    {
        public ActionCompilationTransportMetadataEntry(
            string key,
            string valueType,
            ActionCompilationTransportMetadataKind kind,
            string family)
        {
            Key = key ?? string.Empty;
            ValueType = valueType ?? string.Empty;
            Kind = kind;
            Family = family ?? string.Empty;
        }

        public string Key { get; }

        public string ValueType { get; }

        public ActionCompilationTransportMetadataKind Kind { get; }

        public string Family { get; }
    }

    internal sealed class ActionCompilationTransportMetadataView
    {
        public ActionCompilationTransportMetadataView(
            IReadOnlyList<ActionCompilationTransportMetadataEntry>? entries,
            int compiledPayloadCount,
            int auxiliaryEntryCount,
            IReadOnlyList<string>? families)
        {
            Entries = entries ?? Array.Empty<ActionCompilationTransportMetadataEntry>();
            CompiledPayloadCount = Math.Max(0, compiledPayloadCount);
            AuxiliaryEntryCount = Math.Max(0, auxiliaryEntryCount);
            Families = families ?? Array.Empty<string>();
        }

        public IReadOnlyList<ActionCompilationTransportMetadataEntry> Entries { get; }

        public int CompiledPayloadCount { get; }

        public int AuxiliaryEntryCount { get; }

        public IReadOnlyList<string> Families { get; }

        public int TotalCount => Entries.Count;

        public string Summary
        {
            get
            {
                if (TotalCount <= 0)
                {
                    return "无运输元数据";
                }

                var familySummary = Families.Count == 0
                    ? string.Empty
                    : string.Join(", ", Families);
                if (CompiledPayloadCount > 0 && AuxiliaryEntryCount > 0)
                {
                    return $"compiled {CompiledPayloadCount} 条 | auxiliary {AuxiliaryEntryCount} 条 | {familySummary}";
                }

                if (CompiledPayloadCount > 0)
                {
                    return $"compiled {CompiledPayloadCount} 条 | {familySummary}";
                }

                return $"auxiliary {AuxiliaryEntryCount} 条";
            }
        }
    }

    internal static class ActionCompilationTransportMetadataUtility
    {
        public static ActionCompilationTransportMetadataView CreateView(PropertyValue[]? metadataEntries)
        {
            if (metadataEntries == null || metadataEntries.Length == 0)
            {
                return new ActionCompilationTransportMetadataView(
                    Array.Empty<ActionCompilationTransportMetadataEntry>(),
                    0,
                    0,
                    Array.Empty<string>());
            }

            var entries = new List<ActionCompilationTransportMetadataEntry>(metadataEntries.Length);
            var families = new HashSet<string>(StringComparer.Ordinal);
            var compiledPayloadCount = 0;
            var auxiliaryEntryCount = 0;

            for (var index = 0; index < metadataEntries.Length; index++)
            {
                var property = metadataEntries[index];
                var key = property?.Key?.Trim() ?? string.Empty;
                var valueType = property?.ValueType?.Trim() ?? string.Empty;
                var family = ResolveFamily(key);
                var kind = ResolveKind(key);
                entries.Add(new ActionCompilationTransportMetadataEntry(key, valueType, kind, family));
                if (!string.IsNullOrWhiteSpace(family))
                {
                    families.Add(family);
                }

                if (kind == ActionCompilationTransportMetadataKind.CompiledPayload)
                {
                    compiledPayloadCount++;
                }
                else
                {
                    auxiliaryEntryCount++;
                }
            }

            var familyList = new List<string>(families);
            familyList.Sort(StringComparer.Ordinal);
            return new ActionCompilationTransportMetadataView(
                entries,
                compiledPayloadCount,
                auxiliaryEntryCount,
                familyList);
        }

        private static ActionCompilationTransportMetadataKind ResolveKind(string key)
        {
            return key.StartsWith("compiled.", StringComparison.Ordinal)
                ? ActionCompilationTransportMetadataKind.CompiledPayload
                : ActionCompilationTransportMetadataKind.Auxiliary;
        }

        private static string ResolveFamily(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (key.StartsWith("compiled.signal.", StringComparison.Ordinal))
            {
                return "signal";
            }

            if (key.StartsWith("compiled.flow.", StringComparison.Ordinal))
            {
                return "flow";
            }

            if (key.StartsWith("compiled.blackboard.", StringComparison.Ordinal))
            {
                return "blackboard";
            }

            if (key.StartsWith("compiled.entity-ref-action.", StringComparison.Ordinal))
            {
                return "entity-ref";
            }

            if (key.StartsWith("compiled.spawnPreset.", StringComparison.Ordinal))
            {
                return "spawn-preset";
            }

            if (key.StartsWith("compiled.spawnWave.", StringComparison.Ordinal))
            {
                return "spawn-wave";
            }

            return ResolveKind(key) == ActionCompilationTransportMetadataKind.CompiledPayload
                ? "compiled"
                : "auxiliary";
        }
    }
}
