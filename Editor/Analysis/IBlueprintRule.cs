#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Editor.Analysis
{
    /// <summary>
    /// 单条蓝图分析规则接口。
    /// 每个实现类对应一类静态分析检查（如 SB001 不可达节点）。
    /// </summary>
    public interface IBlueprintRule
    {
        /// <summary>规则唯一编号，如 "SB001"</summary>
        string RuleId { get; }

        /// <summary>
        /// 执行规则检查，返回本条规则产生的所有诊断结果。
        /// 若无问题则返回空集合（不返回 null）。
        /// </summary>
        IEnumerable<Diagnostic> Check(AnalysisContext ctx);
    }
}
