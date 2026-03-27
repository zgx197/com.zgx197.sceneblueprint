#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 信号总线接口——System 完全无状态的帧级快照轮询模式。
    /// <para>
    /// 设计原则：
    /// 1. System 不持有任何跨帧可变数据（订阅表、回调字典等）
    /// 2. 帧级快照轮询替代 Subscribe/Callback 模式
    /// 3. 回滚天然安全——所有状态由 qtn 组件（或 FrameView.States）持有
    /// 4. 确定性优先——遍历顺序完全确定，无回调时序问题
    /// </para>
    /// <para>
    /// Package 侧实现：InMemorySignalBus（C# 堆内存队列）
    /// mini_game 侧实现：FSSignalBus（qtn Singleton 队列 + 帧级缓存）
    /// </para>
    /// </summary>
    public interface ISignalBus
    {
        // ═══════════════════════════════════════
        //  蓝图 → 外部（Signal.Emit 节点产生）
        // ═══════════════════════════════════════

        /// <summary>蓝图发射信号</summary>
        void Emit(SignalTag tag, SignalPayload? payload, BlueprintEventContext? eventContext = null);

        /// <summary>获取本帧所有已发射的信号（只读快照）</summary>
        IReadOnlyList<SignalEntry> GetFrameEmitted();

        // ═══════════════════════════════════════
        //  外部 → 蓝图（业务系统注入）
        // ═══════════════════════════════════════

        /// <summary>外部向蓝图注入信号</summary>
        void Inject(SignalTag tag, SignalPayload? payload, BlueprintEventContext? eventContext = null);

        /// <summary>获取本帧所有已注入的信号（只读快照）</summary>
        IReadOnlyList<SignalEntry> GetFrameInjected();

        // ═══════════════════════════════════════
        //  条件监听（WatchCondition 节点用）
        // ═══════════════════════════════════════

        /// <summary>通知 Bus 开始条件监听（IsFirstEntry 时调用）</summary>
        ConditionWatchHandle BeginConditionWatch(ConditionWatchRegistration registration);

        /// <summary>通知 Bus 结束条件监听（Completed/超时时调用）</summary>
        void EndConditionWatch(ConditionWatchHandle watchHandle);

        /// <summary>查询指定节点的条件是否在本帧被触发</summary>
        bool IsConditionTriggered(ConditionWatchHandle watchHandle);

        /// <summary>
        /// 触发指定节点的条件（由 IConditionEvaluator 实现者调用）。
        /// mini_game 侧由外部 System 直接写入 qtn 队列，等效于调用此方法。
        /// </summary>
        void TriggerCondition(ConditionWatchHandle watchHandle);

        // ═══════════════════════════════════════
        //  条件评估器注册
        // ═══════════════════════════════════════

        /// <summary>注册条件评估器（按 conditionType 匹配）</summary>
        void RegisterEvaluator(IConditionEvaluator evaluator);

        // ═══════════════════════════════════════
        //  生命周期（由 Adapter 调用）
        // ═══════════════════════════════════════

        /// <summary>帧开始时调用（检测回滚、准备帧数据）</summary>
        void OnBeginTick(int currentTick);

        /// <summary>帧结束时调用（清理帧级缓冲）</summary>
        void OnEndTick();

        /// <summary>蓝图销毁时调用（清理所有资源）</summary>
        void Dispose();
    }
}
