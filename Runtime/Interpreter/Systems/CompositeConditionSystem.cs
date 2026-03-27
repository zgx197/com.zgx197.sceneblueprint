#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 组合条件系统——处理 Signal.CompositeCondition 节点。
    /// <para>
    /// 业务状态已迁入 NodePrivateStateDomain：
    /// - ConnectedMask: 已连接 cond_x 端口掩码
    /// - TriggeredMask: 已触发 cond_x 端口掩码
    /// - TimeoutTargetTick: 超时 deadline
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    [UpdateAfter(typeof(SignalSystem))]
    public class CompositeConditionSystem : BlueprintSystemBase, IFrameAware
    {
        internal static readonly NodeStateDescriptor<CompositeConditionNodeState> CompositeConditionStateDescriptor =
            new(
                "signal.composite-condition.state",
                StateLifetime.Execution,
                static () => new CompositeConditionNodeState(),
                debugName: "Signal.CompositeCondition State");

        public override string Name => "CompositeConditionSystem";

        private bool _lifecycleBindingRegistered;

        public BlueprintFrame? Frame { get; set; }

        public override void OnInit(BlueprintFrame frame)
        {
            _lifecycleBindingRegistered = false;
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, CompositeConditionStateDescriptor, ref _lifecycleBindingRegistered);
        }

        public override void OnDisabled(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Signal.CompositeCondition);
            for (var index = 0; index < indices.Count; index++)
            {
                if (CompositeConditionExecutionSupport.TryGetCompositeConditionState(frame, indices[index], out var compositeState))
                {
                    CompositeConditionExecutionSupport.ReleaseAuxiliaryState(frame, compositeState!);
                }

                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], CompositeConditionStateDescriptor);
            }
        }

        public override void Update(ref FrameView view)
        {
            if (Frame == null)
            {
                return;
            }

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, CompositeConditionStateDescriptor, ref _lifecycleBindingRegistered);
            ProcessCompositeCondition(ref view, Frame);
        }

        private static void ProcessCompositeCondition(ref FrameView view, BlueprintFrame frame)
        {
            var indices = view.Query.GetActionIndices(AT.Signal.CompositeCondition);
            if (indices == null)
            {
                return;
            }

            for (var i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running)
                {
                    continue;
                }

                CompositeConditionExecutionSupport.TryExecute(
                    ref view,
                    frame,
                    idx,
                    ref state);
            }
        }
    }
}
