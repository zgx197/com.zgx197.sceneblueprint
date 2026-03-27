#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 条件过滤系统——处理 Flow.Filter 节点。
    /// <para>
    /// 生命周期（pass）：Idle → Running → Completed → (RecycleCompleted) → Idle → (新事件) → Running → ...
    /// 生命周期（reject）：Idle → Running → Listening → (新事件) → Running → ...
    /// </para>
    /// <para>
    /// pass 进入 Completed 表示"本次评估已完成使命"；RecycleCompleted 会在下一 Tick
    /// 将其重置为 Idle，后续事件仍可重新激活。reject 进入 Listening 表示"条件不满足，
    /// 继续等待下一个事件"。蓝图结束时 BlueprintRunner.Tick() 终态清理统一将 Listening
    /// 节点设为 Completed。
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    [UpdateAfter(typeof(BlackboardGetSystem))]
    public class FlowFilterSystem : BlueprintSystemBase, IFrameAware
    {
        internal static readonly NodeStateDescriptor<FlowFilterNodeState> FlowFilterStateDescriptor =
            new(
                "flow.filter.state",
                StateLifetime.Execution,
                static () => new FlowFilterNodeState(),
                debugName: "Flow.Filter State");

        private bool _lifecycleBindingRegistered;

        public override string Name => "FlowFilterSystem";

        /// <summary>BlueprintFrame 引用——用于 DataPort 值读取（FrameView 尚未包含 DataPort 机制）</summary>
        public BlueprintFrame? Frame { get; set; }

        public override void OnInit(BlueprintFrame frame)
        {
            _lifecycleBindingRegistered = false;
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, FlowFilterStateDescriptor, ref _lifecycleBindingRegistered);
        }

        public override void OnDisabled(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Flow.Filter);
            for (var index = 0; index < indices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], FlowFilterStateDescriptor);
            }
        }

        public override void Update(ref FrameView view)
        {
            var indices = view.Query.GetActionIndices(AT.Flow.Filter);
            if (indices == null || Frame == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, FlowFilterStateDescriptor, ref _lifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running) continue;
                ProcessFilter(ref view, idx, ref state);
            }
        }

        private void ProcessFilter(ref FrameView view, int actionIndex, ref ActionRuntimeState state)
        {
            if (Frame == null)
            {
                return;
            }

            FlowFilterExecutionSupport.TryExecute(
                ref view,
                Frame,
                actionIndex,
                ref state);
        }
    }
}
