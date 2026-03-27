#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 蓝图事件上下文——为调试、观察和可视化提供带主体语义的结构化事件快照。
    /// <para>
    /// 第一版优先覆盖 Signal.Emit 等瞬时节点，后续可逐步扩展到注入、匹配、超时等事件。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class BlueprintEventContext
    {
        /// <summary>事件类型，如 Signal.Emit / Signal.Inject。</summary>
        public string EventKind = string.Empty;

        /// <summary>来源节点 Id。</summary>
        public string ActionId = string.Empty;

        /// <summary>来源节点索引。</summary>
        public int ActionIndex = -1;

        /// <summary>事件发生时的 Tick。</summary>
        public int Tick;

        /// <summary>信号标签（如适用）。</summary>
        public string SignalTag = string.Empty;

        /// <summary>主体引用的原始序列化文本。</summary>
        public string SubjectRefSerialized = string.Empty;

        /// <summary>发起者引用的原始序列化文本。</summary>
        public string InstigatorRefSerialized = string.Empty;

        /// <summary>目标引用的原始序列化文本。</summary>
        public string TargetRefSerialized = string.Empty;

        /// <summary>结构化主体引用。</summary>
        public EntityRef SubjectRef = new();

        /// <summary>结构化发起者引用。</summary>
        public EntityRef InstigatorRef = new();

        /// <summary>结构化目标引用。</summary>
        public EntityRef TargetRef = new();

        /// <summary>主体可读摘要。</summary>
        public string SubjectSummary = string.Empty;

        /// <summary>发起者可读摘要。</summary>
        public string InstigatorSummary = string.Empty;

        /// <summary>目标可读摘要。</summary>
        public string TargetSummary = string.Empty;

        /// <summary>信号载荷。</summary>
        public SignalPayload Payload = SignalPayload.Empty;

        /// <summary>信号载荷可读摘要。</summary>
        public string PayloadSummary = string.Empty;
    }
}
