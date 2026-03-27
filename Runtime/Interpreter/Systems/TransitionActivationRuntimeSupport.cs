#nullable enable
using System;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal readonly struct TransitionActivationRuntimePlan
    {
        public TransitionActivationRuntimePlan(
            string actionTypeId,
            string planSource,
            string planSummary,
            int requiredCount,
            GraphSemanticDescriptor? graphDescriptor,
            ITransitionActivationPlanProvider? provider = null)
        {
            ActionTypeId = actionTypeId ?? string.Empty;
            PlanSource = planSource ?? string.Empty;
            PlanSummary = planSummary ?? string.Empty;
            RequiredCount = Math.Max(1, requiredCount);
            GraphDescriptor = graphDescriptor;
            Provider = provider;
        }

        public string ActionTypeId { get; }

        public string PlanSource { get; }

        public string PlanSummary { get; }

        public int RequiredCount { get; }

        public GraphSemanticDescriptor? GraphDescriptor { get; }

        public ITransitionActivationPlanProvider? Provider { get; }
    }

    /// <summary>
    /// 把 Transition 阶段仍保留的特殊激活规则压回统一 support。
    /// TransitionSystem 主循环只负责消费 runtime activation plan，不再直接分发节点类型级特判。
    /// </summary>
    internal static class TransitionActivationRuntimeSupport
    {
        private static readonly ITransitionActivationPlanProvider[] PlanProviders =
        {
            FlowJoinTransitionActivationPlanProvider.Instance,
            CompositeConditionTransitionActivationPlanProvider.Instance,
        };

        public static TransitionActivationRuntimePlan ResolvePlan(BlueprintFrame? frame, ref FrameView view, int actionIndex)
        {
            var typeId = view.Query.GetTypeId(actionIndex);
            for (var index = 0; index < PlanProviders.Length; index++)
            {
                if (PlanProviders[index].TryResolve(frame, view, actionIndex, out var runtimePlan))
                {
                    return runtimePlan;
                }
            }

            return new TransitionActivationRuntimePlan(
                typeId,
                string.Empty,
                string.Empty,
                1,
                null);
        }

        public static bool TryConsumeSpecialActivation(
            BlueprintFrame? frame,
            ref FrameView view,
            PortEvent ev,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            TransitionActivationRuntimePlan runtimePlan)
        {
            return runtimePlan.Provider != null
                && runtimePlan.Provider.TryConsume(
                    frame,
                    ref view,
                    ev,
                    actionIndex,
                    ref runtimeState,
                    runtimePlan);
        }

        public static void ConsumeEvent(
            BlueprintFrame? frame,
            ref FrameView view,
            PortEvent ev)
        {
            var actionIndex = ev.ToActionIndex;
            if (actionIndex < 0 || actionIndex >= view.ActionCount)
            {
                return;
            }

            ref var runtimeState = ref view.States[actionIndex];
            var activationPlan = ResolvePlan(frame, ref view, actionIndex);
            if (TryConsumeSpecialActivation(
                    frame,
                    ref view,
                    ev,
                    actionIndex,
                    ref runtimeState,
                    activationPlan))
            {
                return;
            }

            var activationStep = GraphNodeExecutionTemplate.TryActivateFromTransition(ref runtimeState);
            if (!activationStep.IsRunningTransition)
            {
                return;
            }

            TrackActivatedBy(frame, ev.FromActionIndex, actionIndex);
            LogDirectActivation(frame, ref view, ev, actionIndex, activationStep);
        }

        internal static string DescribeActivationTarget(BlueprintFrame? frame, ref FrameView view, int actionIndex)
        {
            var typeId = view.Query.GetTypeId(actionIndex);
            if (frame == null || actionIndex < 0 || actionIndex >= frame.Actions.Length)
            {
                return $"{typeId} (index={actionIndex})";
            }

            return $"{typeId} (index={actionIndex}, actionId={frame.Actions[actionIndex].Id})";
        }

        internal static void LogBarrierActivation(
            BlueprintFrame? frame,
            ref FrameView view,
            int actionIndex,
            GraphNodeActivationStep activationStep)
        {
            var progress = activationStep.Progress;
            switch (activationStep.Kind)
            {
                case GraphNodeActivationStepKind.PromotedToRunning:
                    Debug.Log(
                        $"[TransitionSystem] {DescribeActivationTarget(frame, ref view, actionIndex)} 收齐 {progress.CurrentCount}/{progress.RequiredCount} → Running{GraphNodeExecutionTemplate.BuildPlanSummarySuffix(progress.PlanSummary)}");
                    break;

                case GraphNodeActivationStepKind.MovedToWaiting:
                case GraphNodeActivationStepKind.Unchanged:
                    Debug.Log(
                        $"[TransitionSystem] {DescribeActivationTarget(frame, ref view, actionIndex)} 等待 {progress.CurrentCount}/{progress.RequiredCount}{GraphNodeExecutionTemplate.BuildPlanSummarySuffix(progress.PlanSummary)}");
                    break;
            }
        }

        private static void TrackActivatedBy(BlueprintFrame? frame, int fromIdx, int toIdx)
        {
            if (frame == null) return;
            var sourceActionId = frame.Actions[fromIdx].Id;
            var targetActionId = frame.Actions[toIdx].Id;
            frame.Blackboard.SetInternal($"_activatedBy.{targetActionId}", sourceActionId);
        }

        private static void LogDirectActivation(
            BlueprintFrame? frame,
            ref FrameView view,
            PortEvent ev,
            int actionIndex,
            GraphNodeActivationStep activationStep)
        {
            var wasReactivated = activationStep.WasReactivated;
            var actionLabel = wasReactivated ? "重激活节点" : "激活节点";
            var target = DescribeActivationTarget(frame, ref view, actionIndex);

            if (!BlueprintRuntimeSettings.Instance.EnableTransitionDetailLogs || frame == null)
            {
                Debug.Log($"[TransitionSystem] {actionLabel} {target} {(wasReactivated ? "Listening" : "Idle")} → Running");
                return;
            }

            var fromIdx = ev.FromActionIndex;
            var fromTypeId = fromIdx >= 0 && fromIdx < frame.Actions.Length
                ? frame.Actions[fromIdx].TypeId
                : "<unknown>";
            var fromActionId = fromIdx >= 0 && fromIdx < frame.Actions.Length
                ? frame.Actions[fromIdx].Id
                : "<unknown>";
            var fromPort = ev.DebugFromPortId ?? ev.FromPortHash.ToString();
            var toPort = ev.DebugToPortId ?? ev.ToPortHash.ToString();

            Debug.Log(
                $"[TransitionSystem] {actionLabel} {target} {(wasReactivated ? "Listening" : "Idle")} → Running " +
                $"(tick={view.CurrentTick}, from={fromTypeId}[index={fromIdx}, actionId={fromActionId}], port={fromPort} → {toPort})");
        }
    }
}
