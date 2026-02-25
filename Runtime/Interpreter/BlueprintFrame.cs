#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图世界状态快照——运行时解释器的核心数据容器。
    /// <para>
    /// 对齐 FrameSyncEngine.Frame 的设计理念：
    /// - Frame 是所有运行时状态的唯一持有者（Single Source of Truth）
    /// - System 通过 Frame 读写状态，自身不持有可变数据
    /// - 静态数据（Actions/Transitions/Properties）加载后不变
    /// - 动态数据（States/Blackboard/Events）每帧由 System 驱动变化
    /// </para>
    /// <para>
    /// 后续迁移路径：
    /// BlueprintFrame 的数据搬到 FrameSyncEngine.Frame 的 Component 上，
    /// 静态数据通过 RuntimeConfig 传入，动态数据通过代码生成的 Component 存储。
    /// </para>
    /// </summary>
    public class BlueprintFrame
    {
        // ══════════════════════════════════════════
        //  静态数据（由 BlueprintLoader 初始化，运行时不变）
        // ══════════════════════════════════════════

        /// <summary>蓝图 ID</summary>
        public string BlueprintId { get; internal set; } = "";

        /// <summary>蓝图名称</summary>
        public string BlueprintName { get; internal set; } = "";

        /// <summary>Action 数量</summary>
        public int ActionCount => Actions.Length;

        /// <summary>
        /// 原始 Action 数据（索引即为 ActionIndex）。
        /// 用于 System 读取节点的 TypeId / Properties / SceneBindings。
        /// </summary>
        public ActionEntry[] Actions { get; internal set; } = Array.Empty<ActionEntry>();

        /// <summary>
        /// 原始 Transition 数据。
        /// TransitionSystem 根据此表进行端口事件路由。
        /// </summary>
        public TransitionEntry[] Transitions { get; internal set; } = Array.Empty<TransitionEntry>();

        // ── 索引表（加速查询）──

        /// <summary>ActionId → ActionIndex 快速查找</summary>
        public Dictionary<string, int> ActionIdToIndex { get; internal set; } = new();

        /// <summary>
        /// 出边索引：ActionIndex → 从该 Action 出发的 Transition 索引列表。
        /// TransitionSystem 用于快速查找"某个 Action 完成后应该触发哪些下游"。
        /// </summary>
        public Dictionary<int, List<int>> OutgoingTransitions { get; internal set; } = new();

        /// <summary>
        /// TypeId → ActionIndex 列表。
        /// 特定 System 用于快速遍历自己需要处理的 Action 子集。
        /// 例如 SpawnPresetSystem 只关心 TypeId == "Spawn.Preset" 的节点。
        /// </summary>
        public Dictionary<string, List<int>> ActionsByTypeId { get; internal set; } = new();

        /// <summary>Flow.Start 节点的 ActionIndex（-1 表示不存在）</summary>
        public int StartActionIndex { get; internal set; } = -1;

        /// <summary>蓝图声明的所有变量（由 BlueprintLoader 初始化）</summary>
        public VariableDeclaration[] Variables { get; internal set; } = Array.Empty<VariableDeclaration>();

        /// <summary>
        /// 数据连接反向索引：(toActionIndex, toPortId) → (fromActionIndex, fromPortId)。
        /// 消费者 System 调用 <see cref="GetDataPortValue"/> 时用于定位生产者。
        /// </summary>
        public Dictionary<(int, string), (int, string)> DataInConnections { get; internal set; } = new();

        // ══════════════════════════════════════════
        //  动态数据（每帧由 System 读写）
        // ══════════════════════════════════════════

        /// <summary>
        /// 每个 Action 的运行时状态（索引与 Actions 一一对应）。
        /// 对齐 FrameSyncEngine 的 Component 数组。
        /// </summary>
        public ActionRuntimeState[] States { get; internal set; } = Array.Empty<ActionRuntimeState>();

        /// <summary>全局黑板变量</summary>
        public Blackboard Blackboard { get; internal set; } = new();

        /// <summary>
        /// 待处理的端口触发事件队列。
        /// System 产生事件 → 放入此队列 → TransitionSystem 消费并激活下游。
        /// </summary>
        public List<PortEvent> PendingEvents { get; } = new();

        /// <summary>
        /// 数据端口运行时值：actionIndex → (portId → stringValue)。
        /// 生产者 System 通过 <see cref="SetDataPortValue"/> 写入；
        /// 消费者 System 通过 <see cref="GetDataPortValue"/> 读取。
        /// </summary>
        public Dictionary<int, Dictionary<string, string>> DataPortValues { get; } = new();

        /// <summary>
        /// DataIn 端口默认值：(actionIndex, portId) → defaultValue 字符串。
        /// 由 <see cref="Interpreter.BlueprintLoader"/> 从 ActionEntry.PortDefaults 构建；
        /// <see cref="GetDataPortValue"/> 无连线时回退此表。
        /// </summary>
        public Dictionary<(int, string), string> DataPortDefaults { get; } = new();

        /// <summary>当前已执行的 Tick 数</summary>
        public int TickCount { get; internal set; }

        /// <summary>蓝图是否已执行完毕（所有 Flow.End 已到达，或无活跃 Action）</summary>
        public bool IsCompleted { get; internal set; }

        // ══════════════════════════════════════════
        //  查询辅助方法
        // ══════════════════════════════════════════

        /// <summary>按名称查找变量声明（线性搜索，变量数量少可接受）</summary>
        public VariableDeclaration? FindVariable(string name)
        {
            for (int i = 0; i < Variables.Length; i++)
            {
                if (Variables[i].Name == name)
                    return Variables[i];
            }
            return null;
        }

        /// <summary>按声明 Index 查找变量声明</summary>
        public VariableDeclaration? FindVariable(int index)
        {
            for (int i = 0; i < Variables.Length; i++)
            {
                if (Variables[i].Index == index)
                    return Variables[i];
            }
            return null;
        }

        /// <summary>根据 ActionId 获取 ActionIndex（-1 表示未找到）</summary>
        public int GetActionIndex(string actionId)
            => ActionIdToIndex.TryGetValue(actionId, out var idx) ? idx : -1;

        /// <summary>根据 ActionIndex 获取 TypeId</summary>
        public string GetTypeId(int actionIndex)
            => (actionIndex >= 0 && actionIndex < Actions.Length) ? Actions[actionIndex].TypeId : "";

        /// <summary>根据 ActionIndex 获取 Action 的属性值（原始字符串）</summary>
        public string GetProperty(int actionIndex, string key)
        {
            if (actionIndex < 0 || actionIndex >= Actions.Length) return "";
            var props = Actions[actionIndex].Properties;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i].Key == key) return props[i].Value;
            }
            return "";
        }

        /// <summary>
        /// 根据 ActionIndex 和类型化属性键获取已解析的值。
        /// <para>
        /// 相比字符串重载的优势：键名与目标类型在声明侧（ActionPortIds）一次绑定，
        /// 调用侧无需手动 Parse，解析失败返回 default(T) 而非抛出异常。
        /// </para>
        /// </summary>
        public T GetProperty<T>(int actionIndex, PropertyKey<T> key)
        {
            var raw = GetProperty(actionIndex, key.Key);
            return PropertyKeyParser.Parse<T>(raw);
        }

        /// <summary>
        /// 类型化属性获取，支持自定义默认值（无属性或解析失败时返回 defaultValue）。
        /// </summary>
        public T GetProperty<T>(int actionIndex, PropertyKey<T> key, T defaultValue)
        {
            var raw = GetProperty(actionIndex, key.Key);
            return string.IsNullOrEmpty(raw) ? defaultValue : PropertyKeyParser.Parse<T>(raw);
        }

        /// <summary>根据 ActionIndex 获取 SceneBindings</summary>
        public SceneBindingEntry[] GetSceneBindings(int actionIndex)
            => (actionIndex >= 0 && actionIndex < Actions.Length)
                ? Actions[actionIndex].SceneBindings
                : Array.Empty<SceneBindingEntry>();

        /// <summary>获取指定 TypeId 的所有 ActionIndex</summary>
        public List<int> GetActionIndices(string typeId)
            => ActionsByTypeId.TryGetValue(typeId, out var list) ? list : _emptyList;

        /// <summary>获取某个 Action 的所有出边 Transition 索引</summary>
        public List<int> GetOutgoingTransitionIndices(int actionIndex)
            => OutgoingTransitions.TryGetValue(actionIndex, out var list) ? list : _emptyList;

        /// <summary>检查是否有任何 Action 处于活跃状态（Running 或 WaitingTrigger）。
        /// Listening 状态属于被动观察节点（如 Flow.Filter），不阻塞主流程完成。</summary>
        public bool HasActiveActions()
        {
            for (int i = 0; i < States.Length; i++)
            {
                var phase = States[i].Phase;
                if (phase == ActionPhase.Running ||
                    phase == ActionPhase.WaitingTrigger)
                    return true;
            }
            return false;
        }

        // ── 事件操作 ──

        /// <summary>发射端口事件（Action 完成后，通知 TransitionSystem 路由至下游）</summary>
        public void EmitPortEvent(int fromIndex, string fromPortId, int toIndex, string toPortId)
        {
            PendingEvents.Add(new PortEvent(fromIndex, fromPortId, toIndex, toPortId));
        }

        /// <summary>清空事件队列（每轮 Tick 由 Runner 在消费完毕后调用）</summary>
        public void ClearEvents() => PendingEvents.Clear();

        // ── 数据端口操作 ──

        /// <summary>
        /// 写数据端口值（由生产者 System 在产出数据时调用，如 SpawnWaveSystem 每波开始时）。
        /// </summary>
        public void SetDataPortValue(int actionIndex, string portId, string value)
        {
            if (!DataPortValues.TryGetValue(actionIndex, out var portMap))
            {
                portMap = new Dictionary<string, string>();
                DataPortValues[actionIndex] = portMap;
            }
            portMap[portId] = value;
        }

        /// <summary>
        /// 读取消费者节点某个 DataIn 端口的值。
        /// 通过 <see cref="DataInConnections"/> 反向定位生产者，再读取其 DataPortValues。
        /// 返回 <c>null</c> 表示该端口无数据连线（消费者应自行决定 fallback 行为）。
        /// </summary>
        public string? GetDataPortValue(int toActionIndex, string toPortId)
        {
            if (!DataInConnections.TryGetValue((toActionIndex, toPortId), out var from))
            {
                // 无连线：回退到端口声明的默认值（来自 ActionDefinition.PortDefinition.DefaultValue）
                if (DataPortDefaults.TryGetValue((toActionIndex, toPortId), out var defaultVal))
                    return defaultVal;
                return null;
            }

            var (fromActionIndex, fromPortId) = from;
            if (DataPortValues.TryGetValue(fromActionIndex, out var portMap) &&
                portMap.TryGetValue(fromPortId, out var value))
                return value;

            return null;
        }

        private static readonly List<int> _emptyList = new();
    }
}
