#nullable enable
using System.Collections.Generic;
using System.Linq;

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

        public NodeValidationContext(
            string nodeId,
            ActionDefinition definition,
            IReadOnlyCollection<string> connectedPortSemanticIds,
            PropertyBag properties)
        {
            NodeId                   = nodeId;
            Definition               = definition;
            ConnectedPortSemanticIds = connectedPortSemanticIds;
            Properties               = properties;
        }

        /// <summary>判断指定语义 Id 的端口是否已连接</summary>
        public bool IsPortConnected(string portSemanticId)
            => ConnectedPortSemanticIds.Contains(portSemanticId);
    }
}
