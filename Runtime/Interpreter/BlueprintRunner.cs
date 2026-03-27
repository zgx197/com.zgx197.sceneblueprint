#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime;
using SceneBlueprint.Runtime.Interpreter.Adapters;
using SceneBlueprint.Runtime.Interpreter.Diagnostics;
using SceneBlueprint.Runtime.Interpreter.Systems;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图运行器——驱动 System 执行的调度中枢。
    /// <para>
    /// 对齐 FrameSyncEngine.FSGame 的角色：
    /// - 持有 BlueprintFrame（世界状态）和 System 列表
    /// - 提供 Tick() 方法驱动每帧模拟
    /// - 管理 System 的注册、排序和生命周期
    /// </para>
    /// <para>
    /// 使用方式（伪代码）：
    /// <code>
    /// var runner = BlueprintRunnerFactory.CreateDefault();
    /// runner.GetSystem&lt;CameraShakeSystem&gt;()?.ShakeHandler = myHandler;
    /// runner.Load(blueprintJsonText);
    /// while (!runner.IsCompleted) runner.Tick();
    /// </code>
    /// </para>
    /// <para>
    /// 后续迁移路径：
    /// BlueprintRunner 的职责迁移到 FrameSyncEngine.FSGame.OnSimulate() 中，
    /// System 注册迁移到 SystemSetup.CreateSystems()。
    /// </para>
    /// </summary>
    public partial class BlueprintRunner
    {
        private readonly List<BlueprintSystemBase> _systems = new();
        private bool _systemsSorted;
        private bool _initialized;
        private ISignalBus? _signalBus;

        /// <summary>帧适配器——桥接 BlueprintFrame 与 FrameView</summary>
        private readonly ClassFrameAdapter _adapter = new();

        /// <summary>当前 Frame（世界状态）</summary>
        public BlueprintFrame? Frame { get; private set; }

        /// <summary>蓝图是否已执行完毕</summary>
        public bool IsCompleted => Frame?.IsCompleted ?? false;

        /// <summary>当前 Tick 数</summary>
        public int TickCount => Frame?.TickCount ?? 0;

        /// <summary>
        /// 调试控制器（可选）。设置后自动记录帧快照，支持暂停/步进检视。
        /// <code>runner.DebugController = new BlueprintDebugController();</code>
        /// </summary>
        public BlueprintDebugController? DebugController { get; set; }

        /// <summary>日志回调（外部注入，用于调试输出）</summary>
        public Action<string>? Log { get; set; }

        /// <summary>警告回调</summary>
        public Action<string>? LogWarning { get; set; }

        /// <summary>错误回调</summary>
        public Action<string>? LogError { get; set; }

        // ══════════════════════════════════════════
        //  System 注册
        // ══════════════════════════════════════════

        /// <summary>
        /// 注册一个 System。
        /// <para>必须在 Load() 之前调用。</para>
        /// </summary>
        public void RegisterSystem(BlueprintSystemBase system)
        {
            if (_initialized)
            {
                LogWarning?.Invoke($"[BlueprintRunner] 已初始化后注册 System '{system.Name}'，将在下次 Load 时生效");
            }
            _systems.Add(system);
            _systemsSorted = false;
        }

        /// <summary>注册多个 System</summary>
        public void RegisterSystems(params BlueprintSystemBase[] systems)
        {
            foreach (var sys in systems) RegisterSystem(sys);
        }

        /// <summary>按类型查找已注册的 System（用于注入 Handler 等可选依赖）</summary>
        public T? GetSystem<T>() where T : BlueprintSystemBase
        {
            foreach (var sys in _systems)
                if (sys is T match) return match;
            return null;
        }

        /// <summary>返回当前 Runner 已注册 System 的名称快照，供 Editor 诊断与测试使用。</summary>
        public IReadOnlyList<string> GetRegisteredSystemNames()
        {
            var names = new List<string>(_systems.Count);
            for (var index = 0; index < _systems.Count; index++)
            {
                names.Add(_systems[index].Name);
            }

            return new ReadOnlyCollection<string>(names);
        }

        // ══════════════════════════════════════════
        //  加载与初始化
        // ══════════════════════════════════════════

        /// <summary>
        /// 从 JSON 文本加载蓝图并初始化运行环境。
        /// <para>
        /// 流程：
        /// 1. 解析 JSON → SceneBlueprintData
        /// 2. 构建 BlueprintFrame（静态数据 + 索引表）
        /// 3. 初始化动态状态（所有 Action 设为 Idle，Flow.Start 设为 Running）
        /// 4. 按 Order 排序 System，依次调用 OnInit(frame)
        /// </para>
        /// </summary>
        public void Load(string jsonText)
        {
            // 1. 解析 + 构建 Frame
            Frame = BlueprintLoader.Load(jsonText);
            if (Frame == null)
            {
                LogError?.Invoke("[BlueprintRunner] 加载蓝图失败：BlueprintLoader 返回 null");
                return;
            }

            Frame.Runner = this;

            Log?.Invoke($"[BlueprintRunner] 蓝图已加载: {Frame.BlueprintId} ({Frame.BlueprintName}), " +
                        $"Actions={Frame.ActionCount}, Transitions={Frame.Transitions.Length}");

            // 2. 初始化起始节点
            if (Frame.StartActionIndex >= 0)
            {
                Frame.States[Frame.StartActionIndex].Phase = ActionPhase.Running;
                Log?.Invoke($"[BlueprintRunner] Flow.Start (index={Frame.StartActionIndex}) → Running");
            }
            else
            {
                LogWarning?.Invoke("[BlueprintRunner] 蓝图中未找到 Flow.Start 节点");
            }

            // 3. 设置适配器
            _adapter.SetFrame(Frame);
            InjectFrameReferences(Frame);

            // 4. 排序 System 并初始化
            EnsureSystemsSorted();
            foreach (var sys in _systems)
            {
                if (sys.Enabled)
                {
                    sys.OnInit(Frame);
                    Log?.Invoke($"[BlueprintRunner] System 已初始化: {sys.Name}");
                }
            }

            _initialized = true;
        }

        /// <summary>
        /// 从已解析的数据对象加载（跳过 JSON 解析步骤，用于测试）。
        /// </summary>
        public void Load(SceneBlueprintData data)
        {
            Frame = BlueprintLoader.BuildFrame(data);
            if (Frame == null)
            {
                LogError?.Invoke("[BlueprintRunner] 加载蓝图失败：BuildFrame 返回 null");
                return;
            }

            Frame.Runner = this;

            if (Frame.StartActionIndex >= 0)
            {
                Frame.States[Frame.StartActionIndex].Phase = ActionPhase.Running;
            }

            _adapter.SetFrame(Frame);
            InjectFrameReferences(Frame);

            EnsureSystemsSorted();
            foreach (var sys in _systems)
            {
                if (sys.Enabled) sys.OnInit(Frame);
            }

            _initialized = true;
        }

        // ══════════════════════════════════════════
        //  Tick 循环
        // ══════════════════════════════════════════

        /// <summary>
        /// 执行一次 Tick（一帧模拟）。
        /// <para>
        /// 对齐 FrameSyncEngine.FSGame.OnSimulate(Frame) 的流程：
        /// 1. 检查是否已完成
        /// 2. 按 Order 顺序调用每个 System 的 Update(frame)
        /// 3. TickCount++
        /// 4. 检查完成条件
        /// </para>
        /// </summary>
        public void Tick()
        {
            if (Frame == null || Frame.IsCompleted)
                return;

            if (DebugController?.IsPaused == true)
                return;

            // 1. 创建 FrameView（零拷贝，States 直接引用 Frame.States）
            var view = _adapter.BeginTick(Frame);

            // 2. 按顺序执行所有 System（通过 FrameView 统一读写）
            for (int i = 0; i < _systems.Count; i++)
            {
                var sys = _systems[i];
                if (sys.Enabled)
                {
                    sys.Update(ref view);
                }
            }

            // 3. 帧结束回写
            _adapter.EndTick(Frame, ref view);

            // 4. 递增 Tick 计数
            Frame.TickCount++;

            // 5. 更新所有 Running Action 的 TicksInPhase
            for (int i = 0; i < Frame.States.Length; i++)
            {
                if (Frame.States[i].Phase == ActionPhase.Running)
                {
                    Frame.States[i].TicksInPhase++;
                }
            }

            // 6. 死锁检测（必须在终态清理之前，否则清理会掩盖真实死锁）
            for (int i = 0; i < Frame.States.Length; i++)
            {
                if (Frame.States[i].Phase == ActionPhase.Completed && !Frame.States[i].EventEmitted)
                {
                    var typeId = Frame.Actions[i].TypeId;
                    UnityEngine.Debug.LogWarning(
                        $"[BlueprintRunner] 死锁警告: 节点 {typeId} (index={i}) " +
                        $"已 Completed 但未发射端口事件！");
                }
            }

            // 7. 完成条件检查
            // 路径 A: FlowSystem 在 Update 中已设置 Frame.IsCompleted = true（Flow.End 到达）
            // 路径 B: 无活跃 Action（Running/WaitingTrigger）且无待消费事件
            if (!Frame.IsCompleted && !Frame.HasActiveActions() && view.PendingEvents.Count == 0)
            {
                Frame.IsCompleted = true;
            }

            if (Frame.IsCompleted)
            {
                Log?.Invoke($"[BlueprintRunner] 蓝图执行完毕 (Tick={Frame.TickCount})");

                // 8. 终态清理：蓝图结束后，将所有仍处于活跃/中间状态的节点统一设为 Completed。
                // 这些节点在蓝图生命周期内不会自行进入 Completed（如 Flow.Filter 始终在
                // Running ↔ Listening 循环），但蓝图结束意味着它们的使命已完成。
                var cleanedNodes = new List<string>();
                for (int i = 0; i < Frame.States.Length; i++)
                {
                    var phase = Frame.States[i].Phase;
                    if (phase == ActionPhase.Running ||
                        phase == ActionPhase.WaitingTrigger ||
                        phase == ActionPhase.Listening)
                    {
                        cleanedNodes.Add(FormatActionState(Frame, i, phase.ToString()));
                        Frame.States[i].Phase = ActionPhase.Completed;
                        Frame.States[i].EventEmitted = true;
                    }
                }

                if (BlueprintRuntimeSettings.Instance.EnableCompletionSummaryLogs)
                {
                    Log?.Invoke(BuildCompletionSummary(Frame, cleanedNodes));
                }
            }

            // 9. 调试快照（在终态清理之后，确保快照反映蓝图的最终状态）
            DebugController?.OnTickCompleted(Frame);
        }

        /// <summary>
        /// 连续执行多次 Tick，直到完成或达到最大次数。
        /// 返回实际执行的 Tick 数。
        /// </summary>
        public int RunUntilComplete(int maxTicks = 10000)
        {
            int count = 0;
            while (!IsCompleted && count < maxTicks)
            {
                Tick();
                count++;
            }

            if (count >= maxTicks && !IsCompleted)
            {
                LogWarning?.Invoke($"[BlueprintRunner] 达到最大 Tick 数 {maxTicks}，蓝图尚未完成");
            }

            return count;
        }

        // ══════════════════════════════════════════
        //  清理
        // ══════════════════════════════════════════

        /// <summary>设置信号总线（在 Load 之前调用）</summary>
        public void SetSignalBus(ISignalBus bus)
        {
            _signalBus = bus ?? throw new ArgumentNullException(nameof(bus));
            RegisterService<ISignalBus>(bus);
            if (bus is IBlueprintEventHistorySignalBus historyAwareBus)
            {
                historyAwareBus.EventHistoryRecorder = GetService<IBlueprintEventHistoryRecorder>();
            }

            _adapter.SetSignalBus(bus);
            BlueprintRunnerConfiguratorRegistry.ConfigureDefaultScopes(this);
        }

        public ISignalBus? SignalBus => _signalBus;

        /// <summary>
        /// 信号桥接器——外部系统与蓝图双向通信的统一入口。
        /// <para>
        /// 由 BlueprintRunnerFactory 自动创建并注入，业务层通过此属性访问。
        /// FS 侧不使用 Bridge（直接操作 qtn 队列），此属性可能为 null。
        /// </para>
        /// </summary>
        public ISignalBridge? Bridge { get; private set; }

        /// <summary>设置信号桥接器（在 Load 之前调用，由 Factory 注入）</summary>
        internal void SetSignalBridge(ISignalBridge bridge)
        {
            Bridge = bridge;
            RegisterService<ISignalBridge>(bridge);
            _adapter.SetSignalBridge(bridge as InMemorySignalBridge);
        }

        /// <summary>获取适配器的 Query 实例（用于外部查询）</summary>
        public ClassActionQuery QueryAdapter => _adapter.Query;

        /// <summary>停止执行并清理资源</summary>
        public void Shutdown()
        {
            if (Frame != null)
            {
                foreach (var sys in _systems)
                {
                    if (sys.Enabled) sys.OnDisabled(Frame);
                }
            }

            Frame = null;
            _initialized = false;
            ClearServices();
            Log?.Invoke("[BlueprintRunner] 已关闭");
        }

        /// <summary>为需要 BlueprintFrame 桥接的 System 注入引用（通过 IFrameAware 接口自动发现）</summary>
        private void InjectFrameReferences(BlueprintFrame frame)
        {
            foreach (var sys in _systems)
            {
                if (sys is IFrameAware aware)
                    aware.Frame = frame;
            }
        }

        // ══════════════════════════════════════════
        //  内部方法
        // ══════════════════════════════════════════

        private void EnsureSystemsSorted()
        {
            if (_systemsSorted) return;

            // 按分组分桶：有 [UpdateInGroup] 用分组值，否则直接用 Order 值（兼容旧写法）
            var buckets = new Dictionary<int, List<BlueprintSystemBase>>();
            foreach (var sys in _systems)
            {
                int band = GetBandOrder(sys);
                if (!buckets.TryGetValue(band, out var list))
                    buckets[band] = list = new List<BlueprintSystemBase>();
                list.Add(sys);
            }

            _systems.Clear();
            var sortedBands = new List<int>(buckets.Keys);
            sortedBands.Sort();
            foreach (var band in sortedBands)
            {
                var group = buckets[band];
                // 先按 Order 值做稳定基准，再按 [UpdateAfter] 拓扑排序
#pragma warning disable CS0618
                group.Sort((a, b) => a.Order.CompareTo(b.Order));
#pragma warning restore CS0618
                _systems.AddRange(TopologicalSort(group));
            }

            _systemsSorted = true;
        }

        /// <summary>
        /// 获取 System 的桶编号：有 [UpdateInGroup] 取分组 int 值，否则取 Order（每个 System 独占一个桶）。
        /// </summary>
        private static int GetBandOrder(BlueprintSystemBase sys)
        {
            var attr = sys.GetType().GetCustomAttribute<UpdateInGroupAttribute>();
#pragma warning disable CS0618
            return attr != null ? (int)attr.Group : sys.Order;
#pragma warning restore CS0618
        }

        /// <summary>
        /// Kahn 算法拓扑排序：基于同组内的 [UpdateAfter] 依赖声明。
        /// 若出现循环依赖（设计错误），剩余节点按原顺序追加。
        /// </summary>
        private static List<BlueprintSystemBase> TopologicalSort(List<BlueprintSystemBase> systems)
        {
            if (systems.Count <= 1) return systems;

            var typeMap = new Dictionary<Type, BlueprintSystemBase>(systems.Count);
            foreach (var s in systems) typeMap[s.GetType()] = s;

            // graph[dep] = 依赖 dep 的 System 列表（dep 必须先于它们执行）
            var graph    = new Dictionary<BlueprintSystemBase, List<BlueprintSystemBase>>(systems.Count);
            var inDegree = new Dictionary<BlueprintSystemBase, int>(systems.Count);
            foreach (var s in systems) { graph[s] = new List<BlueprintSystemBase>(); inDegree[s] = 0; }

            foreach (var s in systems)
            {
                foreach (UpdateAfterAttribute attr in s.GetType().GetCustomAttributes<UpdateAfterAttribute>(false))
                {
                    if (typeMap.TryGetValue(attr.SystemType, out var dep))
                    {
                        graph[dep].Add(s);
                        inDegree[s]++;
                    }
                }
            }

            var queue  = new Queue<BlueprintSystemBase>();
            foreach (var s in systems)
                if (inDegree[s] == 0) queue.Enqueue(s);

            var result = new List<BlueprintSystemBase>(systems.Count);
            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                result.Add(s);
                foreach (var dependent in graph[s])
                    if (--inDegree[dependent] == 0) queue.Enqueue(dependent);
            }

            // 循环依赖兜底：将剩余 System 原样追加（不应出现，但避免丢失 System）
            foreach (var s in systems)
                if (!result.Contains(s)) result.Add(s);

            return result;
        }

        private static string BuildCompletionSummary(BlueprintFrame frame, List<string> cleanedNodes)
        {
            var completedCount = 0;
            var idleCount = 0;
            var runningCount = 0;
            var listeningCount = 0;
            var waitingTriggerCount = 0;

            for (var i = 0; i < frame.States.Length; i++)
            {
                var phase = frame.States[i].Phase;
                switch (phase)
                {
                    case ActionPhase.Completed:
                        completedCount++;
                        break;
                    case ActionPhase.Idle:
                        idleCount++;
                        break;
                    case ActionPhase.Running:
                        runningCount++;
                        break;
                    case ActionPhase.Listening:
                        listeningCount++;
                        break;
                    case ActionPhase.WaitingTrigger:
                        waitingTriggerCount++;
                        break;
                }
            }

            var builder = new StringBuilder();
            builder.Append("[BlueprintRunner] 运行摘要: ");
            builder.Append($"blueprint={frame.BlueprintName}, tick={frame.TickCount}, actions={frame.ActionCount}, ");
            builder.Append($"completed={completedCount}, idle={idleCount}, running={runningCount}, ");
            builder.Append($"listening={listeningCount}, waitingTrigger={waitingTriggerCount}, pendingEvents={frame.PendingEvents.Count}");

            if (cleanedNodes.Count > 0)
            {
                builder.AppendLine();
                builder.Append("[BlueprintRunner] 终态清理已收口的节点: ");
                builder.Append(string.Join(" | ", cleanedNodes));
            }

            return builder.ToString();
        }

        private static string FormatActionState(BlueprintFrame frame, int actionIndex, string phaseName)
        {
            var action = frame.Actions[actionIndex];
            var state = frame.States[actionIndex];
            return $"{action.TypeId}[index={actionIndex}, actionId={action.Id}, phase={phaseName}, ticksInPhase={state.TicksInPhase}]";
        }
    }
}
