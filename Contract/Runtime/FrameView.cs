#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 帧视图——每帧由 Adapter 填充的统一数据视图，System 通过此结构读写所有状态。
    /// <para>
    /// 核心里念（借鉴 Unity DOTS + 行为树 Agent 模式）：
    /// - 每帧开始：Adapter 将底层存储投影为 FrameView
    /// - System 执行：直接操作 States 数组（零虚调用热路径）+ 通过接口做低频查询
    /// - 每帧结束：Adapter 将 FrameView 回写到底层存储
    /// </para>
    /// <para>
    /// States / PendingEvents 是直接数组/列表引用，热路径零虚调用。
    /// Query / Router / Bus 是接口引用，用于低频操作（属性查询、事件发射、信号通信）。
    /// </para>
    /// </summary>
    public struct FrameView
    {
        // ══════════════════════════════════════════
        //  高频直接访问（热路径，零虚调用）
        // ══════════════════════════════════════════

        /// <summary>
        /// 每个 Action 的运行时状态（索引与 Action 一一对应）。
        /// System 直接通过数组索引读写，零虚调用。
        /// </summary>
        public ActionRuntimeState[] States;

        /// <summary>
        /// 待处理的端口触发事件缓冲区。
        /// System 通过 Router 写入，TransitionSystem 消费。
        /// </summary>
        public List<PortEvent> PendingEvents;

        /// <summary>Action 总数（等于 States.Length）</summary>
        public int ActionCount;

        /// <summary>当前 Tick 计数</summary>
        public int CurrentTick;

        /// <summary>
        /// 目标 Tick 率（每秒多少 Tick，用于秒→Tick 转换）。
        /// <para>
        /// 该字段保留给现有系统和业务层做兼容读取，避免本轮时间系统重构影响面过大。
        /// 新代码应优先通过 <see cref="TimeSettings"/> 获取完整的时间配置，而不是只读取 TickRate。
        /// </para>
        /// </summary>
        public int TargetTickRate;

        /// <summary>
        /// 统一时间配置。
        /// <para>
        /// 由 Adapter 在 BeginTick 时填充，供所有运行时 System 统一读取。
        /// 这里承载的不只是 TickRate，还包含秒→Tick 量化时使用的舍入策略，
        /// 是时间语义正式收口后的标准入口。
        /// </para>
        /// </summary>
        public BlueprintTimeSettings TimeSettings;

        // ══════════════════════════════════════════
        //  低频接口访问
        // ══════════════════════════════════════════

        /// <summary>低频查询接口（读取属性、TypeId、入边信息等）</summary>
        public IActionQuery Query;

        /// <summary>事件路由接口（发射端口事件）</summary>
        public ITransitionRouter Router;

        /// <summary>信号总线接口（信号发射/注入/条件监听）</summary>
        public ISignalBus? Bus;
    }
}
