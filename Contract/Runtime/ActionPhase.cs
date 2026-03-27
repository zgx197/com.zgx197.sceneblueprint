#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 节点执行阶段（统一枚举，两端共享）。
    /// <para>
    /// byte 底层类型——最小化 qtn 序列化开销，也兼容 int 强转。
    /// </para>
    /// </summary>
    public enum ActionPhase : byte
    {
        /// <summary>未激活</summary>
        Idle = 0,

        /// <summary>等待外部触发条件（如 Flow.Join 收集入边）</summary>
        WaitingTrigger = 1,

        /// <summary>执行中——System 每帧 Update 处理</summary>
        Running = 2,

        /// <summary>已完成（终态）</summary>
        Completed = 3,

        /// <summary>监听中（可被重新激活为 Running，如 Flow.Filter）</summary>
        Listening = 4,

        /// <summary>已失败（终态）</summary>
        Failed = 5,
    }
}
