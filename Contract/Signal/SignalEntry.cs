#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 统一信号条目——帧级信号数据（值类型，两端共享）。
    /// <para>
    /// 运行时全部使用 int hash，满足 qtn unmanaged 约束。
    /// DEBUG 模式下保留原始字符串用于调试。
    /// </para>
    /// </summary>
    public struct SignalEntry
    {
        /// <summary>信号标签 hash</summary>
        public int TagHash;

        /// <summary>关联的 Action 索引（-1 = 无关联）</summary>
        public int ActionIndex;

        /// <summary>可选整数载荷</summary>
        public int PayloadInt;

        /// <summary>主体引用的原始序列化文本，用于 WaitSignal 等基于主体语义的匹配。</summary>
        public string SubjectRefSerialized;

        /// <summary>
        /// 结构化事件上下文快照。
        /// 让 WaitSignal / 调试观测层在消费注入信号时，能继续看到业务入口附带的主体、目标和载荷摘要。
        /// </summary>
        public BlueprintEventContext? EventContext;

#if UNITY_EDITOR || DEBUG
        /// <summary>调试用：信号标签原始字符串</summary>
        public string? DebugTag;
#endif

        public override string ToString()
        {
#if UNITY_EDITOR || DEBUG
            var tag = DebugTag ?? TagHash.ToString();
            return $"SignalEntry(tag={tag}, action={ActionIndex}, payload={PayloadInt}, subject={SubjectRefSerialized})";
#else
            return $"SignalEntry(tagHash={TagHash}, action={ActionIndex}, payload={PayloadInt}, subject={SubjectRefSerialized})";
#endif
        }
    }
}
