#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Analysis
{
    /// <summary>
    /// 分析上下文——在一次 Analyze() 调用内跨规则共享缓存与辅助数据。
    /// </summary>
    public class AnalysisContext
    {
        public Graph Graph { get; }
        public INodeTypeCatalog TypeProvider { get; }
        /// <summary>SceneBlueprint 业务层 ActionRegistry，用于查询 ActionDefinition（如 Required 端口）</summary>
        public ActionRegistry ActionRegistry { get; }

        /// <summary>子图边界节点 Id 集合（TypeId == "__SubGraphBoundary"），分析时跳过这些节点</summary>
        public HashSet<string> BoundaryNodeIds { get; }

        /// <summary>
        /// 由 ReachabilityRule（SB001）计算后写入，后续规则可直接复用。
        /// null 表示尚未计算。
        /// </summary>
        public HashSet<string>? ReachableNodeIds { get; internal set; }

        public AnalysisContext(Graph graph, INodeTypeCatalog typeProvider, ActionRegistry actionRegistry)
        {
            Graph           = graph;
            TypeProvider    = typeProvider;
            ActionRegistry  = actionRegistry;

            var boundary = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (node.TypeId == SubGraphConstants.BoundaryNodeTypeId)
                    boundary.Add(node.Id);
            }
            BoundaryNodeIds = boundary;
        }

        /// <summary>判断节点是否为非边界的普通业务节点</summary>
        public bool IsBusinessNode(string nodeId) => !BoundaryNodeIds.Contains(nodeId);

        /// <summary>判断节点是否已可达（需在 SB001 执行后调用）</summary>
        public bool IsReachable(string nodeId) => ReachableNodeIds?.Contains(nodeId) ?? false;
    }
}
