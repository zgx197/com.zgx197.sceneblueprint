#nullable enable
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
    /// <para>
    /// 使用方式：
    /// 1. 在 Project 窗口右键 → Create → SceneBlueprint → Runtime Settings
    /// 2. 将创建的配置文件放在 Resources 目录下（推荐路径：Assets/Resources/SceneBlueprintRuntimeSettings.asset）
    /// 3. 代码中通过 BlueprintRuntimeSettings.Instance 访问
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneBlueprintRuntimeSettings",
        menuName = "SceneBlueprint/Runtime Settings",
        order = 90)]
    public class BlueprintRuntimeSettings : ScriptableObject
    {
        // ══════════════════════════════════════════
        //  单例访问
        // ══════════════════════════════════════════

        private static BlueprintRuntimeSettings? _instance;

        /// <summary>
        /// 配置文件的资源路径（相对于 Resources 目录）。
        /// 用户需将配置文件放在项目 Resources 目录的该子路径下，
        /// 例如：Assets/Resources/SceneBlueprint/SceneBlueprintRuntimeSettings.asset
        /// </summary>
        private const string ResourcePath = "SceneBlueprint/SceneBlueprintRuntimeSettings";

        /// <summary>
        /// 全局配置实例（从 Resources 加载）。
        /// <para>如果未找到配置文件，返回默认配置。</para>
        /// </summary>
        public static BlueprintRuntimeSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<BlueprintRuntimeSettings>(ResourcePath);
                    if (_instance == null)
                    {
                        Debug.LogWarning($"[BlueprintRuntimeSettings] 未找到配置文件，使用默认配置。\n" +
                                         $"请在 Unity 菜单 SceneBlueprint → 打开运行时设置 创建配置文件");
                        _instance = CreateInstance<BlueprintRuntimeSettings>();
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

        /// <summary>目标逻辑帧率（Tick/秒）</summary>
        public int TargetTickRate => Mathf.Max(1, _targetTickRate);

        /// <summary>
        /// 每个 Unity 渲染帧执行的 Tick 数。
        /// <para>
        /// - 返回 0：自动模式，需要根据实际帧率动态计算
        /// - 返回 >0：固定模式，每帧执行固定数量的 Tick
        /// </para>
        /// </summary>
        public int TicksPerFrame => Mathf.Max(0, _ticksPerFrame);

        /// <summary>
        /// 根据目标帧率和实际 Unity 帧率计算每帧应执行的 Tick 数。
        /// <para>例如：targetTickRate=10, unityFPS=60 → 返回 1（每 6 帧执行 1 个 Tick）</para>
        /// </summary>
        public int CalculateTicksPerFrame(float unityFPS)
        {
            if (_ticksPerFrame > 0) return _ticksPerFrame; // 手动模式
            
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
        public bool AutoRunInTestScene => _autoRunInTestScene;

        /// <summary>最大 Tick 限制</summary>
        public int MaxTicksLimit => Mathf.Max(100, _maxTicksLimit);

        /// <summary>批量执行的 Tick 数</summary>
        public int BatchTickCount => Mathf.Max(1, _batchTickCount);

        // ══════════════════════════════════════════
        //  调试配置
        // ══════════════════════════════════════════

        [Header("═══ 调试配置 ═══")]
        [Tooltip("启用详细日志（包括 BlueprintLoader、TransitionSystem 等的详细输出）")]
        [SerializeField] private bool _enableDetailedLogs = true;

        [Tooltip("记录每个 System 的执行信息（性能分析用）")]
        [SerializeField] private bool _logSystemExecution = false;

        /// <summary>是否启用详细日志</summary>
        public bool EnableDetailedLogs => _enableDetailedLogs;

        /// <summary>是否记录 System 执行</summary>
        public bool LogSystemExecution => _logSystemExecution;

        // ══════════════════════════════════════════
        //  性能配置
        // ══════════════════════════════════════════

        [Header("═══ 性能配置 ═══")]
        [Tooltip("在测试场景中显示性能统计信息（Tick 耗时、System 耗时等）")]
        [SerializeField] private bool _showPerformanceStats = false;

        /// <summary>是否显示性能统计</summary>
        public bool ShowPerformanceStats => _showPerformanceStats;

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
    }
}
