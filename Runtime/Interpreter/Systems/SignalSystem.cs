#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 信号系统——处理 Signal.Emit / Signal.WaitSignal / Signal.WatchCondition 节点。
    /// <para>
    /// 通过 FrameView.Bus（ISignalBus）实现帧级无状态轮询：
    /// - Emit：通过 Bus.Emit 发射信号
    /// - WaitSignal：轮询 Bus.GetFrameInjected 匹配信号
    /// - WatchCondition：通过 Bus.BeginConditionWatch + Bus.IsConditionTriggered 轮询
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    [UpdateAfter(typeof(FlowSystem))]
    public class SignalSystem : BlueprintSystemBase, IFrameAware
    {
        internal static readonly NodeStateDescriptor<WaitSignalNodeState> WaitSignalStateDescriptor =
            new(
                "signal.wait-signal.state",
                StateLifetime.Execution,
                static () => new WaitSignalNodeState(),
                debugName: "Signal.WaitSignal State");

        internal static readonly NodeStateDescriptor<InstantEventNodeState> EmitStateDescriptor =
            new(
                "signal.emit.state",
                StateLifetime.Execution,
                static () => new InstantEventNodeState(),
                debugName: "Signal.Emit State");

        internal static readonly NodeStateDescriptor<WatchConditionNodeState> WatchConditionStateDescriptor =
            new(
                "signal.watch-condition.state",
                StateLifetime.Execution,
                static () => new WatchConditionNodeState(),
                debugName: "Signal.WatchCondition State");

        private bool _emitLifecycleBindingRegistered;
        private bool _waitSignalLifecycleBindingRegistered;
        private bool _watchConditionLifecycleBindingRegistered;

        public override string Name => "SignalSystem";

        public BlueprintFrame? Frame { get; set; }

        public override void OnInit(BlueprintFrame frame)
        {
            _emitLifecycleBindingRegistered = false;
            _waitSignalLifecycleBindingRegistered = false;
            _watchConditionLifecycleBindingRegistered = false;
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, EmitStateDescriptor, ref _emitLifecycleBindingRegistered);
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, WaitSignalStateDescriptor, ref _waitSignalLifecycleBindingRegistered);
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, WatchConditionStateDescriptor, ref _watchConditionLifecycleBindingRegistered);
        }

        public override void OnDisabled(BlueprintFrame frame)
        {
            var bus = frame.Runner?.SignalBus;

            var emitIndices = frame.GetActionIndices(AT.Signal.Emit);
            for (var index = 0; index < emitIndices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, emitIndices[index], EmitStateDescriptor);
            }

            var waitSignalIndices = frame.GetActionIndices(AT.Signal.WaitSignal);
            for (var index = 0; index < waitSignalIndices.Count; index++)
            {
                if (WaitSignalExecutionSupport.TryGetWaitSignalState(frame, waitSignalIndices[index], out var waitState))
                {
                    WaitSignalExecutionSupport.ReleaseAuxiliaryState(frame, waitState!);
                }

                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, waitSignalIndices[index], WaitSignalStateDescriptor);
            }

            var watchConditionIndices = frame.GetActionIndices(AT.Signal.WatchCondition);
            for (var index = 0; index < watchConditionIndices.Count; index++)
            {
                if (WatchConditionExecutionSupport.TryGetWatchConditionState(frame, watchConditionIndices[index], out var watchState))
                {
                    WatchConditionExecutionSupport.EndWatch(bus, watchState!);
                    WatchConditionExecutionSupport.ReleaseAuxiliaryState(frame, watchState!);
                }

                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, watchConditionIndices[index], WatchConditionStateDescriptor);
            }
        }

        public override void Update(ref FrameView view)
        {
            ProcessEmit(ref view);
            ProcessWaitSignal(ref view);
            ProcessWatchCondition(ref view);
        }

        // ══════════════════════════════════════════
        //  Signal.Emit 处理
        // ══════════════════════════════════════════

        private void ProcessEmit(ref FrameView view)
        {
            if (Frame == null)
            {
                return;
            }

            var indices = view.Query.GetActionIndices(AT.Signal.Emit);
            if (indices == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, EmitStateDescriptor, ref _emitLifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running) continue;
                SignalEmitExecutionSupport.TryExecute(ref view, Frame, idx, ref state);
            }
        }

        // ══════════════════════════════════════════
        //  Signal.WaitSignal 处理
        // ══════════════════════════════════════════

        private void ProcessWaitSignal(ref FrameView view)
        {
            var indices = view.Query.GetActionIndices(AT.Signal.WaitSignal);
            if (indices == null || Frame == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, WaitSignalStateDescriptor, ref _waitSignalLifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running) continue;
                WaitSignalExecutionSupport.TryExecute(ref view, Frame, idx, ref state);
            }
        }

        // ══════════════════════════════════════════
        //  Signal.WatchCondition 处理
        // ══════════════════════════════════════════

        private void ProcessWatchCondition(ref FrameView view)
        {
            if (Frame == null) return;

            var indices = view.Query.GetActionIndices(AT.Signal.WatchCondition);
            if (indices == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, WatchConditionStateDescriptor, ref _watchConditionLifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running) continue;
                WatchConditionExecutionSupport.TryExecute(Frame, ref view, idx, ref state);
            }
        }

        // ══════════════════════════════════════════
        //  辅助方法
        // ══════════════════════════════════════════

    }
}
