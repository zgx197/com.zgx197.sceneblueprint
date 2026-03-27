#nullable enable
using UnityEngine;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    public sealed class BranchNodeState : IGraphTimedNodeState
    {
        public int StartTick { get; set; }

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public bool ConditionResult { get; set; }

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public string RoutedPort { get; set; } = string.Empty;

        public int GetElapsedTicks(int currentTick)
        {
            return Mathf.Max(0, currentTick - StartTick);
        }
    }

    /// <summary>
    /// 流程控制系统——处理 Flow.Start / Flow.End / Flow.Delay / Flow.Join 等流程节点。
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    public class FlowSystem : BlueprintSystemBase, IFrameAware
    {
        private const string FlowEndCompletionEventValue = "blueprint.complete";

        internal static readonly NodeStateDescriptor<InstantEventNodeState> StartStateDescriptor =
            new(
                "flow.start.state",
                StateLifetime.Execution,
                static () => new InstantEventNodeState(),
                debugName: "Flow.Start State");

        internal static readonly NodeStateDescriptor<InstantEventNodeState> EndStateDescriptor =
            new(
                "flow.end.state",
                StateLifetime.Execution,
                static () => new InstantEventNodeState(),
                debugName: "Flow.End State");

        internal static readonly NodeStateDescriptor<TimedNodeState> DelayStateDescriptor =
            new(
                "flow.delay.state",
                StateLifetime.Execution,
                static () => new TimedNodeState(),
                debugName: "Flow.Delay State");

        internal static readonly NodeStateDescriptor<BranchNodeState> BranchStateDescriptor =
            new(
                "flow.branch.state",
                StateLifetime.Execution,
                static () => new BranchNodeState(),
                debugName: "Flow.Branch State");

        internal static readonly NodeStateDescriptor<JoinNodeState> JoinStateDescriptor =
            new(
                "flow.join.state",
                StateLifetime.Execution,
                static () => new JoinNodeState(),
                debugName: "Flow.Join State");

        private bool _startLifecycleBindingRegistered;
        private bool _endLifecycleBindingRegistered;
        private bool _delayLifecycleBindingRegistered;
        private bool _branchLifecycleBindingRegistered;
        private bool _joinLifecycleBindingRegistered;

        public override string Name => "FlowSystem";

        public BlueprintFrame? Frame { get; set; }

        public override void OnInit(BlueprintFrame frame)
        {
            _startLifecycleBindingRegistered = false;
            _endLifecycleBindingRegistered = false;
            _delayLifecycleBindingRegistered = false;
            _branchLifecycleBindingRegistered = false;
            _joinLifecycleBindingRegistered = false;
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, StartStateDescriptor, ref _startLifecycleBindingRegistered);
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, EndStateDescriptor, ref _endLifecycleBindingRegistered);
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, DelayStateDescriptor, ref _delayLifecycleBindingRegistered);
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, BranchStateDescriptor, ref _branchLifecycleBindingRegistered);
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, JoinStateDescriptor, ref _joinLifecycleBindingRegistered);
        }

        public override void OnDisabled(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Flow.Start);
            for (var index = 0; index < indices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], StartStateDescriptor);
            }

            indices = frame.GetActionIndices(AT.Flow.End);
            for (var index = 0; index < indices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], EndStateDescriptor);
            }

            indices = frame.GetActionIndices(AT.Flow.Delay);
            for (var index = 0; index < indices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], DelayStateDescriptor);
            }

            indices = frame.GetActionIndices(AT.Flow.Branch);
            for (var index = 0; index < indices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], BranchStateDescriptor);
            }

            indices = frame.GetActionIndices(AT.Flow.Join);
            for (var index = 0; index < indices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], JoinStateDescriptor);
            }
        }

        public override void Update(ref FrameView view)
        {
            ProcessFlowStart(ref view);
            ProcessFlowEnd(ref view);
            ProcessFlowBranch(ref view);
            ProcessFlowDelay(ref view);
            ProcessFlowJoin(ref view);
        }

        private void ProcessFlowStart(ref FrameView view)
        {
            if (Frame == null)
            {
                return;
            }

            var indices = view.Query.GetActionIndices(AT.Flow.Start);
            if (indices == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, StartStateDescriptor, ref _startLifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running)
                {
                    continue;
                }

                FlowInstantExecutionSupport.TryExecute(
                    ref view,
                    Frame,
                    idx,
                    ref state,
                    StartStateDescriptor,
                    new FlowInstantNodeRuntimePlan(
                        "intrinsic",
                        "intrinsic | Flow.Start",
                        "发射默认 out 端口",
                        AT.Flow.Start,
                        ActionPortIds.FlowStart.Out,
                        isTerminal: false,
                        ActionPortIds.FlowStart.Out));
            }
        }

        private void ProcessFlowEnd(ref FrameView view)
        {
            if (Frame == null)
            {
                return;
            }

            var indices = view.Query.GetActionIndices(AT.Flow.End);
            if (indices == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, EndStateDescriptor, ref _endLifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running)
                {
                    continue;
                }

                FlowInstantExecutionSupport.TryExecute(
                    ref view,
                    Frame,
                    idx,
                    ref state,
                    EndStateDescriptor,
                    new FlowInstantNodeRuntimePlan(
                        "intrinsic",
                        "intrinsic | Flow.End",
                        "发射默认 out 端口并结束蓝图",
                        AT.Flow.End,
                        FlowEndCompletionEventValue,
                        isTerminal: true,
                        "out"),
                    markBlueprintCompleted: true);
            }
        }

        private void ProcessFlowDelay(ref FrameView view)
        {
            var indices = view.Query.GetActionIndices(AT.Flow.Delay);
            if (indices == null || Frame == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, DelayStateDescriptor, ref _delayLifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running) continue;

                FlowDelayExecutionSupport.TryExecute(
                    ref view,
                    Frame,
                    idx,
                    ref state);
            }
        }

        private void ProcessFlowBranch(ref FrameView view)
        {
            var indices = view.Query.GetActionIndices(AT.Flow.Branch);
            if (indices == null || Frame == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, BranchStateDescriptor, ref _branchLifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running)
                {
                    continue;
                }

                FlowBranchExecutionSupport.TryExecute(
                    ref view,
                    Frame,
                    idx,
                    ref state);
            }
        }

        private void ProcessFlowJoin(ref FrameView view)
        {
            var indices = view.Query.GetActionIndices(AT.Flow.Join);
            if (indices == null || Frame == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, JoinStateDescriptor, ref _joinLifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running)
                {
                    continue;
                }

                FlowJoinExecutionSupport.TryExecute(
                    ref view,
                    Frame,
                    idx,
                    ref state);
            }
        }
    }
}
