#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Diagnostics
{
    /// <summary>
    /// 单条 Blackboard 变量的快照条目（值类型，零GC分配，序列化为字符串避免装箱问题）。
    /// </summary>
    public readonly struct BlackboardEntry
    {
        /// <summary>变量的整型索引（与 VariableDeclaration.Index 对应）</summary>
        public readonly int Index;

        /// <summary>变量名称（来自 VariableDeclaration.Name，调试显示用）</summary>
        public readonly string Name;

        /// <summary>变量类型字符串（"int"/"float"/"bool"/"string" 等）</summary>
        public readonly string TypeStr;

        /// <summary>序列化后的值字符串（ToString()），避免 object 装箱在快照层面产生类型丢失</summary>
        public readonly string ValueStr;

        public BlackboardEntry(int index, string name, string typeStr, string valueStr)
        {
            Index    = index;
            Name     = name;
            TypeStr  = typeStr;
            ValueStr = valueStr;
        }

        public override string ToString() => $"{Name}[{Index}]:{TypeStr}={ValueStr}";
    }

    /// <summary>
    /// 某一帧结束时的蓝图状态快照。
    /// <para>
    /// 由 <see cref="BlueprintFrameHistory"/> 的对象池管理，外部只读，不要持久持有引用。
    /// </para>
    /// </summary>
    public class BlueprintFrameSnapshot
    {
        /// <summary>对应的 TickCount（与 BlueprintFrame.TickCount 一致）</summary>
        public int TickCount;

        /// <summary>记录时的 UnityEngine.Time.time（秒），用于换算真实时间轴</summary>
        public float Timestamp;

        /// <summary>
        /// ActionRuntimeState 数组的完整值拷贝。
        /// struct 数组 Array.Copy 天然深拷贝，无引用共享问题。
        /// </summary>
        public ActionRuntimeState[] States = Array.Empty<ActionRuntimeState>();

        /// <summary>
        /// 本帧结束时排队等待下帧处理的端口事件快照。
        /// 代表"本帧执行后，哪些下游连线将在下一帧被激活"。
        /// </summary>
        public PortEvent[] PendingEventsSnapshot = Array.Empty<PortEvent>();

        /// <summary>
        /// 与上一帧相比，Phase 发生变化的节点列表。
        /// 空数组表示本帧无任何节点状态迁移。
        /// </summary>
        public StateDiff[] Diffs = Array.Empty<StateDiff>();

        /// <summary>
        /// 本帧结束时 Local Blackboard 的声明变量快照。
        /// 每个条目已序列化为字符串，不存在 object 装箱类型丢失问题。
        /// </summary>
        public BlackboardEntry[] BlackboardEntries = Array.Empty<BlackboardEntry>();

        // ── 内部对象池标记 ──
        internal bool InUse;

        /// <summary>
        /// 从 BlueprintFrame 捕获当前状态（复用已分配的数组，尽量不产生 GC）。
        /// </summary>
        internal void CaptureFrom(BlueprintFrame frame)
        {
            TickCount = frame.TickCount;
            Timestamp = Time.time;

            // States：按需扩容，然后值拷贝
            if (States.Length != frame.States.Length)
                States = new ActionRuntimeState[frame.States.Length];
            Array.Copy(frame.States, States, frame.States.Length);

            // PendingEvents：按需扩容，然后逐项拷贝（struct）
            var events = frame.PendingEvents;
            if (PendingEventsSnapshot.Length != events.Count)
                PendingEventsSnapshot = new PortEvent[events.Count];
            for (int i = 0; i < events.Count; i++)
                PendingEventsSnapshot[i] = events[i];

            // Blackboard：序列化为字符串条目，结合 Variables 补充名称与类型
            var declared = frame.Blackboard.DeclaredEntries;
            if (BlackboardEntries.Length != declared.Count)
                BlackboardEntries = new BlackboardEntry[declared.Count];
            int idx = 0;
            foreach (var kvp in declared)
            {
                var decl = frame.FindVariable(kvp.Key);
                BlackboardEntries[idx++] = new BlackboardEntry(
                    kvp.Key,
                    decl?.Name  ?? kvp.Key.ToString(),
                    decl?.Type  ?? kvp.Value.ValueType.Name,
                    kvp.Value.BoxedValue?.ToString() ?? "null"
                );
            }
        }
    }
}
