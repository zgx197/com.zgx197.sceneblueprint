#nullable enable
using SceneBlueprint.Runtime.Interpreter;
using SceneBlueprint.Runtime.Interpreter.Diagnostics;
using SceneBlueprint.Runtime.Interpreter.Systems;
using UnityEngine;

namespace SceneBlueprint.Runtime.Test
{
    /// <summary>
    /// 蓝图运行时测试管理器——驱动蓝图执行并协调可视化生成。
    /// <para>
    /// 职责：
    /// 1. 加载蓝图 JSON 数据
    /// 2. 创建 BlueprintRunner 并注册 System
    /// 3. 将 MonsterSpawner 注入 SpawnPresetSystem / SpawnWaveSystem
    /// 4. 逐帧驱动 Tick 使波次间隔等持续型行为在视觉上可观察
    /// 5. 提供运行时状态展示（UI）
    /// </para>
    /// </summary>
    public class BlueprintRuntimeManager : MonoBehaviour
    {
        [Header("蓝图数据")]
        [Tooltip("拖入导出的蓝图 JSON 文件")]
        [SerializeField] private TextAsset? _blueprintJson;

        [Header("组件引用")]
        [SerializeField] private MonsterSpawner? _monsterSpawner;

        private BlueprintRunner? _runner;
        private bool _running;
        private bool _loaded;
        private string _statusText = "等待加载...";
        private float _tickAccumulator = 0f;

        /// <summary>当前 Runner 实例（外部访问用）</summary>
        public BlueprintRunner? Runner => _runner;

        /// <summary>运行时配置（从全局设置读取）</summary>
        private BlueprintRuntimeSettings Settings => BlueprintRuntimeSettings.Instance;

        private void Start()
        {
            if (Settings.AutoRunInTestScene && _blueprintJson != null)
            {
                LoadBlueprint();
            }
        }

        private void Update()
        {
            if (!_running || _runner == null || _runner.IsCompleted)
                return;

            // 根据目标 Tick 率计算每帧应执行的 Tick 数
            int ticksPerFrame = Settings.TicksPerFrame;
            if (ticksPerFrame == 0)
            {
                // 自动模式：根据实际帧率动态计算
                float targetTickRate = Settings.TargetTickRate;
                _tickAccumulator += targetTickRate * Time.deltaTime;
                ticksPerFrame = Mathf.FloorToInt(_tickAccumulator);
                _tickAccumulator -= ticksPerFrame;
            }

            ticksPerFrame = Mathf.Max(1, ticksPerFrame);

            for (int i = 0; i < ticksPerFrame; i++)
            {
                _runner.Tick();
                if (_runner.IsCompleted)
                {
                    _running = false;
                    _statusText = $"执行完毕 — 共 {_runner.TickCount} Tick";
                    Debug.Log($"[BlueprintRuntimeManager] {_statusText}");
                    break;
                }
            }

            if (_running)
            {
                _statusText = $"执行中 — Tick {_runner.TickCount}";
            }
        }

        /// <summary>加载蓝图并开始逐帧执行</summary>
        public void LoadBlueprint()
        {
            if (_blueprintJson == null)
            {
                _statusText = "错误：未指定蓝图 JSON 文件";
                Debug.LogError("[BlueprintRuntimeManager] " + _statusText);
                return;
            }

            if (_monsterSpawner != null) _monsterSpawner.ClearAll();

            _runner = new BlueprintRunner
            {
                Log = msg => Debug.Log(msg),
                LogWarning = msg => Debug.LogWarning(msg),
                LogError = msg => Debug.LogError(msg)
            };

            var spawnPresetSystem = new SpawnPresetSystem();
            var spawnWaveSystem = new SpawnWaveSystem();
            var cameraShakeSystem = new CameraShakeSystem();
            var showWarningSystem = new ShowWarningSystem();
            if (_monsterSpawner != null)
            {
                spawnPresetSystem.SpawnHandler = _monsterSpawner;
                spawnWaveSystem.SpawnHandler = _monsterSpawner;
            }

            // 摄像机震动：动态查找或创建 Handler
            // 摄像机由 SimplePlayerController 在运行时动态创建，无法预先拖引用
            cameraShakeSystem.ShakeHandler = FindOrCreateShakeHandler();

            // 屏幕警告：挂载到自身（需要 OnGUI 绘制）
            showWarningSystem.WarningHandler = FindOrCreateWarningHandler();

            _runner.RegisterSystems(
                new TransitionSystem(),
                new FlowSystem(),
                new BlackboardSetSystem(),
                new BlackboardGetSystem(),
                new FlowFilterSystem(),
                spawnPresetSystem,
                spawnWaveSystem,
                new TriggerEnterAreaSystem(),
                cameraShakeSystem,
                showWarningSystem
            );

            // 创建调试控制器并连线场景处 Handler 的暂停感知
            var debugCtrl = new BlueprintDebugController();
            WireDebugPauseEvents(debugCtrl,
                cameraShakeSystem.ShakeHandler as CameraShakeHandler,
                showWarningSystem.WarningHandler as ShowWarningHandler);
            _runner.DebugController = debugCtrl;

            _statusText = "正在加载...";
            _runner.Load(_blueprintJson.text);

            _loaded = true;
            _running = true;
            _statusText = "执行中 — Tick 0";
        }

        /// <summary>重新加载并执行</summary>
        public void Reload()
        {
            _runner?.Shutdown();
            _runner = null;
            _running = false;
            _loaded = false;
            LoadBlueprint();
        }

        // ── 调试暂停事件连线 ──

        /// <summary>
        /// 将 DebugController 的 OnPaused/OnResumed 事件连线到场景侧 Handler，
        /// 使 Blueprint 暂停时场景效果同步冻结。
        /// </summary>
        private static void WireDebugPauseEvents(
            BlueprintDebugController ctrl,
            CameraShakeHandler?      shakeHandler,
            ShowWarningHandler?      warningHandler)
        {
            if (shakeHandler != null)
            {
                ctrl.OnPaused  += shakeHandler.OnBlueprintPaused;
                ctrl.OnResumed += shakeHandler.OnBlueprintResumed;
            }

            if (warningHandler != null)
            {
                ctrl.OnPaused  += warningHandler.OnBlueprintPaused;
                ctrl.OnResumed += warningHandler.OnBlueprintResumed;
            }

            // MonsterBehavior 是动态生成的，通过静态标志统一冻结所有实例的 AI 逻辑
            ctrl.OnPaused  += () => MonsterBehavior.IsBlueprintPaused = true;
            ctrl.OnResumed += () => MonsterBehavior.IsBlueprintPaused = false;
        }

        // ── Handler 动态查找 ──

        /// <summary>
        /// 动态查找或创建 CameraShakeHandler。
        /// 摄像机由 SimplePlayerController 在 Awake 中动态创建，
        /// 此时（LoadBlueprint 在 Start 中调用）摄像机已存在。
        /// 如果 Camera.main 上没有 Handler 组件，自动挂载一个。
        /// </summary>
        private ICameraShakeHandler? FindOrCreateShakeHandler()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[BlueprintRuntimeManager] 未找到 Main Camera，摄像机震动将仅输出日志");
                return null;
            }

            var handler = cam.GetComponent<CameraShakeHandler>();
            if (handler == null)
            {
                handler = cam.gameObject.AddComponent<CameraShakeHandler>();
                Debug.Log("[BlueprintRuntimeManager] 已在 Main Camera 上自动挂载 CameraShakeHandler");
            }

            return handler;
        }

        /// <summary>
        /// 动态查找或创建 ShowWarningHandler。
        /// 挂载到 BlueprintRuntimeManager 自身（需要 MonoBehaviour 的 OnGUI 回调绘制 UI）。
        /// </summary>
        private IShowWarningHandler FindOrCreateWarningHandler()
        {
            var handler = GetComponent<ShowWarningHandler>();
            if (handler == null)
            {
                handler = gameObject.AddComponent<ShowWarningHandler>();
                Debug.Log("[BlueprintRuntimeManager] 已自动挂载 ShowWarningHandler");
            }
            return handler;
        }

        // ── 简易运行时 UI ──

        private void OnGUI()
        {
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8)
            };

            var area = new Rect(10, 10, 320, 120);
            GUI.Box(area, "", boxStyle);

            GUILayout.BeginArea(new Rect(20, 18, 300, 100));

            GUILayout.Label("<b>蓝图运行时测试</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });
            GUILayout.Label($"状态: {_statusText}");

            if (_blueprintJson != null)
            {
                GUILayout.Label($"蓝图: {_blueprintJson.name}");
            }

            if (_runner != null)
            {
                int ticksPerFrame = Settings.TicksPerFrame;
                if (ticksPerFrame == 0)
                {
                    GUILayout.Label($"Tick 率: {Settings.TargetTickRate}/秒 (自动)");
                }
                else
                {
                    GUILayout.Label($"Tick/帧: {ticksPerFrame} (手动)");
                }
            }

            GUILayout.EndArea();

            if (GUI.Button(new Rect(10, 135, 100, 30), _loaded ? "重新加载" : "加载执行"))
            {
                if (_loaded) Reload();
                else LoadBlueprint();
            }

            var helpRect = new Rect(Screen.width - 240, 10, 230, 70);
            GUI.Box(helpRect, "");
            GUI.Label(new Rect(helpRect.x + 8, helpRect.y + 6, 220, 60),
                "WASD: 移动\n鼠标右键拖拽: 旋转视角\n滚轮: 缩放",
                new GUIStyle(GUI.skin.label) { fontSize = 12 });
        }
    }
}
