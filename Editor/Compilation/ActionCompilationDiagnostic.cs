#nullable enable
using System;
using SceneBlueprint.Editor.Export;

namespace SceneBlueprint.Editor.Compilation
{
    public enum ActionCompilationDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum ActionCompilationDiagnosticStage
    {
        Unknown,
        DefinitionValidation,
        SemanticNormalization,
        PlanCompilation,
        CompatibilityFallback,
        CompilerException
    }

    /// <summary>
    /// Action compiler 输出的统一诊断结构。
    /// 运行时、导出、Inspector 先共享这一套最小诊断模型。
    /// </summary>
    public sealed class ActionCompilationDiagnostic
    {
        public ActionCompilationDiagnostic(
            ActionCompilationDiagnosticSeverity severity,
            string compilerId,
            string actionId,
            string actionTypeId,
            string code,
            string message,
            ActionCompilationDiagnosticStage stage = ActionCompilationDiagnosticStage.Unknown)
        {
            Severity = severity;
            CompilerId = compilerId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            ActionTypeId = actionTypeId ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Stage = stage;
        }

        public ActionCompilationDiagnosticSeverity Severity { get; }

        public string CompilerId { get; }

        public string ActionId { get; }

        public string ActionTypeId { get; }

        public string Code { get; }

        public string Message { get; }

        public ActionCompilationDiagnosticStage Stage { get; }

        public string FormatMessage()
        {
            var prefix = string.IsNullOrWhiteSpace(CompilerId)
                ? "[ActionCompiler]"
                : $"[ActionCompiler:{CompilerId}]";

            var actionPart = string.IsNullOrWhiteSpace(ActionId)
                ? string.Empty
                : $" actionId={ActionId}";

            var typePart = string.IsNullOrWhiteSpace(ActionTypeId)
                ? string.Empty
                : $", actionType={ActionTypeId}";

            var codePart = string.IsNullOrWhiteSpace(Code)
                ? string.Empty
                : $", code={Code}";

            return $"{prefix}{actionPart}{typePart}{codePart}, message={Message}";
        }

        public ValidationMessage ToValidationMessage()
        {
            var formattedMessage = FormatMessage();
            return Severity switch
            {
                ActionCompilationDiagnosticSeverity.Error => ValidationMessage.Error(formattedMessage),
                ActionCompilationDiagnosticSeverity.Warning => ValidationMessage.Warning(formattedMessage),
                _ => ValidationMessage.Info(formattedMessage)
            };
        }

        public static ActionCompilationDiagnostic Info(
            string compilerId,
            string actionId,
            string actionTypeId,
            string code,
            string message,
            ActionCompilationDiagnosticStage stage = ActionCompilationDiagnosticStage.Unknown)
        {
            return new ActionCompilationDiagnostic(
                ActionCompilationDiagnosticSeverity.Info,
                compilerId,
                actionId,
                actionTypeId,
                code,
                message,
                stage);
        }

        public static ActionCompilationDiagnostic Warning(
            string compilerId,
            string actionId,
            string actionTypeId,
            string code,
            string message,
            ActionCompilationDiagnosticStage stage = ActionCompilationDiagnosticStage.Unknown)
        {
            return new ActionCompilationDiagnostic(
                ActionCompilationDiagnosticSeverity.Warning,
                compilerId,
                actionId,
                actionTypeId,
                code,
                message,
                stage);
        }

        public static ActionCompilationDiagnostic Error(
            string compilerId,
            string actionId,
            string actionTypeId,
            string code,
            string message,
            ActionCompilationDiagnosticStage stage = ActionCompilationDiagnosticStage.Unknown)
        {
            return new ActionCompilationDiagnostic(
                ActionCompilationDiagnosticSeverity.Error,
                compilerId,
                actionId,
                actionTypeId,
                code,
                message,
                stage);
        }
    }
}
