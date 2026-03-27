#nullable enable
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    public readonly struct ActionEventRecordOptions
    {
        public ActionEventRecordOptions(
            SemanticDescriptorSet? semantics,
            string? signalTag = null,
            string? subjectRefSerialized = null,
            string? subjectSummary = null,
            string? instigatorRefSerialized = null,
            string? instigatorSummary = null,
            string? targetRefSerialized = null,
            string? targetSummary = null)
        {
            Semantics = semantics;
            SignalTag = signalTag?.Trim() ?? string.Empty;
            SubjectRefSerialized = subjectRefSerialized?.Trim() ?? string.Empty;
            SubjectSummary = subjectSummary?.Trim() ?? string.Empty;
            InstigatorRefSerialized = instigatorRefSerialized?.Trim() ?? string.Empty;
            InstigatorSummary = instigatorSummary?.Trim() ?? string.Empty;
            TargetRefSerialized = targetRefSerialized?.Trim() ?? string.Empty;
            TargetSummary = targetSummary?.Trim() ?? string.Empty;
        }

        public SemanticDescriptorSet? Semantics { get; }

        public string? SignalTag { get; }

        public string? SubjectRefSerialized { get; }

        public string? SubjectSummary { get; }

        public string? InstigatorRefSerialized { get; }

        public string? InstigatorSummary { get; }

        public string? TargetRefSerialized { get; }

        public string? TargetSummary { get; }
    }

    public static class ActionEventHistoryRuntimeSupport
    {
        public static BlueprintEventContext? CreateActionEventContext(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            string eventKind,
            SignalPayload payload,
            ActionEventRecordOptions options = default)
        {
            if (frame?.Runner == null)
            {
                return null;
            }

            return BlueprintEventContextSemanticUtility.CreateActionEventContext(
                eventKind: eventKind,
                actionId: frame.Actions[actionIndex].Id,
                actionIndex: actionIndex,
                tick: currentTick,
                payload: payload,
                semantics: options.Semantics,
                fallbackSignalTag: options.SignalTag,
                fallbackSubjectRefSerialized: options.SubjectRefSerialized,
                fallbackSubjectSummary: options.SubjectSummary,
                fallbackInstigatorRefSerialized: options.InstigatorRefSerialized,
                fallbackInstigatorSummary: options.InstigatorSummary,
                fallbackTargetRefSerialized: options.TargetRefSerialized,
                fallbackTargetSummary: options.TargetSummary);
        }

        public static void RecordActionEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            string eventKind,
            SignalPayload payload,
            ActionEventRecordOptions options = default)
        {
            var eventContext = CreateActionEventContext(
                frame,
                actionIndex,
                currentTick,
                eventKind,
                payload,
                options);
            SignalExecutionSupportUtility.RecordEvent(frame, eventContext);
        }
    }
}
