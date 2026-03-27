#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Settings;
using UnityEngine;

namespace SceneBlueprint.Runtime
{
    /// <summary>
    /// 蓝图运行时全局配置——统一管理运行时行为、测试参数、调试选项等。
    /// <para>
    /// 配置项包括：
    /// - 逻辑帧率配置（Tick Rate）
    /// - 测试场景行为配置
    /// - 调试日志开关
    /// - 性能监控选项
    /// </para>
    /// </summary>
    public class BlueprintRuntimeSettings : ScriptableObject
    {
        // ══════════════════════════════════════════
        //  单例访问
        // ══════════════════════════════════════════

        private static BlueprintRuntimeSettings? _instance;

        /// <summary>
        /// 全局配置实例。
        /// <para>
        /// 该类型当前只作为兼容壳保留，真实数据源统一来自 <see cref="SceneBlueprintProjectConfig.Runtime"/>。
        /// 若项目配置尚未加载，则回退到本类中的代码默认值。
        /// </para>
        /// </summary>
        public static BlueprintRuntimeSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<BlueprintRuntimeSettings>();

                    if (GetProjectRuntimeSettings() == null)
                    {
                        Debug.LogWarning($"[BlueprintRuntimeSettings] 未找到项目配置，当前回退到代码默认值。\n" +
                                         $"请在 SceneBlueprint 设置中心检查 ProjectConfig.Runtime 配置。");
                    }
                }
                return _instance;
            }
        }

        // ══════════════════════════════════════════
        //  逻辑帧配置
        // ══════════════════════════════════════════

        [Header("═══ 逻辑帧配置 ═══")]
        [Tooltip("目标逻辑帧率（Tick/秒）。例如：10 表示每秒执行 10 个逻辑 Tick")]
        [SerializeField] private int _targetTickRate = 10;

        [Tooltip("每个 Unity 渲染帧执行的 Tick 数。\n" +
                 "- 自动模式（0）：根据 targetTickRate 和实际帧率自动计算\n" +
                 "- 手动模式（>0）：固定每帧执行指定数量的 Tick")]
        [SerializeField] private int _ticksPerFrame = 0;

        [Tooltip("秒数换算为 Tick 数时使用的统一舍入策略。建议默认使用 Ceil，避免实际等待时间短于配置值。")]
        [SerializeField] private BlueprintTimeRoundingMode _timeRoundingMode = BlueprintTimeRoundingMode.Ceil;

        /// <summary>目标逻辑帧率（Tick/秒）</summary>
        public int TargetTickRate => GetProjectRuntimeSettings()?.TargetTickRate ?? Mathf.Max(1, _targetTickRate);

        /// <summary>
        /// 秒数转换为 Tick 数时采用的舍入策略。
        /// <para>
        /// 该属性与 <see cref="TargetTickRate"/> 一起构成完整的时间语义配置。
        /// 如果只统一 TickRate、不统一舍入方式，运行时仍然会因为不同系统使用 Ceil / Round / Floor
        /// 而产生行为偏差。
        /// </para>
        /// </summary>
        public BlueprintTimeRoundingMode TimeRoundingMode => GetProjectRuntimeSettings()?.TimeRoundingMode ?? _timeRoundingMode;

        /// <summary>
        /// 当前运行时应使用的时间配置快照。
        /// <para>
        /// Adapter 会在 BeginTick 时读取此快照，并写入 FrameView，之后所有 System 只消费该快照，
        /// 从而避免在执行热路径中到处直接读取 ScriptableObject 单例。
        /// </para>
        /// </summary>
        public BlueprintTimeSettings TimeSettings => GetProjectRuntimeSettings()?.ToTimeSettings() ?? new BlueprintTimeSettings(TargetTickRate, TimeRoundingMode);

        /// <summary>
        /// 每个 Unity 渲染帧执行的 Tick 数。
        /// <para>
        /// - 返回 0：自动模式，需要根据实际帧率动态计算
        /// - 返回 >0：固定模式，每帧执行固定数量的 Tick
        /// </para>
        /// </summary>
        public int TicksPerFrame => GetProjectRuntimeSettings()?.TicksPerFrame ?? Mathf.Max(0, _ticksPerFrame);

        /// <summary>
        /// 根据目标帧率和实际 Unity 帧率计算每帧应执行的 Tick 数。
        /// <para>例如：targetTickRate=10, unityFPS=60 → 返回 1（每 6 帧执行 1 个 Tick）</para>
        /// </summary>
        public int CalculateTicksPerFrame(float unityFPS)
        {
            if (TicksPerFrame > 0) return TicksPerFrame; // 手动模式
            
            // 自动模式：根据帧率计算
            if (unityFPS <= 0) return 1;
            float ticksPerFrame = TargetTickRate / unityFPS;
            return Mathf.Max(1, Mathf.RoundToInt(ticksPerFrame));
        }

        // ══════════════════════════════════════════
        //  测试配置
        // ══════════════════════════════════════════

        [Header("═══ 测试配置 ═══")]
        [Tooltip("测试场景启动时是否自动加载并执行蓝图")]
        [SerializeField] private bool _autoRunInTestScene = true;

        [Tooltip("编辑器测试窗口中 [加载并执行] 的最大 Tick 限制（防止死循环）")]
        [SerializeField] private int _maxTicksLimit = 1000;

        [Tooltip("编辑器测试窗口中 [执行 N Ticks] 按钮的默认 Tick 数")]
        [SerializeField] private int _batchTickCount = 10;

        /// <summary>测试场景是否自动执行</summary>
        public bool AutoRunInTestScene => GetProjectRuntimeSettings()?.AutoRunInTestScene ?? _autoRunInTestScene;

        /// <summary>最大 Tick 限制</summary>
        public int MaxTicksLimit => GetProjectRuntimeSettings()?.MaxTicksLimit ?? Mathf.Max(100, _maxTicksLimit);

        /// <summary>批量执行的 Tick 数</summary>
        public int BatchTickCount => GetProjectRuntimeSettings()?.BatchTickCount ?? Mathf.Max(1, _batchTickCount);

        // ══════════════════════════════════════════
        //  调试配置
        // ══════════════════════════════════════════

        [Header("═══ 调试配置 ═══")]
        [Tooltip("启用详细日志（包括 BlueprintLoader、TransitionSystem 等的详细输出）")]
        [SerializeField] private bool _enableDetailedLogs = true;

        [Tooltip("在测试窗口逐 Tick 执行时输出 Tick 分隔日志")]
        [SerializeField] private bool _enableTickBoundaryLogs = false;

        [Tooltip("为 TransitionSystem 输出带来源上下文的激活日志")]
        [SerializeField] private bool _enableTransitionDetailLogs = true;

        [Tooltip("为 Trigger.EnterArea 输出初始化和等待诊断日志")]
        [SerializeField] private bool _enableTriggerWaitDiagnostics = true;

        [Tooltip("蓝图结束时输出一份运行摘要")]
        [SerializeField] private bool _enableCompletionSummaryLogs = true;

        [Tooltip("记录每个 System 的执行信息（性能分析用）")]
        [SerializeField] private bool _logSystemExecution = false;

        /// <summary>是否启用详细日志</summary>
        public bool EnableDetailedLogs => GetProjectRuntimeSettings()?.EnableDetailedLogs ?? _enableDetailedLogs;

        /// <summary>是否输出 Tick 边界日志</summary>
        public bool EnableTickBoundaryLogs => GetProjectRuntimeSettings()?.EnableTickBoundaryLogs ?? _enableTickBoundaryLogs;

        /// <summary>是否输出转场详情日志</summary>
        public bool EnableTransitionDetailLogs => GetProjectRuntimeSettings()?.EnableTransitionDetailLogs ?? _enableTransitionDetailLogs;

        /// <summary>是否输出 Trigger 等待诊断</summary>
        public bool EnableTriggerWaitDiagnostics => GetProjectRuntimeSettings()?.EnableTriggerWaitDiagnostics ?? _enableTriggerWaitDiagnostics;

        /// <summary>是否输出运行结束摘要</summary>
        public bool EnableCompletionSummaryLogs => GetProjectRuntimeSettings()?.EnableCompletionSummaryLogs ?? _enableCompletionSummaryLogs;

        /// <summary>是否记录 System 执行</summary>
        public bool LogSystemExecution => GetProjectRuntimeSettings()?.LogSystemExecution ?? _logSystemExecution;

        // ══════════════════════════════════════════
        //  性能配置
        // ══════════════════════════════════════════

        [Header("═══ 性能配置 ═══")]
        [Tooltip("在测试场景中显示性能统计信息（Tick 耗时、System 耗时等）")]
        [SerializeField] private bool _showPerformanceStats = false;

        /// <summary>是否显示性能统计</summary>
        public bool ShowPerformanceStats => GetProjectRuntimeSettings()?.ShowPerformanceStats ?? _showPerformanceStats;

        // ══════════════════════════════════════════
        //  编辑器辅助
        // ══════════════════════════════════════════

        private void OnValidate()
        {
            // 确保配置值在合理范围内
            _targetTickRate = Mathf.Max(1, _targetTickRate);
            _ticksPerFrame = Mathf.Max(0, _ticksPerFrame);
            _maxTicksLimit = Mathf.Max(100, _maxTicksLimit);
            _batchTickCount = Mathf.Max(1, _batchTickCount);
        }

        private static SceneBlueprintRuntimeSettingsData? GetProjectRuntimeSettings()
        {
            return SceneBlueprintProjectConfig.FindLoadedInstance()?.Runtime;
        }
    }
}
