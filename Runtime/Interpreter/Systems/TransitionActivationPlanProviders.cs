#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal interface ITransitionActivationPlanProvider
    {
        bool TryResolve(BlueprintFrame? frame, FrameView view, int actionIndex, out TransitionActivationRuntimePlan runtimePlan);

        bool TryConsume(
            BlueprintFrame? frame,
            ref FrameView view,
            PortEvent ev,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            TransitionActivationRuntimePlan runtimePlan);
    }

    internal sealed class FlowJoinTransitionActivationPlanProvider : ITransitionActivationPlanProvider
    {
        public static FlowJoinTransitionActivationPlanProvider Instance { get; } = new();

        private FlowJoinTransitionActivationPlanProvider()
        {
        }

        public bool TryResolve(BlueprintFrame? frame, FrameView view, int actionIndex, out TransitionActivationRuntimePlan runtimePlan)
        {
            if (view.Query.GetTypeId(actionIndex) != AT.Flow.Join)
            {
                runtimePlan = default;
                return false;
            }

            var joinPlan = FlowJoinRuntimePlanResolver.ResolveForActivation(frame, actionIndex, 1);
            runtimePlan = new TransitionActivationRuntimePlan(
                view.Query.GetTypeId(actionIndex),
                joinPlan.PlanSource,
                joinPlan.PlanSummary,
                joinPlan.RequiredCount,
                joinPlan.GraphDescriptor,
                this);
            return true;
        }

        public bool TryConsume(
            BlueprintFrame? frame,
            ref FrameView view,
            PortEvent ev,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            TransitionActivationRuntimePlan runtimePlan)
        {
            JoinNodeState? joinState = null;
            if (frame != null)
            {
                FlowJoinExecutionSupport.TryAccumulateArrival(
                    frame,
                    actionIndex,
                    ref runtimeState,
                    view.CurrentTick,
                    runtimePlan.RequiredCount,
                    out joinState);
            }

            if (joinState == null)
            {
                runtimeState.CustomInt0++;
            }

            var progress = GraphNodeExecutionTemplate.CreateActivationProgress(
                joinState?.ReceivedCount ?? runtimeState.CustomInt0,
                runtimePlan.RequiredCount,
                runtimePlan.PlanSummary,
                runtimePlan.GraphDescriptor);
            var activationStep = GraphNodeExecutionTemplate.ApplyBarrierActivation(ref runtimeState, progress);
            TransitionActivationRuntimeSupport.LogBarrierActivation(frame, ref view, actionIndex, activationStep);
            return true;
        }
    }

    internal sealed class CompositeConditionTransitionActivationPlanProvider : ITransitionActivationPlanProvider
    {
        public static CompositeConditionTransitionActivationPlanProvider Instance { get; } = new();

        private CompositeConditionTransitionActivationPlanProvider()
        {
        }

        public bool TryResolve(BlueprintFrame? frame, FrameView view, int actionIndex, out TransitionActivationRuntimePlan runtimePlan)
        {
            if (view.Query.GetTypeId(actionIndex) != AT.Signal.CompositeCondition)
            {
                runtimePlan = default;
                return false;
            }

            var compositePlan = CompositeConditionRuntimePlanResolver.ResolveForActivation(frame, actionIndex);
            runtimePlan = new TransitionActivationRuntimePlan(
                view.Query.GetTypeId(actionIndex),
                compositePlan.PlanSource,
                compositePlan.PlanSummary,
                1,
                compositePlan.GraphDescriptor,
                this);
            return true;
        }

        public bool TryConsume(
            BlueprintFrame? frame,
            ref FrameView view,
            PortEvent ev,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            TransitionActivationRuntimePlan runtimePlan)
        {
            if (frame == null || runtimeState.Phase != ActionPhase.Running)
            {
                return false;
            }

            return CompositeConditionExecutionSupport.TryAccumulateTriggeredMask(
                frame,
                actionIndex,
                ref runtimeState,
                ev.ToPortHash);
        }
    }
}
