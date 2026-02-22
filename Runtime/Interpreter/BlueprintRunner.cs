#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Interpreter.Diagnostics;

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
    /// var runner = new BlueprintRunner();
    /// runner.RegisterSystem(new TransitionSystem());
    /// runner.RegisterSystem(new FlowSystem());
    /// runner.RegisterSystem(new SpawnPresetSystem());
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
    public class BlueprintRunner
    {
        private readonly List<BlueprintSystemBase> _systems = new();
        private bool _systemsSorted;
        private bool _initialized;

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

            // 3. 排序 System 并初始化
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

            if (Frame.StartActionIndex >= 0)
            {
                Frame.States[Frame.StartActionIndex].Phase = ActionPhase.Running;
            }

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

            // 调试暂停：跳过模拟，保持 Frame 状态冻结
            if (DebugController?.IsPaused == true)
                return;

            // 按顺序执行所有 System
            for (int i = 0; i < _systems.Count; i++)
            {
                var sys = _systems[i];
                if (sys.Enabled)
                {
                    sys.Update(Frame);
                }
            }

            // 递增 Tick 计数
            Frame.TickCount++;

            // 更新所有 Running Action 的 TicksInPhase
            for (int i = 0; i < Frame.States.Length; i++)
            {
                if (Frame.States[i].Phase == ActionPhase.Running)
                {
                    Frame.States[i].TicksInPhase++;
                }
            }

            // 调试快照：tick 末记录历史（在完成检测之前，确保最后一帧也被记录）
            DebugController?.OnTickCompleted(Frame);

            // 检查完成条件：没有活跃 Action 且没有待处理事件
            if (!Frame.HasActiveActions() && Frame.PendingEvents.Count == 0)
            {
                Frame.IsCompleted = true;
                Log?.Invoke($"[BlueprintRunner] 蓝图执行完毕 (Tick={Frame.TickCount})");
            }

            // 蓝图结束时，将所有 Listening 节点统一清理为 Completed
            // 语义：蓝图结束 → 所有监听器下线，不会再有新事件到达
            if (Frame.IsCompleted)
            {
                for (int i = 0; i < Frame.States.Length; i++)
                {
                    if (Frame.States[i].Phase == ActionPhase.Listening)
                    {
                        Frame.States[i].Phase = ActionPhase.Completed;
                    }
                }
            }
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
            Log?.Invoke("[BlueprintRunner] 已关闭");
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
    }
}
