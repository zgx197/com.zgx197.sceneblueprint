#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Interpreter;
using SceneBlueprint.Runtime.Interpreter.Systems;
using SceneBlueprint.Runtime.Interpreter.Diagnostics;
using SceneBlueprint.Runtime.Testing;
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
    public partial class BlueprintTestWindow : EditorWindow
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

        // ── 视图模式 ──
        private enum StateViewMode { Live, History, Summary }
        private bool _showEventsPanel = true;
        private bool _showBlackboard  = true;

        /// <summary>运行时配置（从全局设置读取）</summary>
        private BlueprintRuntimeSettings Settings => BlueprintRuntimeSettings.Instance;

        // ── SceneView 可视化 ──
        [SerializeField] private bool _showSceneViz = true;
        private readonly List<SceneVizAreaEntry>  _vizAreas  = new();
        private readonly List<SceneVizPointEntry> _vizPoints = new();
        private readonly List<string> _runtimeCoverageWarnings = new();

        private const int CircleSeg = 48;

        // 颜色约定
        private static readonly Color VizColorTrigger = new Color(0.9f, 0.2f, 0.2f, 0.85f);
        private static readonly Color VizColorWave    = new Color(0.2f, 0.85f, 0.85f, 0.85f);
        private static readonly Color VizColorPreset  = new Color(0.3f, 0.9f, 0.3f, 0.85f);
        private static readonly Color VizColorPoint   = new Color(0.95f, 0.82f, 0.1f, 0.9f);
        private static readonly Color VizColorDefault = new Color(0.7f, 0.7f, 0.7f, 0.65f);

        private enum VizShapeType { Polygon, Circle, Capsule }

        private struct SceneVizAreaEntry
        {
            public string       Label;
            public Color        Color;
            public VizShapeType ShapeType;
            public float        Height;
            public Vector3[]?   FloorVertices; // Polygon
            public Vector3      Center;
            public float        Radius;        // Circle / Capsule
            public Vector3      PointA;        // Capsule
            public Vector3      PointB;        // Capsule
        }

        private struct SceneVizPointEntry
        {
            public string     Label;
            public Vector3    Position;
            public Quaternion Rotation;
        }

        // ══════════════════════════════════════════
        //  SceneView 注册
        // ══════════════════════════════════════════

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // ══════════════════════════════════════════
        //  GUI
        // ══════════════════════════════════════════

        private void OnGUI()
        {
            // ═══════════ 顶部工具栏（固定高度）═══════════
            DrawToolbar();

            // ═══════════ 中部主体（弹性高度）═══════════
            if (_runner?.Frame != null)
            {
                // 调试历史面板（包含时间轴 + Action 状态 + 事件/Blackboard）
                if (_debugCtrl != null)
                    DrawDebugPanel();
                else
                {
                    EditorGUILayout.LabelField("Action 状态", EditorStyles.boldLabel);
                    DrawActionStates(null, StateViewMode.Live);
                }
            }
        }

        /// <summary>绘制顶部工具栏：输入、按钮、状态信息</summary>
        private void DrawToolbar()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("蓝图运行时解释器测试", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── 输入区 ──
            _jsonAsset = (TextAsset?)EditorGUILayout.ObjectField(
                "蓝图 JSON", _jsonAsset, typeof(TextAsset), false);

            EditorGUILayout.Space(4);

            // ── 操作按钮（第一行）──
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _jsonAsset != null;
                if (GUILayout.Button("加载并执行", GUILayout.Height(26)))
                    LoadAndRunAll();
                if (GUILayout.Button("加载（不执行）", GUILayout.Height(26)))
                    LoadOnly();
                GUI.enabled = true;
            }

            // ── 操作按钮（第二行）──
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _runner?.Frame != null && !_runner.IsCompleted;
                if (GUILayout.Button("单步 Tick", GUILayout.Height(22)))
                    StepTick();
                if (GUILayout.Button($"执行 {Settings.BatchTickCount} Ticks", GUILayout.Height(22)))
                    StepTicks(Settings.BatchTickCount);
                GUI.enabled = _runner != null;
                if (GUILayout.Button("重置", GUILayout.Height(22)))
                    Reset();
                GUI.enabled = true;
            }

            // ── 状态栏 + SceneView 开关（合并为一行）──
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                // 状态标签（带颜色）
                var statusColor = _runner == null ? Color.gray
                    : _runner.IsCompleted ? new Color(0.3f, 0.9f, 0.3f)
                    : new Color(0.3f, 0.8f, 1f);
                var prevColor = GUI.contentColor;
                GUI.contentColor = statusColor;
                EditorGUILayout.LabelField(_statusText, EditorStyles.miniLabel);
                GUI.contentColor = prevColor;

                // SceneView 可视化开关
                var newViz = GUILayout.Toggle(_showSceneViz, "SceneView", EditorStyles.miniButton, GUILayout.Width(70));
                if (newViz != _showSceneViz)
                {
                    _showSceneViz = newViz;
                    SceneView.RepaintAll();
                }
            }

            // 分隔线
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.Space(2);

            if (_runtimeCoverageWarnings.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", _runtimeCoverageWarnings), MessageType.Warning);
            }
        }

        // ══════════════════════════════════════════
        //  Action 状态表绘制
        // ══════════════════════════════════════════

        /// <summary>
        /// 绘制 Action 状态表。
        /// <para>
        /// viewMode:
        /// - Live: 从 frame.States[] 读取实时数据
        /// - History: 从 snapshot 读取历史帧数据，显示 diff
        /// - Summary: 从 PeakStateTracker 读取峰值数据（蓝图完成后的汇总视图）
        /// </para>
        /// </summary>
        private void DrawActionStates(BlueprintFrameSnapshot? snapshot, StateViewMode viewMode)
        {
            var frame    = _runner!.Frame!;
            int rowCount = snapshot != null ? snapshot.States.Length : frame.ActionCount;

            // 自适应高度：ScrollView 占据剩余空间
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

            // 表头
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Idx",   GUILayout.Width(30));
                GUILayout.Label("TypeId",GUILayout.Width(160));
                GUILayout.Label("Phase", GUILayout.Width(100));
                GUILayout.Label("Ticks", GUILayout.Width(44));
                if (viewMode == StateViewMode.History)
                    GUILayout.Label("变化", GUILayout.Width(120));
            }

            // 峰值数据引用（Live + Summary 模式都需要——Live 模式用于"水位线"防退回）
            bool usePeak = viewMode != StateViewMode.History;
            var peakPhases = usePeak ? _debugCtrl?.PeakPhases : null;
            var peakTicks  = usePeak ? _debugCtrl?.PeakTicks  : null;

            for (int i = 0; i < rowCount; i++)
            {
                string typeId;
                ActionPhase phase;
                int    ticks;

                if (viewMode == StateViewMode.Summary && peakPhases != null && i < peakPhases.Length)
                {
                    // 完成汇总：始终显示峰值状态
                    typeId = frame.GetTypeId(i);
                    phase  = peakPhases[i];
                    ticks  = peakTicks != null && i < peakTicks.Length ? peakTicks[i] : 0;
                }
                else if (snapshot != null)
                {
                    typeId = i < frame.Actions.Length ? frame.Actions[i].TypeId : $"#{i}";
                    phase  = snapshot.States[i].Phase;
                    ticks  = snapshot.States[i].TicksInPhase;
                }
                else
                {
                    // Live 模式："水位线"逻辑
                    // 当前帧状态为 Idle（被 RecycleCompleted 回收），但峰值记录更高时，
                    // 显示峰值状态而非 Idle，避免已完成节点在 UI 上"闪回"
                    typeId = frame.GetTypeId(i);
                    phase  = frame.States[i].Phase;
                    ticks  = frame.States[i].TicksInPhase;

                    if (phase == ActionPhase.Idle
                        && peakPhases != null && i < peakPhases.Length
                        && peakPhases[i] != ActionPhase.Idle)
                    {
                        phase = peakPhases[i];
                        ticks = peakTicks != null && i < peakTicks.Length ? peakTicks[i] : 0;
                    }
                }

                // History 模式下查找 diff
                string diffLabel = "";
                if (viewMode == StateViewMode.History && snapshot != null)
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

                // 交替行背景色（提高可读性）
                if (i % 2 == 1)
                {
                    var bgRect = EditorGUILayout.GetControlRect(false, 0);
                    bgRect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.DrawRect(bgRect, new Color(0f, 0f, 0f, 0.06f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(i.ToString(), GUILayout.Width(30));
                    GUILayout.Label(typeId, GUILayout.Width(160));

                    // Phase 带颜色标签
                    var prevColor = GUI.contentColor;
                    GUI.contentColor = GetPhaseColor(phase);
                    GUILayout.Label(phase.ToString(), GUILayout.Width(100));
                    GUI.contentColor = prevColor;

                    GUILayout.Label(ticks.ToString(), GUILayout.Width(44));

                    if (diffLabel != "")
                    {
                        var savedColor = GUI.contentColor;
                        GUI.contentColor = new Color(1f, 0.85f, 0.2f);
                        GUILayout.Label(diffLabel, GUILayout.Width(120));
                        GUI.contentColor = savedColor;
                    }

                    // Live 模式下，对 Trigger.* 触发节点提供强制完成按钮
                    if (viewMode == StateViewMode.Live &&
                        (phase == ActionPhase.Running || phase == ActionPhase.Listening) &&
                        typeId.StartsWith("Trigger.", System.StringComparison.Ordinal))
                    {
                        var prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
                        if (GUILayout.Button("触发", GUILayout.Width(48)))
                        {
                            var curTicks = _runner!.Frame!.States[i].TicksInPhase;
                            var actionId = _runner.Frame.Actions[i].Id;
                            var manualTick = _runner.Frame.TickCount;
                            UnityEngine.Debug.Log(
                                $"[BlueprintTestWindow] 手动触发节点 {typeId} (index={i}, actionId={actionId}, tick={manualTick}) " +
                                "原因=运行时测试窗口中点击“触发”按钮");
                            _runner.Frame.EmitOutEvent(i);
                            _runner.Frame.States[i].Phase = ActionPhase.Completed;
                            // 手动触发发生在 Tick 之外，需要立即同步峰值追踪器
                            // 否则下一帧 RecycleCompleted 会将 Completed 重置为 Idle，峰值追踪器永远看不到
                            _debugCtrl?.NotifyManualPhaseChange(i, ActionPhase.Completed, curTicks);
                            Repaint();
                        }
                        GUI.backgroundColor = prevBg;
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

            // ── 时间轴控制栏 ──
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool isLive = _inspectTick < 0;

                // 实时按钮
                GUI.color = isLive && !_runner!.IsCompleted ? new Color(0.5f, 1f, 0.5f) : Color.white;
                if (GUILayout.Button("实时", EditorStyles.toolbarButton, GUILayout.Width(36)))
                    _inspectTick = -1;
                GUI.color = Color.white;

                // 汇总按钮（蓝图完成后可用）
                bool isSummary = isLive && _runner!.IsCompleted;
                GUI.color = isSummary ? new Color(0.3f, 0.9f, 0.3f) : Color.white;
                GUI.enabled = _runner!.IsCompleted;
                if (GUILayout.Button("汇总", EditorStyles.toolbarButton, GUILayout.Width(36)))
                    _inspectTick = -1; // 回到 live 模式，IsCompleted 决定显示汇总
                GUI.enabled = true;
                GUI.color = Color.white;

                // 时间轴滑块
                int oldest = h.OldestTick >= 0 ? h.OldestTick : 0;
                int latest = h.LatestTick >= 0 ? h.LatestTick : 0;

                GUI.enabled = h.Count >= 2;
                int displayTick = isLive ? latest : _inspectTick;
                int newTick = EditorGUILayout.IntSlider(displayTick, oldest, latest);
                if (newTick != displayTick)
                    _inspectTick = newTick;
                GUI.enabled = true;

                // Tick 标签
                string tickLabel = isSummary ? $"T={latest} (汇总)"
                    : isLive ? $"T={latest} (实时)"
                    : $"T={_inspectTick}";
                GUILayout.Label(tickLabel, GUILayout.Width(90));
                GUILayout.Label($"{h.Count}/{h.Capacity}", GUILayout.Width(60));
            }

            // ── 确定视图模式 ──
            bool showingLive = _inspectTick < 0;
            bool showSummary = showingLive && _runner!.IsCompleted;
            BlueprintFrameSnapshot? snap = showingLive ? null : h.GetByTick(_inspectTick);

            StateViewMode viewMode = showSummary ? StateViewMode.Summary
                : snap != null ? StateViewMode.History
                : StateViewMode.Live;

            // ── 视图标题 ──
            string viewTitle = viewMode switch
            {
                StateViewMode.Summary => "Action 状态（完成汇总 — 峰值）",
                StateViewMode.History => $"Action 状态 @ T={_inspectTick}",
                _                     => "Action 状态（实时）"
            };
            EditorGUILayout.LabelField(viewTitle, EditorStyles.boldLabel);

            // ── Action 状态表（弹性高度）──
            DrawActionStates(snap, viewMode);

            // ── 底部折叠面板 ──

            // 分隔线
            var sepRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(sepRect, new Color(0.3f, 0.3f, 0.3f));

            // Phase 变化 & 事件（仅历史模式）
            if (snap != null)
            {
                _showEventsPanel = EditorGUILayout.Foldout(_showEventsPanel,
                    $"Phase 变化 & 排队事件 ({snap.Diffs.Length} diffs, {snap.PendingEventsSnapshot.Length} events)",
                    true);
                if (_showEventsPanel)
                {
                    _diffScrollPos = EditorGUILayout.BeginScrollView(_diffScrollPos,
                        GUILayout.MinHeight(40), GUILayout.MaxHeight(140));

                    foreach (var d in snap.Diffs)
                    {
                        string tid = d.ActionIndex < _runner!.Frame!.Actions.Length
                            ? _runner.Frame.Actions[d.ActionIndex].TypeId
                            : $"#{d.ActionIndex}";
                        var prevC = GUI.contentColor;
                        GUI.contentColor = GetPhaseColor(d.PhaseAfter);
                        EditorGUILayout.LabelField(
                            $"  [{d.ActionIndex}] {tid}: {d.PhaseBefore} → {d.PhaseAfter}");
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
                            $"  事件: [{ev.FromActionIndex}]:{ev.DebugFromPortId ?? ev.FromPortHash.ToString()} → [{ev.ToActionIndex}]:{ev.DebugToPortId ?? ev.ToPortHash.ToString()}");
                        GUI.contentColor = prevC;
                    }

                    if (snap.PendingEventsSnapshot.Length == 0)
                        EditorGUILayout.LabelField("  （无排队事件）");

                    EditorGUILayout.EndScrollView();
                }
            }

            // Blackboard 变量
            if (snap != null)
                DrawBlackboardEntries(snap.BlackboardEntries);
            else
                DrawBlackboardLive();

            DrawRuntimeStatePanel(viewMode);
        }

        private void DrawBlackboardEntries(BlackboardEntry[] entries)
        {
            _showBlackboard = EditorGUILayout.Foldout(_showBlackboard,
                $"Blackboard 变量 ({entries.Length})", true);
            if (!_showBlackboard) return;

            if (entries.Length == 0)
            {
                EditorGUILayout.LabelField("  （无声明变量）");
                return;
            }

            _bbScrollPos = EditorGUILayout.BeginScrollView(_bbScrollPos,
                GUILayout.MinHeight(30), GUILayout.MaxHeight(100));
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
            _showBlackboard = EditorGUILayout.Foldout(_showBlackboard,
                $"Blackboard 变量 ({declared.Count})", true);
            if (!_showBlackboard) return;

            if (declared.Count == 0)
            {
                EditorGUILayout.LabelField("  （无声明变量）");
                return;
            }

            _bbScrollPos = EditorGUILayout.BeginScrollView(_bbScrollPos,
                GUILayout.MinHeight(30), GUILayout.MaxHeight(100));
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
            _runtimeStateHistory.Clear();
            _runtimeSnapshots.Clear();
            _runtimeCoverageWarnings.Clear();
            _selectedRuntimeStateKey = string.Empty;
            _selectedRuntimeSnapshotId = string.Empty;
            _selectedRuntimeSnapshotEntryKey = string.Empty;

            var runner = BlueprintTestRunnerBootstrap.CreateRunner(
                log: msg => UnityEngine.Debug.Log(msg),
                logWarning: msg => UnityEngine.Debug.LogWarning(msg));
            runner.Log = msg => UnityEngine.Debug.Log(msg);
            runner.LogWarning = msg => UnityEngine.Debug.LogWarning(msg);
            runner.LogError = msg => UnityEngine.Debug.LogError(msg);
            runner.DebugController = _debugCtrl;
            _debugCtrl.OnTickRecorded += _ => CaptureRuntimeStateHistoryFrame(runner);

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

            ValidateRuntimeCoverage(_runner);

            CaptureRuntimeStateHistoryFrame(_runner);

            TryInvokeRuntimeSmokeProbes(_runner, _jsonAsset);

            UnityEngine.Debug.Log("══════════════════════════════════════════");
            UnityEngine.Debug.Log("  蓝图运行时测试 - 开始执行");
            UnityEngine.Debug.Log("══════════════════════════════════════════");

            RebuildSceneVisualization();

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

            ValidateRuntimeCoverage(_runner);

            CaptureRuntimeStateHistoryFrame(_runner);

            TryInvokeRuntimeSmokeProbes(_runner, _jsonAsset);

            _statusText = $"已加载: {_runner.Frame.BlueprintName} " +
                          $"({_runner.Frame.ActionCount} Actions, " +
                          $"{_runner.Frame.Transitions.Length} Transitions)";

            RebuildSceneVisualization();
            Repaint();
        }

        private void StepTick()
        {
            if (_runner?.Frame == null || _runner.IsCompleted) return;

            if (Settings.EnableTickBoundaryLogs)
            {
                UnityEngine.Debug.Log($"────── Tick {_runner.TickCount + 1} ──────");
            }
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
            _runtimeStateHistory.Clear();
            _runtimeSnapshots.Clear();
            _runtimeCoverageWarnings.Clear();
            _selectedRuntimeStateKey = string.Empty;
            _selectedRuntimeSnapshotId = string.Empty;
            _selectedRuntimeSnapshotEntryKey = string.Empty;
            _vizAreas.Clear();
            _vizPoints.Clear();
            SceneView.RepaintAll();
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
            SceneView.duringSceneGui -= OnSceneGUI;
            _runner?.Shutdown();
            _runtimeStateHistory.Clear();
            _runtimeSnapshots.Clear();
            _runtimeCoverageWarnings.Clear();
            _vizAreas.Clear();
            _vizPoints.Clear();
        }

        private void ValidateRuntimeCoverage(BlueprintRunner runner)
        {
            _runtimeCoverageWarnings.Clear();
            var warnings = BlueprintTestRunnerBootstrap.AnalyzeCoverage(runner, runner.Frame);
            for (var index = 0; index < warnings.Count; index++)
            {
                var warning = warnings[index];
                _runtimeCoverageWarnings.Add(warning);
                UnityEngine.Debug.LogWarning(warning);
            }
        }

        private static void TryInvokeRuntimeSmokeProbes(BlueprintRunner runner, TextAsset jsonAsset)
        {
            if (runner == null || jsonAsset == null)
            {
                return;
            }

            try
            {
                BlueprintRuntimeSmokeRegistry.TryRunMatching(runner, jsonAsset);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[BlueprintTestWindow] 调用 RuntimeSmokeProbe 异常: {ex}");
            }
        }

        // ══════════════════════════════════════════
        //  SceneView 可视化 — 空间数据解析
        // ══════════════════════════════════════════

        /// <summary>从已加载的 Frame 解析所有空间数据用于 SceneView 绘制</summary>
        private void RebuildSceneVisualization()
        {
            _vizAreas.Clear();
            _vizPoints.Clear();

            if (_runner?.Frame == null) return;

            var frame = _runner.Frame;
            for (int i = 0; i < frame.ActionCount; i++)
            {
                var typeId   = frame.GetTypeId(i);
                var bindings = frame.GetSceneBindings(i);
                foreach (var binding in bindings)
                {
                    if (string.IsNullOrEmpty(binding.SpatialPayloadJson)) continue;

                    if (binding.BindingType == "Area")
                        TryParseArea(typeId, binding.SpatialPayloadJson);
                    else if (binding.BindingType == "Transform")
                        TryParsePoint(typeId, binding.SpatialPayloadJson);
                }
            }

            UnityEngine.Debug.Log($"[BlueprintTestWindow] SceneView 可视化：{_vizAreas.Count} 个区域，{_vizPoints.Count} 个点位");
            SceneView.RepaintAll();
        }

        // ── JSON 反序列化辅助类（与导出格式对齐）──

        [System.Serializable]
        private class VizAreaPayloadJson
        {
            public string    type   = "Polygon";
            public Vector3[] floorVertices = System.Array.Empty<Vector3>();
            public Vector3   center;
            public float     radius;
            public Vector3   pointA;
            public Vector3   pointB;
            public float     height = 3f;
        }

        [System.Serializable]
        private class VizPointPayloadJson
        {
            public VizVec3 position = new();
            public VizVec3 rotation = new();
        }

        [System.Serializable]
        private class VizVec3
        {
            public float x, y, z;
            public Vector3 ToVector3() => new(x, y, z);
        }

        private void TryParseArea(string typeId, string json)
        {
            VizAreaPayloadJson? raw;
            try { raw = JsonUtility.FromJson<VizAreaPayloadJson>(json); }
            catch { return; }
            if (raw == null) return;

            var label = typeId.Contains('.') ? typeId.Substring(typeId.LastIndexOf('.') + 1) : typeId;
            var color = GetVizAreaColor(typeId);
            var shape = raw.type switch
            {
                "Circle"  => VizShapeType.Circle,
                "Capsule" => VizShapeType.Capsule,
                _         => VizShapeType.Polygon
            };

            var entry = new SceneVizAreaEntry
            {
                Label = label, Color = color, ShapeType = shape,
                Height = Mathf.Max(raw.height, 0.5f),
            };

            switch (shape)
            {
                case VizShapeType.Circle:
                    entry.Center = raw.center;
                    entry.Radius = raw.radius;
                    break;
                case VizShapeType.Capsule:
                    entry.PointA = raw.pointA;
                    entry.PointB = raw.pointB;
                    entry.Radius = raw.radius;
                    entry.Center = (raw.pointA + raw.pointB) * 0.5f;
                    break;
                default:
                    entry.FloorVertices = raw.floorVertices;
                    if (entry.FloorVertices != null && entry.FloorVertices.Length > 0)
                    {
                        var sum = Vector3.zero;
                        for (int j = 0; j < entry.FloorVertices.Length; j++) sum += entry.FloorVertices[j];
                        entry.Center = sum / entry.FloorVertices.Length;
                    }
                    break;
            }

            _vizAreas.Add(entry);
        }

        private void TryParsePoint(string typeId, string json)
        {
            VizPointPayloadJson? raw;
            try { raw = JsonUtility.FromJson<VizPointPayloadJson>(json); }
            catch { return; }
            if (raw == null) return;

            _vizPoints.Add(new SceneVizPointEntry
            {
                Label    = typeId.Contains('.') ? typeId.Substring(typeId.LastIndexOf('.') + 1) : typeId,
                Position = raw.position.ToVector3(),
                Rotation = Quaternion.Euler(raw.rotation.ToVector3())
            });
        }

        private static Color GetVizAreaColor(string typeId)
        {
            if (typeId.StartsWith("Trigger."))     return VizColorTrigger;
            if (typeId.StartsWith("Spawn.Wave"))   return VizColorWave;
            if (typeId.StartsWith("Spawn.Preset")) return VizColorPreset;
            return VizColorDefault;
        }

        // ══════════════════════════════════════════
        //  SceneView 可视化 — 绘制
        // ══════════════════════════════════════════

        private void OnSceneGUI(SceneView sv)
        {
            if (!_showSceneViz) return;
            if (_vizAreas.Count == 0 && _vizPoints.Count == 0) return;

            foreach (var e in _vizAreas)
            {
                switch (e.ShapeType)
                {
                    case VizShapeType.Polygon:  DrawVizPolygon(e);  break;
                    case VizShapeType.Circle:   DrawVizCircle(e);   break;
                    case VizShapeType.Capsule:  DrawVizCapsule(e);  break;
                }
                // 标签
                DrawVizLabel(e.Center + Vector3.up * (e.Height + 0.5f), e.Label, e.Color);
            }

            foreach (var e in _vizPoints)
            {
                DrawVizPoint(e);
                DrawVizLabel(e.Position + Vector3.up * 1.8f, e.Label, VizColorPoint);
            }
        }

        // ── Polygon ──────────────────────────────

        private static void DrawVizPolygon(SceneVizAreaEntry e)
        {
            var verts = e.FloorVertices;
            if (verts == null || verts.Length < 3) return;

            Handles.color = e.Color;
            // 底面
            for (int i = 0; i < verts.Length; i++)
                Handles.DrawLine(verts[i], verts[(i + 1) % verts.Length]);
            // 顶面
            for (int i = 0; i < verts.Length; i++)
                Handles.DrawLine(verts[i] + Vector3.up * e.Height, verts[(i + 1) % verts.Length] + Vector3.up * e.Height);
            // 竖边
            for (int i = 0; i < verts.Length; i++)
                Handles.DrawLine(verts[i], verts[i] + Vector3.up * e.Height);

            // 半透明底面对角线（填充近似）
            Handles.color = new Color(e.Color.r, e.Color.g, e.Color.b, 0.08f);
            for (int i = 1; i < verts.Length - 1; i++)
            {
                Handles.DrawLine(verts[0], verts[i]);
                Handles.DrawLine(verts[0], verts[i + 1]);
            }
        }

        // ── Circle ──────────────────────────────

        private static void DrawVizCircle(SceneVizAreaEntry e)
        {
            Handles.color = e.Color;
            // 底面 + 顶面
            Handles.DrawWireDisc(e.Center, Vector3.up, e.Radius);
            Handles.DrawWireDisc(e.Center + Vector3.up * e.Height, Vector3.up, e.Radius);
            // 4 条竖边
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                var off = new Vector3(Mathf.Cos(a) * e.Radius, 0, Mathf.Sin(a) * e.Radius);
                Handles.DrawLine(e.Center + off, e.Center + off + Vector3.up * e.Height);
            }
            // 半透明底面
            Handles.color = new Color(e.Color.r, e.Color.g, e.Color.b, 0.06f);
            Handles.DrawSolidDisc(e.Center + Vector3.up * 0.01f, Vector3.up, e.Radius);
        }

        // ── Capsule ──────────────────────────────

        private static void DrawVizCapsule(SceneVizAreaEntry e)
        {
            Handles.color = e.Color;

            var axis = e.PointB - e.PointA;
            var axisDir = axis.magnitude > 0.001f ? axis.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, axisDir);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right = right.normalized;

            // 底面 + 顶面胶囊轮廓
            DrawVizCapsuleOutline(e.PointA, e.PointB, e.Radius, axisDir, right, 0f);
            DrawVizCapsuleOutline(e.PointA, e.PointB, e.Radius, axisDir, right, e.Height);

            // 竖边
            var perpR = right * e.Radius;
            Handles.DrawLine(e.PointA + perpR, e.PointA + perpR + Vector3.up * e.Height);
            Handles.DrawLine(e.PointA - perpR, e.PointA - perpR + Vector3.up * e.Height);
            Handles.DrawLine(e.PointB + perpR, e.PointB + perpR + Vector3.up * e.Height);
            Handles.DrawLine(e.PointB - perpR, e.PointB - perpR + Vector3.up * e.Height);

            // 轴线
            Handles.color = new Color(e.Color.r, e.Color.g, e.Color.b, 0.3f);
            Handles.DrawDottedLine(e.PointA, e.PointB, 3f);
            Handles.DrawDottedLine(e.PointA + Vector3.up * e.Height, e.PointB + Vector3.up * e.Height, 3f);
        }

        private static void DrawVizCapsuleOutline(Vector3 pA, Vector3 pB, float radius, Vector3 axisDir, Vector3 right, float yOff)
        {
            var up = Vector3.up * yOff;
            var a = pA + up;
            var b = pB + up;

            // 两条平行线
            Handles.DrawLine(a + right * radius, b + right * radius);
            Handles.DrawLine(a - right * radius, b - right * radius);

            // A 端半圆（朝 -axisDir）
            DrawVizHalfCircle(a, radius, axisDir, right, false);
            // B 端半圆（朝 +axisDir）
            DrawVizHalfCircle(b, radius, axisDir, right, true);
        }

        private static void DrawVizHalfCircle(Vector3 center, float radius, Vector3 axisDir, Vector3 right, bool forward)
        {
            int seg = CircleSeg / 2;
            float startA = forward ? -90f : 90f;
            float endA   = forward ?  90f : 270f;

            var prevP = center + (Mathf.Cos(startA * Mathf.Deg2Rad) * right +
                                  Mathf.Sin(startA * Mathf.Deg2Rad) * axisDir) * radius;
            for (int i = 1; i <= seg; i++)
            {
                float t = (float)i / seg;
                float ang = Mathf.Lerp(startA, endA, t) * Mathf.Deg2Rad;
                var nextP = center + (Mathf.Cos(ang) * right + Mathf.Sin(ang) * axisDir) * radius;
                Handles.DrawLine(prevP, nextP);
                prevP = nextP;
            }
        }

        // ── Point（预设刷怪点）────────────────────

        private static void DrawVizPoint(SceneVizPointEntry e)
        {
            var cubeCenter = e.Position + Vector3.up * 0.5f;

            // Cube 线框
            Handles.color = VizColorPoint;
            var mx = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(cubeCenter, e.Rotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, Vector3.one);
            Handles.matrix = mx;

            // 半透明填充（用 cube 代替）
            Handles.color = new Color(VizColorPoint.r, VizColorPoint.g, VizColorPoint.b, 0.15f);
            Handles.matrix = Matrix4x4.TRS(cubeCenter, e.Rotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, Vector3.one * 0.98f);
            Handles.matrix = mx;

            // 朝向箭头
            Handles.color = VizColorPoint;
            var fwd = e.Rotation * Vector3.forward;
            var rgt = e.Rotation * Vector3.right;
            var tip = cubeCenter + fwd * 1.5f;
            Handles.DrawLine(cubeCenter, tip);
            Handles.DrawLine(tip, tip - fwd * 0.35f + rgt * 0.18f);
            Handles.DrawLine(tip, tip - fwd * 0.35f - rgt * 0.18f);
        }

        // ── 标签 ─────────────────────────────────

        private static void DrawVizLabel(Vector3 worldPos, string text, Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = color }
            };
            Handles.Label(worldPos, text, style);
        }
    }
}
