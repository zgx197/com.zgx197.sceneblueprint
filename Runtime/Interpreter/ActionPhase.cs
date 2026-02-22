#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 节点执行阶段。
    /// <para>
    /// 对齐 FrameSyncEngine 的 Entity 状态模型：
    /// 每个 Action 在 BlueprintFrame 中有一个 Phase，
    /// System 根据 Phase 决定是否处理该 Action。
    /// </para>
    /// </summary>
    public enum ActionPhase
    {
        /// <summary>未激活——尚未被任何 Transition 触发</summary>
        Idle = 0,

        /// <summary>等待触发——已注册监听条件（如 Trigger.EnterArea），等待条件满足</summary>
        WaitingTrigger,

        /// <summary>执行中——System 每帧 Update 处理</summary>
        Running,

        /// <summary>已完成——正常结束，等待 TransitionSystem 传播至下游</summary>
        Completed,

        /// <summary>
        /// 监听中——已完成一次执行，但仍在等待新的输入事件以重新激活。
        /// <para>
        /// 典型场景：Flow.Filter 接在 Spawn.Wave.onWaveStart 后面，
        /// 每波事件到达时需要重新执行条件判断。
        /// </para>
        /// <para>
        /// 生命周期：Idle → Running → Listening → (收到新事件) → Running → Listening → ... → Completed
        /// </para>
        /// <para>
        /// 与 Completed 的区别：
        /// - Completed 是终态，TransitionSystem 不会再激活该节点
        /// - Listening 是等待态，TransitionSystem 收到新事件时会执行"软重置"并重新激活为 Running
        /// </para>
        /// <para>
        /// 软重置规则（Listening → Running 时）：
        /// - TicksInPhase = 0（新一轮执行）
        /// - TransitionPropagated = false（允许新的出边传播）
        /// - CustomInt / CustomFloat 保留（业务 System 自行管理累计状态）
        /// - _activatedBy 更新为新的来源节点
        /// </para>
        /// </summary>
        Listening,

        /// <summary>已失败——异常结束</summary>
        Failed
    }
}
