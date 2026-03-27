#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using UnityEngine;

namespace SceneBlueprint.Runtime.Settings
{
    /// <summary>
    /// 项目级运行时配置 section。
    /// <para>
    /// 该数据结构用于承载未来统一配置中心中的“项目运行时配置”，
    /// 目标是替代散落的独立运行时配置资产，让 TickRate、时间舍入策略、测试与调试项
    /// 都归并到同一个项目配置容器中。
    /// </para>
    /// <para>
    /// Phase 1 中它主要作为正式数据模型和编辑入口存在，
    /// 现有运行时消费链尚未切换到这里。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintRuntimeSettingsData
    {
        [InspectorName("目标 Tick 频率 Target Tick Rate")]
        public int TargetTickRate = 10;

        [InspectorName("每帧 Tick 数 Ticks Per Frame")]
        public int TicksPerFrame = 0;

        [InspectorName("时间舍入模式 Time Rounding Mode")]
        public BlueprintTimeRoundingMode TimeRoundingMode = BlueprintTimeRoundingMode.Ceil;

        [InspectorName("测试场景自动运行 Auto Run In Test Scene")]
        public bool AutoRunInTestScene = true;

        [InspectorName("最大 Tick 限制 Max Ticks Limit")]
        public int MaxTicksLimit = 1000;

        [InspectorName("批量 Tick 数 Batch Tick Count")]
        public int BatchTickCount = 10;

        [InspectorName("启用详细日志 Enable Detailed Logs")]
        public bool EnableDetailedLogs = true;

        [InspectorName("输出 Tick 边界日志 Enable Tick Boundary Logs")]
        public bool EnableTickBoundaryLogs;

        [InspectorName("输出转场详情日志 Enable Transition Detail Logs")]
        public bool EnableTransitionDetailLogs = true;

        [InspectorName("输出 Trigger 等待诊断 Enable Trigger Wait Diagnostics")]
        public bool EnableTriggerWaitDiagnostics = true;

        [InspectorName("输出运行结束摘要 Enable Completion Summary Logs")]
        public bool EnableCompletionSummaryLogs = true;

        [InspectorName("记录系统执行 Log System Execution")]
        public bool LogSystemExecution;

        [InspectorName("显示性能统计 Show Performance Stats")]
        public bool ShowPerformanceStats;

        /// <summary>
        /// 将项目配置中的时间字段组装为统一的时间配置快照。
        /// </summary>
        public BlueprintTimeSettings ToTimeSettings()
        {
            return new BlueprintTimeSettings(TargetTickRate, TimeRoundingMode);
        }

        /// <summary>
        /// 规范化数值范围，避免资产中出现非法值。
        /// </summary>
        public void Normalize()
        {
            TargetTickRate = Mathf.Max(1, TargetTickRate);
            TicksPerFrame = Mathf.Max(0, TicksPerFrame);
            MaxTicksLimit = Mathf.Max(100, MaxTicksLimit);
            BatchTickCount = Mathf.Max(1, BatchTickCount);
        }
    }

    /// <summary>
    /// 怪物映射中的单条配置记录。
    /// <para>
    /// 表示一个 `MonsterType -> MonsterId` 的映射关系，以及编辑器展示所需的简称与描述。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintMonsterMappingEntry
    {
        [InspectorName("关卡怪物槽位 MonsterType")]
        public int MonsterType;

        [InspectorName("怪物 ID Monster ID")]
        public int MonsterId;

        [InspectorName("简称 Short Name")]
        public string ShortName = "";

        [InspectorName("描述 Description")]
        public string Description = "";
    }

    /// <summary>
    /// 单个关卡的怪物映射集合。
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintLevelMonsterMapping
    {
        [InspectorName("关卡 ID Level ID")]
        public int LevelId;

        [InspectorName("映射条目 Entries")]
        public List<SceneBlueprintMonsterMappingEntry> Entries = new List<SceneBlueprintMonsterMappingEntry>();
    }

    /// <summary>
    /// 怪物映射快照数据。
    /// <para>
    /// 该类型保留为稳定的运行时/编辑器查询快照模型，
    /// 由分关卡怪物映射资产聚合而成。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintMonsterMappingData
    {
        [InspectorName("关卡列表 Levels")]
        public List<SceneBlueprintLevelMonsterMapping> Levels = new List<SceneBlueprintLevelMonsterMapping>();

        /// <summary>
        /// 根据关卡 ID 与关卡怪物槽位查找真实怪物 ID。
        /// </summary>
        public bool TryGetMonsterId(int levelId, int monsterType, out int monsterId)
        {
            monsterId = 0;
            var level = FindLevel(levelId);
            if (level == null)
            {
                return false;
            }

            for (int i = 0; i < level.Entries.Count; i++)
            {
                var entry = level.Entries[i];
                if (entry.MonsterType != monsterType)
                {
                    continue;
                }

                monsterId = entry.MonsterId;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取编辑器显示标签。
        /// <para>
        /// 组合规则为：简称 + 描述，其次简称，其次描述。
        /// </para>
        /// </summary>
        public string GetDisplayLabel(int levelId, int monsterType)
        {
            var level = FindLevel(levelId);
            if (level == null)
            {
                return "";
            }

            for (int i = 0; i < level.Entries.Count; i++)
            {
                var entry = level.Entries[i];
                if (entry.MonsterType != monsterType)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.ShortName) && !string.IsNullOrEmpty(entry.Description))
                {
                    return entry.ShortName + " - " + entry.Description;
                }

                if (!string.IsNullOrEmpty(entry.ShortName))
                {
                    return entry.ShortName;
                }

                if (!string.IsNullOrEmpty(entry.Description))
                {
                    return entry.Description;
                }

                return "";
            }

            return "";
        }

        /// <summary>
        /// 获取指定关卡中已配置 MonsterType 的最小值与最大值。
        /// </summary>
        public bool TryGetMonsterTypeRange(int levelId, out int minMonsterType, out int maxMonsterType)
        {
            minMonsterType = 0;
            maxMonsterType = 0;

            var level = FindLevel(levelId);
            if (level == null || level.Entries == null || level.Entries.Count == 0)
            {
                return false;
            }

            var hasValue = false;
            for (int i = 0; i < level.Entries.Count; i++)
            {
                var entry = level.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                var monsterType = Mathf.Max(1, entry.MonsterType);
                if (!hasValue)
                {
                    minMonsterType = monsterType;
                    maxMonsterType = monsterType;
                    hasValue = true;
                    continue;
                }

                if (monsterType < minMonsterType)
                {
                    minMonsterType = monsterType;
                }

                if (monsterType > maxMonsterType)
                {
                    maxMonsterType = monsterType;
                }
            }

            return hasValue;
        }

        /// <summary>
        /// 根据关卡 ID 查找对应的关卡映射数据。
        /// </summary>
        private SceneBlueprintLevelMonsterMapping? FindLevel(int levelId)
        {
            for (int i = 0; i < Levels.Count; i++)
            {
                var level = Levels[i];
                if (level.LevelId == levelId)
                {
                    return level;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 项目对框架默认能力的覆盖配置。
    /// <para>
    /// 这里保存“这个项目最终选择了什么”，而不是框架默认值本身。
    /// 例如 `SpatialModeId` 就属于项目对框架能力的选择结果。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintFrameworkOverrideSettings
    {
        [InspectorName("空间模式 Spatial Mode")]
        public string SpatialModeId = "Unity3D";

        /// <summary>
        /// 规范化字符串配置，避免空白值污染项目资产。
        /// </summary>
        public void Normalize()
        {
            SpatialModeId = string.IsNullOrWhiteSpace(SpatialModeId)
                ? "Unity3D"
                : SpatialModeId.Trim();
        }
    }

    /// <summary>
    /// 项目级业务集成配置。
    /// <para>
    /// 当前先保留最小骨架，后续用于导出流程、业务桥接参数等项目级扩展配置。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintIntegrationSettings
    {
        [InspectorName("导出配置 ID Export Profile ID")]
        public string ExportProfileId = "";

        [InspectorName("备注 Notes")]
        public string Notes = "";
    }

    /// <summary>
    /// SceneBlueprint 项目配置资产。
    /// <para>
    /// 这是统一配置中心中的“项目配置”根容器，固定落在
    /// `Assets/SceneBlueprintSettings/SceneBlueprintProjectConfig.asset`。
    /// </para>
    /// <para>
    /// 设计原则是：项目层保留统一的总入口和基线配置，
    /// 但高频业务目录型数据允许拆分为独立共享资产，以降低多人协作冲突。
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneBlueprintProjectConfig",
        menuName = "SceneBlueprint/Project Config",
        order = 70)]
    public sealed class SceneBlueprintProjectConfig : ScriptableObject
    {
        private static SceneBlueprintProjectConfig? _loadedInstance;

        [InspectorName("运行时配置 Runtime")]
        [SerializeField] private SceneBlueprintRuntimeSettingsData _runtime = new SceneBlueprintRuntimeSettingsData();

        [InspectorName("怪物映射 Monster Mapping")]
        [SerializeField] private SceneBlueprintMonsterMappingProjectSettings _monsterMappingSettings = new SceneBlueprintMonsterMappingProjectSettings();

        [InspectorName("刷怪编辑约束 Spawn Authoring")]
        [SerializeField] private SceneBlueprintSpawnAuthoringProjectSettings _spawnAuthoringSettings = new SceneBlueprintSpawnAuthoringProjectSettings();

        [InspectorName("框架覆盖 Framework Overrides")]
        [SerializeField] private SceneBlueprintFrameworkOverrideSettings _frameworkOverrides = new SceneBlueprintFrameworkOverrideSettings();

        [InspectorName("项目集成 Integration")]
        [SerializeField] private SceneBlueprintIntegrationSettings _integration = new SceneBlueprintIntegrationSettings();

        /// <summary>项目运行时配置 section。</summary>
        public SceneBlueprintRuntimeSettingsData Runtime => _runtime;

        /// <summary>项目怪物映射分片根设置。</summary>
        public SceneBlueprintMonsterMappingProjectSettings MonsterMapping => _monsterMappingSettings;

        /// <summary>项目级刷怪编辑约束。</summary>
        public SceneBlueprintSpawnAuthoringProjectSettings SpawnAuthoring => _spawnAuthoringSettings;

        /// <summary>项目级框架覆盖 section。</summary>
        public SceneBlueprintFrameworkOverrideSettings FrameworkOverrides => _frameworkOverrides;

        /// <summary>项目业务集成 section。</summary>
        public SceneBlueprintIntegrationSettings Integration => _integration;

        public static SceneBlueprintProjectConfig? FindLoadedInstance()
        {
            if (_loadedInstance != null)
            {
                return _loadedInstance;
            }

            var loaded = Resources.FindObjectsOfTypeAll<SceneBlueprintProjectConfig>();
            if (loaded != null && loaded.Length > 0)
            {
                _loadedInstance = loaded[0];
            }

            return _loadedInstance;
        }

        /// <summary>
        /// 资产校验入口。
        /// </summary>
        private void OnValidate()
        {
            _runtime.Normalize();
            _monsterMappingSettings.Normalize();
            _spawnAuthoringSettings.Normalize();
            _frameworkOverrides.Normalize();
        }

    }
}
