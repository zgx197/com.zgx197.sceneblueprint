#nullable enable
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 转场系统——消费 PendingEvents，激活下游 Action 节点。
    /// <para>
    /// 两阶段处理：
    /// 1. 回收阶段：将所有 Completed 节点回收为 Idle
    /// 2. 激活阶段：消费 PendingEvents，将目标节点从 Idle/Listening 激活为 Running
    /// </para>
    /// <para>
    /// Flow.Join 特殊处理：通过 NodePrivateStateDomain 累加已收到的输入数，收齐后才激活。
    /// </para>
    /// <para>
    /// 执行顺序：必须在所有业务 System 之前执行（SystemGroup.Transition）。
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Transition)]
    public class TransitionSystem : BlueprintSystemBase, IFrameAware
    {
        public override string Name => "TransitionSystem";

        /// <summary>BlueprintFrame 引用——用于 _activatedBy 黑板追踪</summary>
        public BlueprintFrame? Frame { get; set; }

        public override void Update(ref FrameView view)
        {
            // Phase 1：回收 Completed → Idle
            RecycleCompleted(ref view);

            // Phase 2：消费 PendingEvents → 激活下游
            ConsumeEvents(ref view, Frame);
        }

        /// <summary>将所有 Completed 节点回收为 Idle（释放活跃资源）</summary>
        private static void RecycleCompleted(ref FrameView view)
        {
            for (int i = 0; i < view.ActionCount; i++)
            {
                if (view.States[i].Phase == ActionPhase.Completed)
                {
                    view.States[i].Reset();
                }
            }
        }

        /// <summary>消费 PendingEvents，激活下游节点</summary>
        private static void ConsumeEvents(ref FrameView view, BlueprintFrame? frame)
        {
            if (view.PendingEvents.Count == 0)
                return;

            for (int e = 0; e < view.PendingEvents.Count; e++)
            {
                var ev = view.PendingEvents[e];
                TransitionActivationRuntimeSupport.ConsumeEvent(frame, ref view, ev);
            }

            // 清空已消费的事件
            view.PendingEvents.Clear();
        }
    }
}
