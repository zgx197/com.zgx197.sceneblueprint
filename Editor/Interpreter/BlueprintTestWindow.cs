#nullable enable
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Runtime.Interpreter;
using SceneBlueprint.Runtime.Interpreter.Systems;
using SceneBlueprint.Runtime.Interpreter.Diagnostics;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.Interpreter
{
    /// <summary>
    /// 蓝图运行时测试窗口——在编辑器中一键加载并执行导出的蓝图 JSON，验证运行时解释器。
    /// <para>
    /// 使用方式：
    /// 1. 菜单 SceneBlueprint → 运行时测试
    /// 2. 拖入导出的 JSON 文件（TextAsset）
    /// 3. 点击 [加载并执行] → 在 Console 中查看执行日志
    /// 4. 或点击 [逐帧执行] → 手动 Tick 观察每帧状态
    /// </para>
    /// </summary>
    public class BlueprintTestWindow : EditorWindow
    {
        [MenuItem("SceneBlueprint/运行时测试", priority = 2000)]
        private static void Open()
        {
            var window = GetWindow<BlueprintTestWindow>("蓝图运行时测试");
            window.minSize = new Vector2(400, 300);
        }

        // ── 序列化字段（Inspector 持久化）──
        [SerializeField] private TextAsset? _jsonAsset;

        // ── 运行时状态 ──
        private BlueprintRunner? _runner;
        private string _statusText = "未加载";
        private Vector2 _scrollPos;

        // ── 调试历史 ──
        private BlueprintDebugController? _debugCtrl;
        private int  _inspectTick = -1;   // -1 = 跟随最新帧
        private bool _showDebug   = true; // 调试面板折叠状态
        private Vector2 _diffScrollPos;
        private Vector2 _bbScrollPos;

        /// <summary>运行时配置（从全局设置读取）</summary>
        private BlueprintRuntimeSettings Settings => BlueprintRuntimeSettings.Instance;

        // ══════════════════════════════════════════
        //  GUI
        // ══════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("蓝图运行时解释器测试", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            // ── 输入区 ──
            _jsonAsset = (TextAsset?)EditorGUILayout.ObjectField(
                "蓝图 JSON", _jsonAsset, typeof(TextAsset), false);

            EditorGUILayout.Space(8);

            // ── 操作按钮 ──
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _jsonAsset != null;

                if (GUILayout.Button("加载并执行", GUILayout.Height(30)))
                {
                    LoadAndRunAll();
                }

                if (GUILayout.Button("加载（不执行）", GUILayout.Height(30)))
                {
                    LoadOnly();
                }

                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _runner?.Frame != null && !_runner.IsCompleted;

                if (GUILayout.Button("单步 Tick", GUILayout.Height(24)))
                {
                    StepTick();
                }

                if (GUILayout.Button("执行 10 Ticks", GUILayout.Height(24)))
                {
                    StepTicks(Settings.BatchTickCount);
                }

                GUI.enabled = _runner != null;

                if (GUILayout.Button("重置", GUILayout.Height(24)))
                {
                    Reset();
                }

                GUI.enabled = true;
            }

            EditorGUILayout.Space(8);

            // ── 状态显示 ──
            EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_statusText, MessageType.Info);

            // ── Action 状态表 ──
            if (_runner?.Frame != null)
            {
                EditorGUILayout.Space(4);

                // 调试历史面板
                if (_debugCtrl != null)
                    DrawDebugPanel();
                else
                {
                    EditorGUILayout.LabelField("Action 状态", EditorStyles.boldLabel);
                    DrawActionStates(null);
                }
            }
        }

        // ══════════════════════════════════════════
        //  Action 状态表绘制
        // ══════════════════════════════════════════

        /// <summary>
        /// 绘制 Action 状态表。
        /// <paramref name="snapshot"/> 为 null 时显示实时 Frame 数据；
        /// 传入快照时显示历史帧数据，并在 Phase 发生变化的行旁显示 diff 箭头。
        /// </summary>
        private void DrawActionStates(BlueprintFrameSnapshot? snapshot)
        {
            var frame    = _runner!.Frame!;
            int rowCount = snapshot != null ? snapshot.States.Length : frame.ActionCount;

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(200));

            // 表头
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Idx",   GUILayout.Width(30));
                GUILayout.Label("TypeId",GUILayout.Width(160));
                GUILayout.Label("Phase", GUILayout.Width(100));
                GUILayout.Label("Ticks", GUILayout.Width(44));
                GUILayout.Label("变化",   GUILayout.Width(120));
            }

            // 表体
            for (int i = 0; i < rowCount; i++)
            {
                string typeId;
                ActionPhase phase;
                int    ticks;

                if (snapshot != null)
                {
                    typeId = i < frame.Actions.Length ? frame.Actions[i].TypeId : $"#{i}";
                    phase  = snapshot.States[i].Phase;
                    ticks  = snapshot.States[i].TicksInPhase;
                }
                else
                {
                    typeId = frame.GetTypeId(i);
                    phase  = frame.States[i].Phase;
                    ticks  = frame.States[i].TicksInPhase;
                }

                // 找到该行对应的 diff（如果有）
                string diffLabel = "";
                if (snapshot != null)
                {
                    foreach (var d in snapshot.Diffs)
                    {
                        if (d.ActionIndex == i)
                        {
                            diffLabel = $"{d.PhaseBefore} → {d.PhaseAfter}";
                            break;
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(i.ToString(), GUILayout.Width(30));
                    GUILayout.Label(typeId, GUILayout.Width(160));

                    var prevColor = GUI.contentColor;
                    GUI.contentColor = GetPhaseColor(phase);
                    GUILayout.Label(phase.ToString(), GUILayout.Width(100));
                    GUI.contentColor = prevColor;

                    GUILayout.Label(ticks.ToString(), GUILayout.Width(44));

                    if (diffLabel != "")
                    {
                        GUI.contentColor = new Color(1f, 0.85f, 0.2f);
                        GUILayout.Label(diffLabel, GUILayout.Width(120));
                        GUI.contentColor = prevColor;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════
        //  调试历史面板
        // ══════════════════════════════════════════

        private void DrawDebugPanel()
        {
            var h = _debugCtrl!.History;

            _showDebug = EditorGUILayout.Foldout(_showDebug, "调试历史", true, EditorStyles.foldoutHeader);
            if (!_showDebug) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                // ── 时间轴控制栏 ──
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isLive = _inspectTick < 0;
                    GUI.color = isLive ? new Color(0.5f, 1f, 0.5f) : Color.white;
                    if (GUILayout.Button("跟随最新帧", GUILayout.Width(88)))
                        _inspectTick = -1;
                    GUI.color = Color.white;

                    int oldest = h.OldestTick >= 0 ? h.OldestTick : 0;
                    int latest = h.LatestTick >= 0 ? h.LatestTick : 0;

                    GUI.enabled = h.Count >= 2;
                    int displayTick = isLive ? latest : _inspectTick;
                    int newTick = EditorGUILayout.IntSlider(displayTick, oldest, latest);
                    if (newTick != displayTick)
                        _inspectTick = newTick;
                    GUI.enabled = true;

                    GUILayout.Label(
                        isLive ? $"T={latest} (实时)" : $"T={_inspectTick}",
                        GUILayout.Width(80));

                    GUILayout.Label($"{h.Count}/{h.Capacity}", GUILayout.Width(60));
                }

                // ── Action 状态（实时或历史快照）──
                bool showingLive = _inspectTick < 0;
                BlueprintFrameSnapshot? snap = showingLive
                    ? null
                    : h.GetByTick(_inspectTick);

                EditorGUILayout.LabelField(
                    showingLive ? "Action 状态（实时）" : $"Action 状态 @ T={_inspectTick}",
                    EditorStyles.boldLabel);
                DrawActionStates(snap);

                // ── Diff + Events（仅历史模式）──
                if (snap != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Phase 变化 & 排队事件", EditorStyles.boldLabel);

                    _diffScrollPos = EditorGUILayout.BeginScrollView(_diffScrollPos, GUILayout.MaxHeight(100));

                    foreach (var d in snap.Diffs)
                    {
                        string tid = d.ActionIndex < _runner!.Frame!.Actions.Length
                            ? _runner.Frame.Actions[d.ActionIndex].TypeId
                            : $"#{d.ActionIndex}";
                        var prevC = GUI.contentColor;
                        GUI.contentColor = GetPhaseColor(d.PhaseAfter);
                        EditorGUILayout.LabelField(
                            $"[{d.ActionIndex}] {tid}: {d.PhaseBefore} → {d.PhaseAfter}");
                        GUI.contentColor = prevC;
                    }

                    if (snap.Diffs.Length == 0)
                        EditorGUILayout.LabelField("  （本帧无 Phase 变化）");

                    EditorGUILayout.Space(2);
                    foreach (var ev in snap.PendingEventsSnapshot)
                    {
                        var prevC = GUI.contentColor;
                        GUI.contentColor = new Color(1f, 0.85f, 0.3f);
                        EditorGUILayout.LabelField(
                            $"  事件: [{ev.FromActionIndex}]:{ev.FromPortId} → [{ev.ToActionIndex}]:{ev.ToPortId}");
                        GUI.contentColor = prevC;
                    }

                    if (snap.PendingEventsSnapshot.Length == 0)
                        EditorGUILayout.LabelField("  （无排队事件）");

                    EditorGUILayout.EndScrollView();

                    // ── Blackboard 变量（历史快照）──
                    EditorGUILayout.Space(4);
                    DrawBlackboardEntries(snap.BlackboardEntries);
                }
                else
                {
                    // ── Blackboard 变量（实时）──
                    EditorGUILayout.Space(4);
                    DrawBlackboardLive();
                }
            }
        }

        private void DrawBlackboardEntries(BlackboardEntry[] entries)
        {
            EditorGUILayout.LabelField("Blackboard 变量", EditorStyles.boldLabel);
            if (entries.Length == 0)
            {
                EditorGUILayout.LabelField("  （无声明变量）");
                return;
            }

            _bbScrollPos = EditorGUILayout.BeginScrollView(_bbScrollPos, GUILayout.MaxHeight(80));
            foreach (var e in entries)
            {
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(0.7f, 1f, 0.85f);
                EditorGUILayout.LabelField($"  {e.Name}[{e.Index}] : {e.TypeStr} = {e.ValueStr}");
                GUI.contentColor = prev;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBlackboardLive()
        {
            if (_runner?.Frame == null) return;

            var declared = _runner.Frame.Blackboard.DeclaredEntries;
            EditorGUILayout.LabelField("Blackboard 变量（实时）", EditorStyles.boldLabel);
            if (declared.Count == 0)
            {
                EditorGUILayout.LabelField("  （无声明变量）");
                return;
            }

            _bbScrollPos = EditorGUILayout.BeginScrollView(_bbScrollPos, GUILayout.MaxHeight(80));
            foreach (var kvp in declared)
            {
                var decl  = _runner.Frame.FindVariable(kvp.Key);
                string name    = decl?.Name ?? kvp.Key.ToString();
                string typeStr = decl?.Type  ?? kvp.Value.ValueType.Name;
                string valStr  = kvp.Value.BoxedValue?.ToString() ?? "null";
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(0.7f, 1f, 0.85f);
                EditorGUILayout.LabelField($"  {name}[{kvp.Key}] : {typeStr} = {valStr}");
                GUI.contentColor = prev;
            }
            EditorGUILayout.EndScrollView();
        }

        private static Color GetPhaseColor(ActionPhase phase) => phase switch
        {
            ActionPhase.Idle => Color.gray,
            ActionPhase.WaitingTrigger => new Color(1f, 0.8f, 0.2f),
            ActionPhase.Running => new Color(0.3f, 0.8f, 1f),
            ActionPhase.Completed => new Color(0.3f, 0.9f, 0.3f),
            ActionPhase.Listening => new Color(0.6f, 0.5f, 1f), // 紫色——监听等待中
            ActionPhase.Failed => new Color(1f, 0.3f, 0.3f),
            _ => Color.white
        };

        // ══════════════════════════════════════════
        //  操作实现
        // ══════════════════════════════════════════

        private BlueprintRunner CreateRunner()
        {
            _inspectTick = -1;
            _debugCtrl   = new BlueprintDebugController(600);

            var runner = new BlueprintRunner
            {
                Log = msg => UnityEngine.Debug.Log(msg),
                LogWarning = msg => UnityEngine.Debug.LogWarning(msg),
                LogError = msg => UnityEngine.Debug.LogError(msg),
                DebugController = _debugCtrl
            };

            // 注册所有基础 System
            runner.RegisterSystems(
                new TransitionSystem(),
                new FlowSystem(),
                new FlowFilterSystem(),
                new SpawnPresetSystem(),
                new SpawnWaveSystem(),
                new TriggerEnterAreaSystem(),
                new CameraShakeSystem(),
                new ShowWarningSystem()
            );

            return runner;
        }

        private void LoadAndRunAll()
        {
            if (_jsonAsset == null) return;

            _runner = CreateRunner();
            _runner.Load(_jsonAsset.text);

            if (_runner.Frame == null)
            {
                _statusText = "加载失败，请查看 Console";
                return;
            }

            UnityEngine.Debug.Log("══════════════════════════════════════════");
            UnityEngine.Debug.Log("  蓝图运行时测试 - 开始执行");
            UnityEngine.Debug.Log("══════════════════════════════════════════");

            var ticks = _runner.RunUntilComplete(maxTicks: Settings.MaxTicksLimit);

            _statusText = _runner.IsCompleted
                ? $"执行完毕！共 {ticks} Tick，{_runner.Frame.ActionCount} 个 Action"
                : $"达到最大 Tick 限制 ({Settings.MaxTicksLimit})，尚未完成";

            UnityEngine.Debug.Log("══════════════════════════════════════════");
            UnityEngine.Debug.Log($"  蓝图运行时测试 - {_statusText}");
            UnityEngine.Debug.Log("══════════════════════════════════════════");

            Repaint();
        }

        private void LoadOnly()
        {
            if (_jsonAsset == null) return;

            _runner = CreateRunner();
            _runner.Load(_jsonAsset.text);

            if (_runner.Frame == null)
            {
                _statusText = "加载失败，请查看 Console";
                return;
            }

            _statusText = $"已加载: {_runner.Frame.BlueprintName} " +
                          $"({_runner.Frame.ActionCount} Actions, " +
                          $"{_runner.Frame.Transitions.Length} Transitions)";

            Repaint();
        }

        private void StepTick()
        {
            if (_runner?.Frame == null || _runner.IsCompleted) return;

            UnityEngine.Debug.Log($"────── Tick {_runner.TickCount + 1} ──────");
            _runner.Tick();

            _statusText = _runner.IsCompleted
                ? $"执行完毕！Tick={_runner.TickCount}"
                : $"Tick {_runner.TickCount}，活跃 Action: {CountActiveActions()}";

            Repaint();
        }

        private void StepTicks(int count)
        {
            for (int i = 0; i < count && !(_runner?.IsCompleted ?? true); i++)
            {
                StepTick();
            }
        }

        private void Reset()
        {
            _runner?.Shutdown();
            _runner      = null;
            _debugCtrl   = null;
            _inspectTick = -1;
            _statusText  = "已重置";
            Repaint();
        }

        private int CountActiveActions()
        {
            if (_runner?.Frame == null) return 0;
            int count = 0;
            for (int i = 0; i < _runner.Frame.States.Length; i++)
            {
                var phase = _runner.Frame.States[i].Phase;
                if (phase == ActionPhase.Running ||
                    phase == ActionPhase.WaitingTrigger ||
                    phase == ActionPhase.Listening)
                    count++;
            }
            return count;
        }

        private void OnDestroy()
        {
            _runner?.Shutdown();
        }
    }
}
