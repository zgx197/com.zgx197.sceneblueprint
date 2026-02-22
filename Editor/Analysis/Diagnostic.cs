#nullable enable

namespace SceneBlueprint.Editor.Analysis
{
    /// <summary>
    /// 单条分析诊断结果。
    /// NodeId 为 null 表示图级问题；PortId 为 null 表示节点级问题。
    /// </summary>
    public class Diagnostic
    {
        public DiagnosticSeverity Severity { get; }
        /// <summary>规则编号，如 "SB001"</summary>
        public string Code { get; }
        public string Message { get; }
        /// <summary>关联的节点 Id（Graph 层 GUID）；null 表示图级问题</summary>
        public string? NodeId { get; }
        /// <summary>关联的端口语义 Id；null 表示节点级问题</summary>
        public string? PortId { get; }

        private Diagnostic(DiagnosticSeverity severity, string code, string message,
            string? nodeId = null, string? portId = null)
        {
            Severity = severity;
            Code     = code;
            Message  = message;
            NodeId   = nodeId;
            PortId   = portId;
        }

        public static Diagnostic Error(string code, string message,
            string? nodeId = null, string? portId = null)
            => new(DiagnosticSeverity.Error, code, message, nodeId, portId);

        public static Diagnostic Warning(string code, string message,
            string? nodeId = null, string? portId = null)
            => new(DiagnosticSeverity.Warning, code, message, nodeId, portId);

        public static Diagnostic Info(string code, string message,
            string? nodeId = null, string? portId = null)
            => new(DiagnosticSeverity.Info, code, message, nodeId, portId);

        public override string ToString() => $"[{Code}] {Severity}: {Message}";
    }
}
