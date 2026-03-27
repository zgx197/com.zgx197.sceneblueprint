#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Editor.Settings
{
    /// <summary>
    /// 怪物映射兼容门面。
    /// <para>
    /// 真正的数据真源是“每关卡一个 SceneBlueprintLevelMonsterMappingAsset”。
    /// 本类只负责向旧调用点提供稳定查询接口，不承载任何可编辑数据。
    /// </para>
    /// </summary>
    public class MonsterMappingConfig : ScriptableObject
    {
        // ══════════════════════════════════════
        //  查询 API
        // ══════════════════════════════════════

        /// <summary>
        /// 获取指定关卡的关卡怪物槽位 `monsterType` → 真实怪物 `monsterId` 映射字典副本。
        /// 若关卡未配置，返回空字典。
        /// </summary>
        public Dictionary<int, int> GetMappingForLevel(int levelId)
        {
            var result = new Dictionary<int, int>();
            var levels = SceneBlueprintMonsterMappingRegistry.GetSnapshot().Levels;
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (level.LevelId != levelId)
                {
                    continue;
                }

                for (int j = 0; j < level.Entries.Count; j++)
                {
                    var entry = level.Entries[j];
                    if (!result.ContainsKey(entry.MonsterType))
                    {
                        result[entry.MonsterType] = entry.MonsterId;
                    }
                }

                break;
            }

            return result;
        }

        /// <summary>
        /// 尝试根据关卡与关卡怪物槽位获取真实怪物 ID。
        /// </summary>
        public bool TryGetMonsterId(int levelId, int monsterType, out int monsterId)
        {
            return SceneBlueprintMonsterMappingRegistry.GetSnapshot().TryGetMonsterId(levelId, monsterType, out monsterId);
        }

        /// <summary>
        /// 获取指定关卡中某个 MonsterType 槽位的简称。
        /// </summary>
        public string GetShortName(int levelId, int monsterType)
        {
            var levels = SceneBlueprintMonsterMappingRegistry.GetSnapshot().Levels;
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (level.LevelId != levelId)
                {
                    continue;
                }

                for (int j = 0; j < level.Entries.Count; j++)
                {
                    var entry = level.Entries[j];
                    if (entry.MonsterType == monsterType)
                    {
                        return entry.ShortName;
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// 获取指定关卡中某个 MonsterType 的描述。
        /// </summary>
        public string GetDescription(int levelId, int monsterType)
        {
            var levels = SceneBlueprintMonsterMappingRegistry.GetSnapshot().Levels;
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (level.LevelId != levelId)
                {
                    continue;
                }

                for (int j = 0; j < level.Entries.Count; j++)
                {
                    var entry = level.Entries[j];
                    if (entry.MonsterType == monsterType)
                    {
                        return entry.Description;
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// 获取指定关卡中某个 MonsterType 的显示标签（简称 + 描述）。
        /// 例如："小兵 - 近战小兵" 或 "小兵"(无描述时) 或 ""(未配置时)。
        /// </summary>
        public string GetDisplayLabel(int levelId, int monsterType)
        {
            return SceneBlueprintMonsterMappingRegistry.GetSnapshot().GetDisplayLabel(levelId, monsterType);
        }

        /// <summary>
        /// 遍历所有关卡查找指定 MonsterType 的显示标签（用于编辑器中不知道关卡 ID 的场景）。
        /// 优先返回第一个匹配的结果。
        /// </summary>
        public string GetDisplayLabelAnyLevel(int monsterType)
        {
            var levels = SceneBlueprintMonsterMappingRegistry.GetSnapshot().Levels;
            for (int i = 0; i < levels.Count; i++)
            {
                string label = SceneBlueprintMonsterMappingRegistry.GetSnapshot().GetDisplayLabel(levels[i].LevelId, monsterType);
                if (!string.IsNullOrEmpty(label))
                {
                    return label;
                }
            }
            return "";
        }

        /// <summary>
        /// 获取指定关卡 MonsterType 的有效范围。
        /// </summary>
        public bool TryGetMonsterTypeRange(int levelId, out int minMonsterType, out int maxMonsterType)
        {
            return SceneBlueprintMonsterMappingRegistry.GetSnapshot().TryGetMonsterTypeRange(
                levelId,
                out minMonsterType,
                out maxMonsterType);
        }

        // ══════════════════════════════════════
        //  单例加载（Editor 侧便捷访问）
        // ══════════════════════════════════════

        private static MonsterMappingConfig? _cached;

        /// <summary>
        /// 获取临时兼容门面实例。
        /// 结果被缓存，Domain Reload 后自动重新创建。
        /// </summary>
        public static MonsterMappingConfig? FindInProject()
        {
            if (_cached != null) return _cached;

            _cached = CreateInstance<MonsterMappingConfig>();
            _cached.hideFlags = HideFlags.HideAndDontSave;
            return _cached;
        }

        /// <summary>清除缓存（测试用）。</summary>
        public static void ClearCache() => _cached = null;
    }
}
