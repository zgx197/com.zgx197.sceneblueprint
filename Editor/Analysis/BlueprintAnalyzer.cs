#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Analysis
{
    /// <summary>
    /// 蓝图静态分析器（方案 C+B 的 Analyze Phase 实现）。
    /// 组合多条 IBlueprintRule，对 Graph 进行静态分析并返回 AnalysisReport。
    ///
    /// 使用方式：
    /// <code>
    /// var analyzer = SceneBlueprintProfile.CreateAnalyzer(typeProvider, actionRegistry);
    /// var report = analyzer.Analyze(graph);
    /// if (report.HasErrors) { /* 阻断导出 */ }
    /// </code>
    /// </summary>
    public class BlueprintAnalyzer
    {
        private readonly List<IBlueprintRule> _rules = new();
        private readonly INodeTypeCatalog _typeProvider;
        private readonly ActionRegistry _actionRegistry;

        public BlueprintAnalyzer(INodeTypeCatalog typeProvider, ActionRegistry actionRegistry)
        {
            _typeProvider   = typeProvider;
            _actionRegistry = actionRegistry;
        }

        /// <summary>链式添加规则，返回自身以支持 Fluent 写法</summary>
        public BlueprintAnalyzer AddRule(IBlueprintRule rule)
        {
            _rules.Add(rule);
            return this;
        }

        /// <summary>
        /// 对 graph 执行所有已注册规则，返回分析报告。
        /// 规则按注册顺序依次执行，前序规则写入的 ctx 缓存（如 ReachableNodeIds）可被后续规则复用。
        /// </summary>
        public AnalysisReport Analyze(Graph graph)
        {
            var ctx = new AnalysisContext(graph, _typeProvider, _actionRegistry);
            var diagnostics = new List<Diagnostic>();

            foreach (var rule in _rules)
            {
                foreach (var diag in rule.Check(ctx))
                    diagnostics.Add(diag);
            }

            return new AnalysisReport(diagnostics);
        }
    }
}
