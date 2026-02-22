#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace SceneBlueprint.Editor.Analysis
{
    /// <summary>
    /// 一次 BlueprintAnalyzer.Analyze() 的完整结果。
    /// </summary>
    public class AnalysisReport
    {
        public static AnalysisReport Empty { get; } = new(new List<Diagnostic>());

        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public bool HasErrors   => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

        public int ErrorCount   => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

        public AnalysisReport(IReadOnlyList<Diagnostic> diagnostics)
        {
            Diagnostics = diagnostics;
        }
    }
}
