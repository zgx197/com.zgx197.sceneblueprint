#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using SceneBlueprint.Runtime.Interpreter;
using UnityEngine;

namespace SceneBlueprint.Runtime.Testing
{
    public enum BlueprintRuntimeSmokeEntryKind
    {
        Unknown = 0,
        RuntimeTestWindowLoadAndRun = 1,
        RuntimeTestWindowReload = 2,
        DemoHostReload = 3,
    }

    public sealed class BlueprintRuntimeSmokeDefinition
    {
        public BlueprintRuntimeSmokeDefinition(
            string id,
            string title,
            string summary,
            BlueprintRegistryContractScope contractScope,
            BlueprintRuntimeSmokeEntryKind entryKind,
            IReadOnlyList<string>? targetBlueprintNames = null,
            string? successLogMarker = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Smoke id cannot be empty.", nameof(id)) : id.Trim();
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            ContractScope = contractScope;
            EntryKind = entryKind;
            TargetBlueprintNames = targetBlueprintNames ?? Array.Empty<string>();
            SuccessLogMarker = successLogMarker ?? string.Empty;
        }

        public string Id { get; }

        public string Title { get; }

        public string Summary { get; }

        public BlueprintRegistryContractScope ContractScope { get; }

        public BlueprintRuntimeSmokeEntryKind EntryKind { get; }

        public string DisplayScopeTitle => BlueprintRegistryPresentationUtility.GetScopeTitle(ContractScope);

        public string DisplayEntryTitle => BlueprintRegistryPresentationUtility.GetRuntimeSmokeEntryTitle(EntryKind);

        public IReadOnlyList<string> TargetBlueprintNames { get; }

        public string SuccessLogMarker { get; }

        public bool Matches(string? assetName, string? loadedBlueprintName)
        {
            if (TargetBlueprintNames.Count == 0)
            {
                return true;
            }

            for (var index = 0; index < TargetBlueprintNames.Count; index++)
            {
                var candidate = TargetBlueprintNames[index];
                if (string.Equals(candidate, assetName, StringComparison.Ordinal)
                    || string.Equals(candidate, loadedBlueprintName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public interface IBlueprintRuntimeSmokeProbe
    {
        BlueprintRuntimeSmokeDefinition Definition { get; }

        void TryRun(BlueprintRunner runner, TextAsset blueprintAsset);
    }

    public static class BlueprintRuntimeSmokeRegistry
    {
        private static IReadOnlyList<IBlueprintRuntimeSmokeProbe>? s_cachedProbes;

        public static IReadOnlyList<IBlueprintRuntimeSmokeProbe> All
        {
            get
            {
                s_cachedProbes ??= Discover();
                return s_cachedProbes;
            }
        }

        public static IReadOnlyList<BlueprintRuntimeSmokeDefinition> Definitions
        {
            get
            {
                var probes = All;
                var definitions = new BlueprintRuntimeSmokeDefinition[probes.Count];
                for (var index = 0; index < probes.Count; index++)
                {
                    definitions[index] = probes[index].Definition;
                }

                return definitions;
            }
        }

        public static BlueprintRuntimeSmokeDefinition Find(string id)
        {
            var definitions = Definitions;
            for (var index = 0; index < definitions.Count; index++)
            {
                if (string.Equals(definitions[index].Id, id, StringComparison.Ordinal))
                {
                    return definitions[index];
                }
            }

            throw new KeyNotFoundException($"Runtime smoke definition was not found: {id}");
        }

        public static IReadOnlyList<BlueprintRuntimeSmokeDefinition> EnumerateByScope(BlueprintRegistryContractScope scope)
        {
            if (scope == BlueprintRegistryContractScope.Unknown)
            {
                return Array.Empty<BlueprintRuntimeSmokeDefinition>();
            }

            var definitions = Definitions;
            var matches = new List<BlueprintRuntimeSmokeDefinition>();
            for (var index = 0; index < definitions.Count; index++)
            {
                if (definitions[index].ContractScope == scope)
                {
                    matches.Add(definitions[index]);
                }
            }

            return matches;
        }

        public static string DescribeScopeCatalog(BlueprintRegistryContractScope scope)
        {
            return BlueprintRegistryPresentationUtility.BuildSmokeCatalogSummary(EnumerateByScope(scope));
        }

        public static int TryRunMatching(BlueprintRunner? runner, TextAsset? blueprintAsset)
        {
            if (runner == null || blueprintAsset == null)
            {
                return 0;
            }

            var assetName = blueprintAsset.name;
            var loadedBlueprintName = runner.Frame?.BlueprintName ?? string.Empty;
            var matchCount = 0;

            var probes = All;
            for (var index = 0; index < probes.Count; index++)
            {
                var probe = probes[index];
                if (!probe.Definition.Matches(assetName, loadedBlueprintName))
                {
                    continue;
                }

                probe.TryRun(runner, blueprintAsset);
                matchCount++;
            }

            return matchCount;
        }

        public static bool HasMatchingProbe(string? assetName, string? loadedBlueprintName)
        {
            var definitions = Definitions;
            for (var index = 0; index < definitions.Count; index++)
            {
                if (definitions[index].Matches(assetName, loadedBlueprintName))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<IBlueprintRuntimeSmokeProbe> Discover()
        {
            var probes = new List<IBlueprintRuntimeSmokeProbe>();
            var probeType = typeof(IBlueprintRuntimeSmokeProbe);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types ?? Type.EmptyTypes;
                }

                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];
                    if (type == null
                        || type.IsAbstract
                        || !probeType.IsAssignableFrom(type))
                    {
                        continue;
                    }

                    try
                    {
                        if (Activator.CreateInstance(type) is not IBlueprintRuntimeSmokeProbe probe
                            || string.IsNullOrWhiteSpace(probe.Definition.Id))
                        {
                            continue;
                        }

                        probes.Add(probe);
                    }
                    catch
                    {
                        // Keep smoke discovery best-effort so missing optional assemblies do not break the editor.
                    }
                }
            }

            probes.Sort(static (left, right) =>
                string.Compare(left.Definition.Id, right.Definition.Id, StringComparison.Ordinal));
            return probes;
        }
    }
}
