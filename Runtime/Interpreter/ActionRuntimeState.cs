#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 单个 Action 节点的运行时状态（值类型，存储在 BlueprintFrame.States 数组中）。
    /// <para>
    /// 对齐 FrameSyncEngine 的 Component 概念：
    /// 纯数据，无逻辑，由 System 读写。
    /// </para>
    /// </summary>
    public struct ActionRuntimeState
    {
        /// <summary>当前执行阶段</summary>
        public ActionPhase Phase;

        /// <summary>在当前阶段已执行的 Tick 次数（用于 Duration 型节点计时）</summary>
        public int TicksInPhase;

        /// <summary>通用整型状态槽（System 可自定义用途，避免装箱）</summary>
        public int CustomInt;

        /// <summary>通用浮点状态槽（System 可自定义用途）</summary>
        public float CustomFloat;

        /// <summary>
        /// TransitionSystem 专用标记：该 Completed Action 的出边是否已传播。
        /// 与 CustomInt 解耦，避免业务 System 使用 CustomInt 时产生冲突。
        /// </summary>
        public bool TransitionPropagated;

        /// <summary>
        /// 首次进入标记——节点被激活（Idle→Running 或 Listening→Running）时由 TransitionSystem 设为 true。
        /// <para>
        /// 业务 System 在 Update 中检测此标记执行初始化逻辑（读取属性、计算目标 Tick 等），
        /// 初始化完成后将其设为 false。
        /// </para>
        /// <para>
        /// 引入原因：TransitionSystem (Order=900) 在帧末激活节点后，BlueprintRunner 会递增 TicksInPhase，
        /// 导致下一帧业务 System 看到的 TicksInPhase 永远不是 0，无法用 TicksInPhase==0 做首帧检测。
        /// IsFirstEntry 将"首次进入检测"与 TicksInPhase 时序彻底解耦。
        /// </para>
        /// </summary>
        public bool IsFirstEntry;

        /// <summary>重置为初始状态</summary>
        public void Reset()
        {
            Phase = ActionPhase.Idle;
            TicksInPhase = 0;
            CustomInt = 0;
            CustomFloat = 0f;
            TransitionPropagated = false;
            IsFirstEntry = false;
        }

        /// <summary>
        /// 软重置——从 Listening 重新激活为 Running 时调用。
        /// <para>
        /// 重置执行计时和传播标记，设置 IsFirstEntry 允许业务 System 重新初始化，
        /// 但保留 CustomInt/CustomFloat（业务 System 可能需要累计状态）。
        /// 借鉴行为树的 OnBehaviorRestart 和状态机的 self-transition 语义。
        /// </para>
        /// </summary>
        public void SoftReset()
        {
            Phase = ActionPhase.Running;
            TicksInPhase = 0;
            TransitionPropagated = false;
            IsFirstEntry = true;
            // CustomInt / CustomFloat 保留——业务 System 自行管理
        }
    }
}
