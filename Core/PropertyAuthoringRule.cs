#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 属性级 authoring 上下文。
    /// 这是 definition 层进行字段规范化与字段级校验的正式输入。
    /// </summary>
    public readonly struct PropertyAuthoringContext
    {
        public PropertyAuthoringContext(
            string nodeId,
            ActionDefinition definition,
            PropertyDefinition property,
            PropertyBag bag,
            IReadOnlyCollection<string>? connectedPortSemanticIds = null,
            IReadOnlyList<VariableDeclaration>? variables = null)
        {
            NodeId = nodeId ?? string.Empty;
            Definition = definition;
            Property = property;
            Bag = bag;
            ConnectedPortSemanticIds = connectedPortSemanticIds ?? System.Array.Empty<string>();
            Variables = variables ?? System.Array.Empty<VariableDeclaration>();
        }

        public string NodeId { get; }

        public ActionDefinition Definition { get; }

        public PropertyDefinition Property { get; }

        public PropertyBag Bag { get; }

        public IReadOnlyCollection<string> ConnectedPortSemanticIds { get; }

        public IReadOnlyList<VariableDeclaration> Variables { get; }

        public PropertyBagReader CreateBagReader()
            => new PropertyBagReader(Bag, Definition.Properties);
    }

    /// <summary>
    /// 属性级 authoring 规则。
    /// 当前承担两类职责：
    /// 1. 提供安全的 canonical 规范化值。
    /// 2. 提供字段级 validation issue。
    /// </summary>
    public interface IPropertyAuthoringRule
    {
        bool TryNormalize(PropertyAuthoringContext context, object? currentValue, out object? normalizedValue);

        IEnumerable<ValidationIssue> Validate(PropertyAuthoringContext context);
    }

    /// <summary>
    /// 为 PropertyDefinition 快照提供规则实例复制语义。
    /// 若规则对象包含可变状态，必须通过该接口返回独立快照；
    /// 无状态规则也建议实现该接口，避免未来扩展时意外共享行为实例。
    /// </summary>
    public interface IPropertyAuthoringRuleSnapshotProvider
    {
        IPropertyAuthoringRule CreateSnapshotRule();
    }
}
