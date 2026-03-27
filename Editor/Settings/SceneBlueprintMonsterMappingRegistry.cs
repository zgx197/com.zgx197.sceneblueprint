#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using SceneBlueprint.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Settings
{
    /// <summary>
    /// 分片怪物映射注册表。
    /// <para>
    /// 统一管理“每关卡一个 Monster Mapping 资产”的发现、聚合与校验，
    /// 对外暴露稳定的查询快照，避免调用方直接耦合 AssetDatabase 扫描逻辑。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class SceneBlueprintMonsterMappingRegistry
    {
        private static bool _dirty = true;
        private static List<SceneBlueprintLevelMonsterMappingAsset> _assets = new();
        private static List<string> _issues = new();
        private static SceneBlueprintMonsterMappingData _snapshot = new();

        static SceneBlueprintMonsterMappingRegistry()
        {
            EditorApplication.projectChanged += Invalidate;
        }

        public static void Invalidate()
        {
            _dirty = true;
        }

        public static IReadOnlyList<SceneBlueprintLevelMonsterMappingAsset> GetAllAssets()
        {
            EnsureLoaded();
            return _assets;
        }

        public static IReadOnlyList<string> GetIssues()
        {
            EnsureLoaded();
            return _issues;
        }

        public static SceneBlueprintMonsterMappingData GetSnapshot()
        {
            EnsureLoaded();
            return _snapshot;
        }

        public static string GetRootFolderPath()
        {
            var settings = SceneBlueprintSettingsService.Project.MonsterMapping;
            settings.Normalize();
            return settings.RootFolderPath;
        }

        public static bool EnsureRootFolder()
        {
            string rootFolderPath = GetRootFolderPath();
            if (AssetDatabase.IsValidFolder(rootFolderPath))
                return true;

            string[] segments = rootFolderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
                return false;

            string current = "Assets";
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }

            return AssetDatabase.IsValidFolder(rootFolderPath);
        }

        public static bool TryCreateLevelAsset(
            int levelId,
            out SceneBlueprintLevelMonsterMappingAsset? asset,
            out string assetPath,
            out string error)
        {
            asset = null;
            assetPath = string.Empty;
            error = string.Empty;

            if (!EnsureRootFolder())
            {
                error = $"怪物映射根目录非法或创建失败: {GetRootFolderPath()}";
                return false;
            }

            string rootFolderPath = GetRootFolderPath();
            string fileName = $"Level_{Mathf.Max(0, levelId)}_MonsterMapping.asset";
            assetPath = AssetDatabase.GenerateUniqueAssetPath($"{rootFolderPath}/{fileName}");

            asset = ScriptableObject.CreateInstance<SceneBlueprintLevelMonsterMappingAsset>();
            asset.ApplySnapshot(new SceneBlueprintLevelMonsterMapping
            {
                LevelId = Mathf.Max(0, levelId),
                Entries = new List<SceneBlueprintMonsterMappingEntry>()
            });

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Invalidate();
            return true;
        }

        private static void EnsureLoaded()
        {
            if (!_dirty)
                return;

            _dirty = false;
            _assets = LoadAssets();
            _issues = new List<string>();
            _snapshot = BuildSnapshot(_assets, _issues);
        }

        private static List<SceneBlueprintLevelMonsterMappingAsset> LoadAssets()
        {
            string rootFolderPath = GetRootFolderPath();
            if (!AssetDatabase.IsValidFolder(rootFolderPath))
                return new List<SceneBlueprintLevelMonsterMappingAsset>();

            string[] guids = AssetDatabase.FindAssets("t:SceneBlueprintLevelMonsterMappingAsset", new[] { rootFolderPath });
            var assets = new List<SceneBlueprintLevelMonsterMappingAsset>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<SceneBlueprintLevelMonsterMappingAsset>(path);
                if (asset != null)
                    assets.Add(asset);
            }

            assets.Sort(static (left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(
                    AssetDatabase.GetAssetPath(left),
                    AssetDatabase.GetAssetPath(right)));
            return assets;
        }

        private static SceneBlueprintMonsterMappingData BuildSnapshot(
            IReadOnlyList<SceneBlueprintLevelMonsterMappingAsset> assets,
            List<string> issues)
        {
            var result = new SceneBlueprintMonsterMappingData();
            var selectedByLevel = new Dictionary<int, SceneBlueprintLevelMonsterMapping>(assets.Count);
            var selectedAssetPathByLevel = new Dictionary<int, string>(assets.Count);

            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                string path = AssetDatabase.GetAssetPath(asset);
                var snapshot = asset.CreateSnapshot();

                if (selectedByLevel.ContainsKey(snapshot.LevelId))
                {
                    issues.Add($"发现重复的关卡怪物映射资产：Level {snapshot.LevelId}。当前采用 {selectedAssetPathByLevel[snapshot.LevelId]}，忽略 {path}。");
                    continue;
                }

                selectedByLevel[snapshot.LevelId] = snapshot;
                selectedAssetPathByLevel[snapshot.LevelId] = path;
                AppendDuplicateMonsterTypeIssues(snapshot, path, issues);
            }

            foreach (var pair in selectedByLevel.OrderBy(static pair => pair.Key))
                result.Levels.Add(CloneLevel(pair.Value));

            result.Levels.Sort(static (left, right) => left.LevelId.CompareTo(right.LevelId));
            return result;
        }

        private static void AppendDuplicateMonsterTypeIssues(
            SceneBlueprintLevelMonsterMapping level,
            string assetPath,
            List<string> issues)
        {
            var seenTypes = new HashSet<int>();
            for (int i = 0; i < level.Entries.Count; i++)
            {
                var entry = level.Entries[i];
                if (!seenTypes.Add(entry.MonsterType))
                    issues.Add($"关卡映射资产 {assetPath} 存在重复 MonsterType: {entry.MonsterType}。查询时将采用排序后的第一条记录。");
            }
        }

        private static SceneBlueprintLevelMonsterMapping CloneLevel(SceneBlueprintLevelMonsterMapping source)
        {
            var clone = new SceneBlueprintLevelMonsterMapping
            {
                LevelId = source.LevelId,
                Entries = new List<SceneBlueprintMonsterMappingEntry>(source.Entries.Count)
            };

            for (int i = 0; i < source.Entries.Count; i++)
            {
                var entry = source.Entries[i] ?? new SceneBlueprintMonsterMappingEntry();
                clone.Entries.Add(new SceneBlueprintMonsterMappingEntry
                {
                    MonsterType = Mathf.Max(1, entry.MonsterType),
                    MonsterId = Mathf.Max(0, entry.MonsterId),
                    ShortName = string.IsNullOrWhiteSpace(entry.ShortName) ? string.Empty : entry.ShortName.Trim(),
                    Description = string.IsNullOrWhiteSpace(entry.Description) ? string.Empty : entry.Description.Trim()
                });
            }

            return clone;
        }
    }
}
