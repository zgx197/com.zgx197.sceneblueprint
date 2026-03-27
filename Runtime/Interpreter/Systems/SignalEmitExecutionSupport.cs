#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal static class SignalEmitExecutionSupport
    {
        public static GraphNodeExecutionResult CompletedResult { get; } =
            GraphNodeExecutionTemplate.CreateCompletionResult(
                GraphNodeExecutionResultKind.Completed,
                AT.Signal.Emit,
                ActionPortIds.SignalEmit.Out);

        public static bool TryExecute(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            ref ActionRuntimeState runtimeState)
        {
            var stateAccess = GraphNodeExecutionTemplate.AcquireRunningState(
                frame,
                actionIndex,
                SignalSystem.EmitStateDescriptor,
                ref runtimeState);
            var emitState = stateAccess.State;

            if (stateAccess.CreatedFresh)
            {
                InitializeState(ref view, frame, actionIndex, emitState);
            }

            return GraphNodeExecutionTemplate.TryFinalizeCompletion(
                frame,
                ref view,
                actionIndex,
                ref runtimeState,
                SignalSystem.EmitStateDescriptor,
                EvaluateTick(frame, ref view, actionIndex, emitState));
        }

        public static void InitializeState(
            ref FrameView view,
            BlueprintFrame frame,
            int actionIndex,
            InstantEventNodeState emitState)
        {
            var runtimeConfig = SignalActionRuntimeConfigResolver.ResolveEmit(frame, actionIndex);
            var payload = SignalPayload.Empty;

            GraphNodeExecutionTemplate.InitializeInstantState(
                emitState,
                view.CurrentTick,
                GraphNodeExecutionTemplate.CreatePlanHeader(
                    runtimeConfig.PlanSource,
                    BuildPlanSummary(runtimeConfig),
                    string.Empty),
                BuildExecutionSummary(runtimeConfig),
                AT.Signal.Emit,
                runtimeConfig.SignalTag,
                isTerminal: false,
                BlueprintEventContextSemanticUtility.BuildPayloadSummary(payload));
            emitState.SubjectRefSerialized = runtimeConfig.SubjectRefSerialized;
            emitState.SubjectSummary = runtimeConfig.SubjectSummary;
            emitState.InstigatorRefSerialized = runtimeConfig.InstigatorRefSerialized;
            emitState.InstigatorSummary = runtimeConfig.InstigatorSummary;
            emitState.TargetRefSerialized = runtimeConfig.TargetRefSerialized;
            emitState.TargetSummary = runtimeConfig.TargetSummary;
        }

        public static GraphNodeExecutionResult EvaluateTick(
            BlueprintFrame frame,
            ref FrameView view,
            int actionIndex,
            InstantEventNodeState emitState)
        {
            if (string.IsNullOrEmpty(emitState.EventValue))
            {
                Debug.LogWarning($"[SignalSystem] Signal.Emit (index={actionIndex}) 未配置 signalTag，跳过");
                return CompletedResult;
            }

            var signalTag = new SignalTag(emitState.EventValue);
            var payload = SignalPayload.Empty;
            var runtimeConfig = SignalActionRuntimeConfigResolver.ResolveEmit(frame, actionIndex);
            var eventSemantics = BlueprintEventContextSemanticUtility.BuildEventContextSemantics(
                runtimeConfig.Semantics,
                AT.Signal.Emit,
                signalTag.Path,
                payload,
                runtimeConfig.SubjectRefSerialized,
                runtimeConfig.SubjectSummary,
                runtimeConfig.InstigatorRefSerialized,
                runtimeConfig.InstigatorSummary,
                runtimeConfig.TargetRefSerialized,
                runtimeConfig.TargetSummary);
            var eventContext = ActionEventHistoryRuntimeSupport.CreateActionEventContext(
                frame,
                actionIndex,
                view.CurrentTick,
                AT.Signal.Emit,
                payload,
                new ActionEventRecordOptions(eventSemantics));

            view.Bus?.Emit(signalTag, payload, eventContext);
            SignalExecutionSupportUtility.RecordEvent(frame, eventContext);
            Debug.Log($"[SignalSystem] Signal.Emit (index={actionIndex}) 发射信号: {emitState.EventValue}");
            return CompletedResult;
        }

        private static string BuildPlanSummary(SignalEmitRuntimeConfig runtimeConfig)
        {
            return string.IsNullOrWhiteSpace(runtimeConfig.SignalTag)
                ? $"{runtimeConfig.PlanSource} | 发射未配置的信号"
                : $"{runtimeConfig.PlanSource} | 发射信号 {runtimeConfig.SignalTag}";
        }

        private static string BuildExecutionSummary(SignalEmitRuntimeConfig runtimeConfig)
        {
            var summary = string.IsNullOrWhiteSpace(runtimeConfig.SignalTag)
                ? "发射未配置的信号"
                : $"发射信号 {runtimeConfig.SignalTag}";
            if (!string.IsNullOrWhiteSpace(runtimeConfig.SubjectSummary))
            {
                summary += $" | 主体 {runtimeConfig.SubjectSummary}";
            }

            return summary;
        }
    }
}
