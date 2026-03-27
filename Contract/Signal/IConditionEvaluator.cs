#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 条件评估器接口（Contract 层统一定义）——由外部业务系统实现，用于驱动 WatchCondition 节点。
    /// <para>
    /// 与 Runtime 层旧版 IConditionEvaluator 的区别：
    /// - 旧版使用回调模式（onTriggered 委托），System 持有状态 → 回滚不安全
    /// - 新版配合 ISignalBus 轮询模式，Evaluator 只负责注册和触发，
    ///   状态由 Bus 持有并暴露给 System 查询（IsConditionTriggered）
    /// </para>
    /// <para>
    /// Package 侧：Evaluator 主动调用 Bus.TriggerCondition 触发条件（Digong C# 运行时）
    /// mini_game 侧：外部 System 直接写入 qtn 队列，无需 Evaluator（帧同步安全）
    /// </para>
    /// </summary>
    public interface IConditionEvaluator
    {
        /// <summary>条件类型 ID（对应 WatchCondition 节点的 conditionType 属性）</summary>
        string TypeId { get; }

        /// <summary>
        /// 开始监听条件。
        /// 当条件满足时，实现者应通过 ISignalBus 通知框架。
        /// </summary>
        /// <param name="registration">结构化条件监听注册对象（含 handle / descriptor / normalized type）</param>
        /// <param name="bus">信号总线引用，用于触发条件通知</param>
        void BeginWatch(ConditionWatchRegistration registration, ISignalBus bus);

        /// <summary>停止监听条件</summary>
        void EndWatch(ConditionWatchHandle watchHandle);
    }
}
