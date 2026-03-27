#nullable enable
using System;
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// Inspector override 侧共用的图结构查询辅助。
    /// 让 editor 主编辑区也能像 validator 一样按 graph role 读取已连接端口，
    /// 避免每个 override 各自扫描 graph edges。
    /// </summary>
    public static class ActionInspectorGraphUtility
    {
        public static bool IsAnyPortConnected(ActionInspectorPropertyContext context, PortGraphRole graphRole)
        {
            return IsAnyPortConnected(context.OwnerContext, context.OwnerNodeId, graphRole);
        }

        public static bool IsAnyPortConnected(ActionInspectorOverrideContext context, string ownerNodeId, PortGraphRole graphRole)
        {
            return GetConnectedPortIdsByRole(context, ownerNodeId, graphRole).Length > 0;
        }

        public static string[] GetConnectedPortIdsByRole(ActionInspectorPropertyContext context, PortGraphRole graphRole)
        {
            return GetConnectedPortIdsByRole(context.OwnerContext, context.OwnerNodeId, graphRole);
        }

        public static string[] GetConnectedPortIdsByRole(ActionInspectorOverrideContext context, string ownerNodeId, PortGraphRole graphRole)
        {
            if (context.Graph == null
                || string.IsNullOrWhiteSpace(ownerNodeId)
                || graphRole == PortGraphRole.None)
            {
                return Array.Empty<string>();
            }

            var ports = context.Definition.FindPortsByGraphRole(graphRole);
            if (ports.Length == 0)
            {
                return Array.Empty<string>();
            }

            var connectedPortIds = CollectConnectedPortSemanticIds(context.Graph, ownerNodeId);
            if (connectedPortIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            for (var index = 0; index < ports.Length; index++)
            {
                var semanticId = ports[index].Id;
                if (!string.IsNullOrWhiteSpace(semanticId)
                    && connectedPortIds.Contains(semanticId))
                {
                    result.Add(semanticId);
                }
            }

            return result.Count == 0 ? Array.Empty<string>() : result.ToArray();
        }

        private static HashSet<string> CollectConnectedPortSemanticIds(Graph graph, string ownerNodeId)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in graph.Edges)
            {
                var sourcePort = graph.FindPort(edge.SourcePortId);
                if (sourcePort != null
                    && string.Equals(sourcePort.NodeId, ownerNodeId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(sourcePort.SemanticId))
                {
                    result.Add(sourcePort.SemanticId);
                }

                var targetPort = graph.FindPort(edge.TargetPortId);
                if (targetPort != null
                    && string.Equals(targetPort.NodeId, ownerNodeId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(targetPort.SemanticId))
                {
                    result.Add(targetPort.SemanticId);
                }
            }

            return result;
        }
    }
}
