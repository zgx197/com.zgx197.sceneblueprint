#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 端口触发事件（值类型，零 GC）。
    /// <para>
    /// 当一个 Action 完成（Phase → Completed）时，TransitionSystem 根据 Transition 表
    /// 生成 PortEvent 放入事件队列；下一轮 Tick 由 TransitionSystem 消费，激活目标 Action。
    /// </para>
    /// <para>
    /// 对齐 FrameSyncEngine 的 Signal/Event 概念：
    /// Frame 内的帧内通信机制，System 之间通过事件队列解耦。
    /// </para>
    /// </summary>
    public struct PortEvent
    {
        /// <summary>源 Action 在 Frame.States 中的索引</summary>
        public int FromActionIndex;

        /// <summary>源端口 ID（如 "out"）</summary>
        public string FromPortId;

        /// <summary>目标 Action 在 Frame.States 中的索引</summary>
        public int ToActionIndex;

        /// <summary>目标端口 ID（如 "in"）</summary>
        public string ToPortId;

        public PortEvent(int fromIndex, string fromPort, int toIndex, string toPort)
        {
            FromActionIndex = fromIndex;
            FromPortId = fromPort;
            ToActionIndex = toIndex;
            ToPortId = toPort;
        }

        public override string ToString()
            => $"PortEvent({FromActionIndex}:{FromPortId} → {ToActionIndex}:{ToPortId})";
    }
}
