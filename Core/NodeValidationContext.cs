#nullable enable
using System.Collections.Generic;
using System.Linq;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// IActionValidator.Validate() 的调用上下文。
    /// 只携带 Validator 做判断所需的最少信息，不引用任何 Editor 层类型。
    /// </summary>
    public class NodeValidationContext
    {
        /// <summary>Graph 层节点 GUID</summary>
        public string NodeId { get; }

        /// <summary>该节点对应的行动类型定义</summary>
        public ActionDefinition Definition { get; }

        /// <summary>
        /// 已连接端口的 SemanticId 集合。
        /// 通过 semanticId（如 "trueOut"）判断端口是否有连线。
        /// </summary>
        public IReadOnlyCollection<string> ConnectedPortSemanticIds { get; }

        /// <summary>节点当前属性值（PropertyBag）</summary>
        public PropertyBag Properties { get; }

        /// <summary>
        /// 当前蓝图可见的变量声明列表。
        /// 图分析阶段若尚未注入变量表，则为空数组。
        /// </summary>
        public IReadOnlyList<VariableDeclaration> Variables { get; }

        public NodeValidationContext(
            string nodeId,
            ActionDefinition definition,
            IReadOnlyCollection<string> connectedPortSemanticIds,
            PropertyBag properties,
            IReadOnlyList<VariableDeclaration>? variables = null)
        {
            NodeId                   = nodeId;
            Definition               = definition;
            ConnectedPortSemanticIds = connectedPortSemanticIds;
            Properties               = properties;
            Variables                = variables ?? System.Array.Empty<VariableDeclaration>();
        }

        /// <summary>判断指定语义 Id 的端口是否已连接</summary>
        public bool IsPortConnected(string portSemanticId)
            => ConnectedPortSemanticIds.Contains(portSemanticId);

        /// <summary>判断指定图结构语义角色是否至少有一个端口已连接。</summary>
        public bool IsAnyPortConnected(PortGraphRole graphRole)
        {
            var ports = Definition.FindPortsByGraphRole(graphRole);
            for (var index = 0; index < ports.Length; index++)
            {
                if (ConnectedPortSemanticIds.Contains(ports[index].Id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>获取指定图结构语义角色下已连接的端口语义 Id。</summary>
        public string[] GetConnectedPortIdsByRole(PortGraphRole graphRole)
        {
            var ports = Definition.FindPortsByGraphRole(graphRole);
            if (ports.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var result = new List<string>();
            for (var index = 0; index < ports.Length; index++)
            {
                if (ConnectedPortSemanticIds.Contains(ports[index].Id))
                {
                    result.Add(ports[index].Id);
                }
            }

            return result.Count == 0 ? System.Array.Empty<string>() : result.ToArray();
        }

        /// <summary>
        /// 按当前 ActionDefinition.Properties 创建定义驱动 reader。
        /// validator 不再需要各自直接碰 PropertyBag 的原始对象。
        /// </summary>
        public PropertyBagReader CreateBagReader()
            => new PropertyBagReader(Properties, Definition.Properties);
    }
}
