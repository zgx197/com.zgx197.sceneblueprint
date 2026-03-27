#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 蓝图信号桥接器——外部系统与蓝图双向通信的统一入口。
    /// <para>
    /// 设计理念对齐 UE5 GameplayMessageSubsystem：
    /// - 单一入口（通过 BlueprintRunner.Bridge 获取）
    /// - Tag 路由（SignalTag 层级匹配）
    /// - Pub-Sub（Listen + Send）
    /// - 句柄生命周期（IDisposable 自动注销）
    /// </para>
    /// <para>
    /// Package 侧：直接使用 Listen/Send（回调模式）
    /// FS 侧：通过 GetEmittedThisTick() 轮询（确定性安全）
    /// </para>
    /// </summary>
    public interface ISignalBridge
    {
        // ═══ 蓝图→外部（监听蓝图发射的信号）═══

        /// <summary>
        /// 注册信号监听器。
        /// <para>
        /// 当蓝图 Signal.Emit 节点发射匹配的信号时，callback 会在帧末被调用。
        /// 返回 IDisposable 句柄，Dispose() 自动注销。
        /// </para>
        /// </summary>
        /// <param name="tag">要监听的信号标签</param>
        /// <param name="callback">匹配时的回调（参数：实际信号标签, 载荷）</param>
        /// <param name="mode">匹配模式：精确匹配或前缀匹配</param>
        /// <returns>监听句柄，Dispose 时自动注销</returns>
        IDisposable Listen(SignalTag tag, Action<SignalTag, SignalPayload> callback,
                           SignalMatchMode mode = SignalMatchMode.Exact);

        /// <summary>
        /// 获取本帧蓝图发射的所有信号（只读快照）。
        /// <para>
        /// 供 FS System 等无法使用回调的环境轮询消费。
        /// 信号在下一帧 OnBeginTick 时清空。
        /// </para>
        /// </summary>
        IReadOnlyList<SignalEntry> GetEmittedThisTick();

        // ═══ 外部→蓝图（向蓝图注入信号）═══

        /// <summary>
        /// 向蓝图注入信号。注入的信号在同帧或下帧被 WaitSignal 节点消费。
        /// <para>
        /// 对齐 UE5 BroadcastMessage 的语义——发送方不关心谁在接收。
        /// </para>
        /// </summary>
        /// <param name="tag">信号标签</param>
        /// <param name="payload">可选载荷</param>
        void Send(SignalTag tag, SignalPayload? payload = null);
    }

    /// <summary>
    /// 可选扩展接口：允许业务侧在注入信号时同时附带结构化事件上下文。
    /// 未实现该接口的桥接器仍可通过 <see cref="ISignalBridge.Send(SignalTag, SignalPayload?)"/> 工作。
    /// </summary>
    public interface IBlueprintEventContextSignalBridge : ISignalBridge
    {
        void Send(SignalTag tag, SignalPayload? payload, BlueprintEventContext? eventContext);
    }

    /// <summary>
    /// 信号匹配模式。
    /// </summary>
    public enum SignalMatchMode
    {
        /// <summary>精确匹配（tag hash 相等）</summary>
        Exact,

        /// <summary>
        /// 前缀匹配（如 Listen("Combat") 匹配 "Combat.Damage"、"Combat.Monster.Died" 等）。
        /// <para>对齐 UE5 GameplayTag 层级匹配语义。</para>
        /// </summary>
        PrefixMatch
    }
}
