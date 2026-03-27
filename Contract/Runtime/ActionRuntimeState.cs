#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 单个 Action 节点的运行时状态（统一值类型，两端共享）。
    /// <para>
    /// 设计要点：
    /// - 废弃浮点槽（CustomFloat/CustomFP），改为两个 int 槽——零精度损失，兼容 qtn unmanaged
    /// - 新增 IsFirstEntry 显式标记，替代 CustomFP==0 的隐式判断
    /// - 新增 EventEmitted 死锁检测标记
    /// - TicksInPhase 由框架自动计算，System 只读
    /// </para>
    /// </summary>
    public struct ActionRuntimeState
    {
        /// <summary>当前执行阶段</summary>
        public ActionPhase Phase;

        /// <summary>在当前 Phase 中已执行的 Tick 数（框架自动计算，System 只读）</summary>
        public int TicksInPhase;

        /// <summary>通用整型槽 0（System 自定义，如：超时 Tick、波次索引）</summary>
        public int CustomInt0;

        /// <summary>通用整型槽 1（System 自定义，如：掩码、剩余怪物数）</summary>
        public int CustomInt1;

        /// <summary>
        /// 首次进入标记——节点被激活时由 TransitionSystem 设为 true。
        /// 业务 System 在首帧执行初始化后设为 false。
        /// 彻底替代原 CustomFP==0 的隐式判断。
        /// </summary>
        public bool IsFirstEntry;

        /// <summary>
        /// 端口事件发射标记——EmitFlowEvent/EmitOutEvent 时自动设为 true。
        /// 用于死锁检测：Completed 但 EventEmitted=false → 忘记发射端口事件。
        /// </summary>
        public bool EventEmitted;

        /// <summary>重置为初始状态</summary>
        public void Reset()
        {
            Phase = ActionPhase.Idle;
            TicksInPhase = 0;
            CustomInt0 = 0;
            CustomInt1 = 0;
            IsFirstEntry = false;
            EventEmitted = false;
        }

        /// <summary>
        /// 软重置——从 Listening 重新激活为 Running 时调用。
        /// 重置执行计时和传播标记，设置 IsFirstEntry 允许业务 System 重新初始化，
        /// 但保留 CustomInt0/CustomInt1（业务 System 可能需要累计状态）。
        /// </summary>
        public void SoftReset()
        {
            Phase = ActionPhase.Running;
            TicksInPhase = 0;
            EventEmitted = false;
            IsFirstEntry = true;
            // CustomInt0 / CustomInt1 保留——业务 System 自行管理累计状态
        }
    }
}
