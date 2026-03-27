#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Drawers
{
    /// <summary>
    /// 为 Spawn.Wave.fixedSpawns[].entryId 提供来自 MonsterPoolAnnotation 的条目候选。
    /// 这里返回的是“怪物池供给条目”，供 Spawn.Wave 的固定消费项去点名消费。
    /// 真正进入运行时主链的仍然是稳定 entryId，而不是某一波与条目的一一对应关系。
    /// </summary>
    public static class MonsterPoolEntryCandidateProvider
    {
        private const string MonsterPoolAnnotationTypeName = "SceneBlueprintUser.Annotations.MonsterPoolAnnotation";
        private const string UserAuthoringCandidateProviderTypeName = "SceneBlueprintUser.Editor.Drawers.MonsterPoolAuthoringCandidateProvider, SceneBlueprintUser.Editor";

        public static IReadOnlyList<MonsterPoolEntryCandidate> BuildCandidates(GameObject? areaObject)
        {
            if (areaObject == null)
            {
                return Array.Empty<MonsterPoolEntryCandidate>();
            }

            if (TryBuildCandidatesViaAuthoringProvider(areaObject, out var authoringCandidates))
            {
                return authoringCandidates;
            }

            var component = FindMonsterPoolAnnotationComponent(areaObject);
            if (component == null)
            {
                return Array.Empty<MonsterPoolEntryCandidate>();
            }

            var serializedObject = new SerializedObject(component);
            var entriesProperty = serializedObject.FindProperty("SupplyEntries");
            if (entriesProperty == null || !entriesProperty.isArray)
            {
                return Array.Empty<MonsterPoolEntryCandidate>();
            }

            var sourceEntries = new List<MonsterPoolEntryCandidateSource>(entriesProperty.arraySize);
            for (var index = 0; index < entriesProperty.arraySize; index++)
            {
                var item = entriesProperty.GetArrayElementAtIndex(index);
                if (item == null)
                {
                    continue;
                }

                sourceEntries.Add(new MonsterPoolEntryCandidateSource(
                    GetString(item, "EntryId"),
                    GetString(item, "DisplayName"),
                    GetInt(item, "MonsterType"),
                    GetDisplayText(item, "Tag"),
                    GetInt(item, "Count")));
            }

            return BuildCandidates(sourceEntries);
        }

        private static bool TryBuildCandidatesViaAuthoringProvider(
            GameObject areaObject,
            out IReadOnlyList<MonsterPoolEntryCandidate> candidates)
        {
            candidates = Array.Empty<MonsterPoolEntryCandidate>();

            try
            {
                var providerType = Type.GetType(UserAuthoringCandidateProviderTypeName, throwOnError: false);
                var method = providerType?.GetMethod(
                    "BuildCandidates",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(GameObject) },
                    modifiers: null);

                if (method == null)
                {
                    return false;
                }

                var result = method.Invoke(null, new object[] { areaObject });
                if (result is IReadOnlyList<MonsterPoolEntryCandidate> typedCandidates)
                {
                    candidates = typedCandidates;
                    return true;
                }

                if (result is MonsterPoolEntryCandidate[] array)
                {
                    candidates = array;
                    return true;
                }
            }
            catch
            {
                // User 层 helper 不可用时回退到旧的 SerializedProperty 读取链。
            }

            return false;
        }

        public static IReadOnlyList<MonsterPoolEntryCandidate> BuildCandidates(IEnumerable<MonsterPoolEntryCandidateSource>? entries)
        {
            if (entries == null)
            {
                return Array.Empty<MonsterPoolEntryCandidate>();
            }

            var unique = new Dictionary<string, MonsterPoolEntryCandidate>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var entryId = MonsterPoolEntryIdentityConventions.NormalizeEntryId(entry.EntryId);
                if (string.IsNullOrWhiteSpace(entryId) || unique.ContainsKey(entryId))
                {
                    continue;
                }

                unique.Add(entryId, new MonsterPoolEntryCandidate(
                    entryId,
                    entry.DisplayName,
                    entry.MonsterType,
                    entry.PoolTag,
                    entry.StockCount));
            }

            return unique.Values
                .OrderBy(static candidate => GetSupplySortRank(candidate.PoolTag))
                .ThenBy(static candidate => candidate.MonsterType)
                .ThenBy(static candidate => candidate.DisplayName, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.EntryId, StringComparer.Ordinal)
                .ToArray();
        }

        public static int FindCandidateIndex(string entryId, IReadOnlyList<MonsterPoolEntryCandidate>? candidates)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(entryId))
            {
                return -1;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                if (string.Equals(candidates[index].EntryId, entryId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        public static string ResolveEntryIdFromPopup(
            string currentValue,
            int popupIndex,
            IReadOnlyList<MonsterPoolEntryCandidate>? candidates)
        {
            if (popupIndex <= 0 || candidates == null)
            {
                return string.Empty;
            }

            var candidateIndex = popupIndex - 1;
            if (candidateIndex < 0 || candidateIndex >= candidates.Count)
            {
                return currentValue;
            }

            return candidates[candidateIndex].EntryId;
        }

        private static int GetSupplySortRank(string? poolTag)
        {
            return poolTag switch
            {
                "Boss" => 0,
                "Elite" => 1,
                "Normal" => 2,
                "Minion" => 3,
                "Special" => 4,
                _ => 5,
            };
        }

        private static Component? FindMonsterPoolAnnotationComponent(GameObject areaObject)
        {
            var components = areaObject.GetComponents<MonoBehaviour>();
            for (var index = 0; index < components.Length; index++)
            {
                var component = components[index];
                if (component == null)
                {
                    continue;
                }

                var fullName = component.GetType().FullName;
                if (string.Equals(fullName, MonsterPoolAnnotationTypeName, StringComparison.Ordinal))
                {
                    return component;
                }
            }

            return null;
        }

        private static string GetString(SerializedProperty item, string relativeName)
        {
            var property = item.FindPropertyRelative(relativeName);
            return property?.stringValue ?? string.Empty;
        }

        private static int GetInt(SerializedProperty item, string relativeName)
        {
            var property = item.FindPropertyRelative(relativeName);
            return property?.intValue ?? 0;
        }

        private static string GetDisplayText(SerializedProperty item, string relativeName)
        {
            var property = item.FindPropertyRelative(relativeName);
            if (property == null)
            {
                return string.Empty;
            }

            if (property.propertyType == SerializedPropertyType.Enum
                && property.enumValueIndex >= 0
                && property.enumValueIndex < property.enumDisplayNames.Length)
            {
                return property.enumDisplayNames[property.enumValueIndex];
            }

            return property.displayName;
        }
    }

    public readonly struct MonsterPoolEntryCandidateSource
    {
        public MonsterPoolEntryCandidateSource(
            string entryId,
            string displayName,
            int monsterType,
            string poolTag,
            int stockCount)
        {
            EntryId = entryId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            MonsterType = monsterType;
            PoolTag = poolTag ?? string.Empty;
            StockCount = stockCount;
        }

        public string EntryId { get; }

        public string DisplayName { get; }

        public int MonsterType { get; }

        public string PoolTag { get; }

        public int StockCount { get; }
    }

    public readonly struct MonsterPoolEntryCandidate
    {
        public MonsterPoolEntryCandidate(
            string entryId,
            string displayName,
            int monsterType,
            string poolTag,
            int stockCount)
        {
            EntryId = entryId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? EntryId : displayName.Trim();
            MonsterType = monsterType;
            PoolTag = poolTag ?? string.Empty;
            StockCount = stockCount;
        }

        public string EntryId { get; }

        public string DisplayName { get; }

        public int MonsterType { get; }

        public string PoolTag { get; }

        public int StockCount { get; }

        public string SummaryText => $"{DisplayName} / 槽位{MonsterType} / {PoolTag} / 库存{StockCount}";

        public string DisplayLabel => $"{DisplayName} ({EntryId}) / 槽位{MonsterType} / {PoolTag} / 库存{StockCount}";
    }

    public static class MonsterPoolEntryIdentityConventions
    {
        private const string UserMonsterPoolAuthoringUtilityTypeName = "SceneBlueprintUser.Compilation.MonsterPoolAuthoringUtility, SceneBlueprintUser.Compilation";
        private const string GeneratedEntryIdPrefix = "supply_";

        public static string NormalizeEntryId(string? entryId)
        {
            if (TryInvokeUserStringMethod(
                    "NormalizeEntryId",
                    new object?[] { entryId },
                    out var normalizedEntryId))
            {
                return normalizedEntryId;
            }

            return string.IsNullOrWhiteSpace(entryId) ? string.Empty : entryId.Trim();
        }

        public static string BuildSuggestedEntryId(
            string? displayName,
            int monsterType,
            string? poolTag,
            int siblingIndex)
        {
            if (TryInvokeUserStringMethod(
                    "BuildSuggestedEntryId",
                    new object?[] { displayName, monsterType, poolTag, siblingIndex },
                    out var suggestedEntryId))
            {
                return suggestedEntryId;
            }

            return GeneratedEntryIdPrefix + Guid.NewGuid().ToString("N");
        }

        private static bool TryInvokeUserStringMethod(string methodName, object?[] args, out string result)
        {
            result = string.Empty;

            try
            {
                var utilityType = Type.GetType(UserMonsterPoolAuthoringUtilityTypeName, throwOnError: false);
                var method = utilityType?.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null,
                    types: GetArgumentTypes(args),
                    modifiers: null);

                if (method == null)
                {
                    return false;
                }

                if (method.Invoke(null, args) is string stringResult)
                {
                    result = stringResult;
                    return true;
                }
            }
            catch
            {
                // User 层 contract 不可用时，保持 package 层兼容回退。
            }

            return false;
        }

        private static Type[] GetArgumentTypes(object?[] args)
        {
            var types = new Type[args.Length];
            for (var index = 0; index < args.Length; index++)
            {
                types[index] = args[index]?.GetType() ?? typeof(string);
            }

            return types;
        }
    }
}
