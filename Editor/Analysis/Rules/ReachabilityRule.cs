#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;

namespace SceneBlueprint.Editor.Analysis.Rules
{
    /// <summary>
    /// SB001：检查节点可达性。
    /// 从所有 Flow.Start 节点出发，沿控制流边（PortKind.Control）做 BFS，
    /// 未被访问到的非边界节点视为不可达（Error）。
    ///
    /// 副作用：将可达节点集合写入 ctx.ReachableNodeIds，供后续规则（SB002、SB005）复用。
    /// </summary>
    public class ReachabilityRule : IBlueprintRule
    {
        public string RuleId => "SB001";

        private const string FlowStartTypeId = "Flow.Start";

        public IEnumerable<Diagnostic> Check(AnalysisContext ctx)
        {
            var graph = ctx.Graph;

            // ── 1. 构建 portId → nodeId 快速查找表 ──
            var portToNode = new Dictionary<string, string>();
            foreach (var node in graph.Nodes)
                foreach (var port in node.Ports)
                    portToNode[port.Id] = node.Id;

            // ── 2. 构建 nodeId → 出边目标 nodeId（仅 Control 边）──
            var controlNeighbors = new Dictionary<string, List<string>>();
            foreach (var node in graph.Nodes)
                controlNeighbors[node.Id] = new List<string>();

            foreach (var edge in graph.Edges)
            {
                var srcPort = graph.FindPort(edge.SourcePortId);
                var tgtPort = graph.FindPort(edge.TargetPortId);
                if (srcPort == null || tgtPort == null) continue;
                if (srcPort.Kind != PortKind.Control) continue;

                if (!controlNeighbors.ContainsKey(srcPort.NodeId))
                    controlNeighbors[srcPort.NodeId] = new List<string>();
                controlNeighbors[srcPort.NodeId].Add(tgtPort.NodeId);
            }

            // ── 3. BFS 从所有 Flow.Start 出发 ──
            var visited = new HashSet<string>();
            var queue   = new Queue<string>();

            foreach (var node in graph.Nodes)
            {
                if (node.TypeId == FlowStartTypeId && ctx.IsBusinessNode(node.Id))
                {
                    if (visited.Add(node.Id))
                        queue.Enqueue(node.Id);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!controlNeighbors.TryGetValue(current, out var neighbors)) continue;

                foreach (var neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            // ── 4. 写入上下文供后续规则复用 ──
            ctx.ReachableNodeIds = visited;

            // ── 5. 报告不可达节点 ──
            foreach (var node in graph.Nodes)
            {
                if (!ctx.IsBusinessNode(node.Id)) continue;
                if (node.TypeId == FlowStartTypeId) continue;
                if (visited.Contains(node.Id)) continue;

                var typeIdHint = (node.UserData as SceneBlueprint.Core.ActionNodeData)?.ActionTypeId
                    ?? node.TypeId;
                yield return Diagnostic.Error(RuleId,
                    $"节点 '{typeIdHint}' 从 Flow.Start 不可达", node.Id);
            }
        }
    }
}
