#nullable enable
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 收口 definition 层 OutputVariables 的 editor/export 消费逻辑。
    /// 避免黑板面板、导出器、后续更多 editor helper 各自再实现一份
    /// “扫描节点定义 -> 去重 -> 分配稳定合成 Index” 的局部协议。
    /// </summary>
    internal static class OutputVariableDeclarationSupport
    {
        public static List<VariableDeclaration> CollectDeclaredOutputVariables(
            Graph? graph,
            ActionRegistry registry)
        {
            var result = new List<VariableDeclaration>();
            if (graph == null)
            {
                return result;
            }

            var seen = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data)
                {
                    continue;
                }

                if (!registry.TryGet(data.ActionTypeId, out var definition))
                {
                    continue;
                }

                var outputVariables = definition.GetDeclaredOutputVariables();
                for (var index = 0; index < outputVariables.Length; index++)
                {
                    var outputVariable = outputVariables[index];
                    if (string.IsNullOrWhiteSpace(outputVariable.Name)
                        || !seen.Add(outputVariable.Name))
                    {
                        continue;
                    }

                    result.Add(new VariableDeclaration
                    {
                        Index = BuildSyntheticIndex(outputVariable.Name),
                        Name = outputVariable.Name,
                        Type = outputVariable.Type,
                        Scope = outputVariable.Scope,
                        InitialValue = string.Empty
                    });
                }
            }

            return result;
        }

        public static VariableDeclaration[] MergeDeclaredOutputVariables(
            VariableDeclaration[]? userVariables,
            Graph? graph,
            ActionRegistry registry)
        {
            var result = new List<VariableDeclaration>(userVariables ?? System.Array.Empty<VariableDeclaration>());
            var seen = new HashSet<string>(result.Select(variable => variable.Name));
            var declaredVariables = CollectDeclaredOutputVariables(graph, registry);
            for (var index = 0; index < declaredVariables.Count; index++)
            {
                var variable = declaredVariables[index];
                if (!seen.Add(variable.Name))
                {
                    continue;
                }

                result.Add(variable);
            }

            return result.ToArray();
        }

        /// <summary>
        /// DJB2 hash of name -> 10000-19999.
        /// 与 editor/export 两侧保持同一套稳定索引约定。
        /// </summary>
        public static int BuildSyntheticIndex(string name)
        {
            uint hash = 5381;
            foreach (var character in name)
            {
                hash = ((hash << 5) + hash) + character;
            }

            return 10000 + (int)(hash % 10000);
        }
    }
}
