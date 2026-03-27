#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Settings
{
    /// <summary>
    /// 项目级怪物映射根设置。
    /// <para>
    /// 该 section 不再内联保存所有关卡的怪物映射明细，
    /// 而只声明“分片怪物映射资产”所在的根目录。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintMonsterMappingProjectSettings
    {
        public const string DefaultRootFolderPath = "Assets/SceneBlueprintSettings/MonsterMappings";

        [InspectorName("映射根目录 Root Folder")]
        public string RootFolderPath = DefaultRootFolderPath;

        public void Normalize()
        {
            RootFolderPath = string.IsNullOrWhiteSpace(RootFolderPath)
                ? DefaultRootFolderPath
                : RootFolderPath.Trim().Replace('\\', '/');
        }
    }

    /// <summary>
    /// Spawn / MonsterPool 的项目级 authoring 约束。
    /// <para>
    /// 这里保存团队共享的编辑侧上限与输入规则，
    /// 避免把具体阈值散落到各个 Inspector 或 generated annotation 特性里。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintSpawnAuthoringProjectSettings
    {
        public const int DefaultMaxMonsterPoolEntryStock = 100;
        public const float DefaultMonsterSenseRange = 10f;

        [InspectorName("MonsterPool 条目最大库存 Max MonsterPool Entry Stock")]
        public int MaxMonsterPoolEntryStock = DefaultMaxMonsterPoolEntryStock;

        [InspectorName("默认视觉范围 Default Vision Range")]
        public float DefaultVisionRange = DefaultMonsterSenseRange;

        [InspectorName("默认听觉范围 Default Hearing Range")]
        public float DefaultHearingRange = DefaultMonsterSenseRange;

        public void Normalize()
        {
            MaxMonsterPoolEntryStock = Mathf.Max(0, MaxMonsterPoolEntryStock);
            DefaultVisionRange = DefaultVisionRange > 0f ? DefaultVisionRange : DefaultMonsterSenseRange;
            DefaultHearingRange = DefaultHearingRange > 0f ? DefaultHearingRange : DefaultMonsterSenseRange;
        }
    }

    /// <summary>
    /// 单关卡怪物映射资产。
    /// <para>
    /// 每个资产只承载一个关卡的 MonsterType → MonsterId 映射，
    /// 从而把高频业务配置从单一项目总表中拆分出来，降低多人协作冲突。
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "LevelMonsterMapping",
        menuName = "SceneBlueprint/Level Monster Mapping Asset",
        order = 71)]
    public sealed class SceneBlueprintLevelMonsterMappingAsset : ScriptableObject
    {
        [InspectorName("关卡 ID Level ID")]
        [SerializeField] private int _levelId;

        [InspectorName("映射条目 Entries")]
        [SerializeField] private List<SceneBlueprintMonsterMappingEntry> _entries = new List<SceneBlueprintMonsterMappingEntry>();

        public int LevelId => _levelId;
        public IReadOnlyList<SceneBlueprintMonsterMappingEntry> Entries => _entries;

        public void ApplySnapshot(SceneBlueprintLevelMonsterMapping source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            _levelId = Mathf.Max(0, source.LevelId);
            _entries = new List<SceneBlueprintMonsterMappingEntry>(source.Entries?.Count ?? 0);
            if (source.Entries != null)
            {
                for (int i = 0; i < source.Entries.Count; i++)
                {
                    var entry = source.Entries[i] ?? new SceneBlueprintMonsterMappingEntry();
                    _entries.Add(new SceneBlueprintMonsterMappingEntry
                    {
                        MonsterType = Mathf.Max(1, entry.MonsterType),
                        MonsterId = Mathf.Max(0, entry.MonsterId),
                        ShortName = string.IsNullOrWhiteSpace(entry.ShortName) ? string.Empty : entry.ShortName.Trim(),
                        Description = string.IsNullOrWhiteSpace(entry.Description) ? string.Empty : entry.Description.Trim()
                    });
                }
            }

            OnValidate();
        }

        public SceneBlueprintLevelMonsterMapping CreateSnapshot()
        {
            var snapshot = new SceneBlueprintLevelMonsterMapping
            {
                LevelId = _levelId,
                Entries = new List<SceneBlueprintMonsterMappingEntry>(_entries.Count)
            };

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i] ?? new SceneBlueprintMonsterMappingEntry();
                snapshot.Entries.Add(new SceneBlueprintMonsterMappingEntry
                {
                    MonsterType = Mathf.Max(1, entry.MonsterType),
                    MonsterId = Mathf.Max(0, entry.MonsterId),
                    ShortName = string.IsNullOrWhiteSpace(entry.ShortName) ? string.Empty : entry.ShortName.Trim(),
                    Description = string.IsNullOrWhiteSpace(entry.Description) ? string.Empty : entry.Description.Trim()
                });
            }

            return snapshot;
        }

        private void OnValidate()
        {
            _levelId = Mathf.Max(0, _levelId);
            if (_entries == null)
            {
                _entries = new List<SceneBlueprintMonsterMappingEntry>();
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null)
                {
                    _entries[i] = new SceneBlueprintMonsterMappingEntry();
                    entry = _entries[i];
                }

                entry.MonsterType = Mathf.Max(1, entry.MonsterType);
                entry.MonsterId = Mathf.Max(0, entry.MonsterId);
                entry.ShortName = string.IsNullOrWhiteSpace(entry.ShortName) ? string.Empty : entry.ShortName.Trim();
                entry.Description = string.IsNullOrWhiteSpace(entry.Description) ? string.Empty : entry.Description.Trim();
            }

            _entries.Sort(static (left, right) =>
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return 1;
                if (right == null)
                    return -1;

                int typeCompare = left.MonsterType.CompareTo(right.MonsterType);
                return typeCompare != 0 ? typeCompare : left.MonsterId.CompareTo(right.MonsterId);
            });
        }
    }
}
