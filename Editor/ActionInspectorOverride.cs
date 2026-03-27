#nullable enable
using System;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Editor
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ActionInspectorOverrideAttribute : Attribute
    {
        public ActionInspectorOverrideAttribute(int order = 0)
        {
            Order = order;
        }

        public int Order { get; }
    }

    public readonly struct ActionInspectorOverrideContext
    {
        public ActionInspectorOverrideContext(
            Node node,
            Graph? graph,
            ActionDefinition definition,
            ActionNodeData data,
            IActionRegistry actionRegistry,
            BindingContext? bindingContext,
            VariableDeclaration[]? variables)
        {
            Node = node;
            Graph = graph;
            Definition = definition;
            Data = data;
            ActionRegistry = actionRegistry;
            BindingContext = bindingContext;
            Variables = variables ?? Array.Empty<VariableDeclaration>();
        }

        public Node Node { get; }

        public Graph? Graph { get; }

        public ActionDefinition Definition { get; }

        public ActionNodeData Data { get; }

        public IActionRegistry ActionRegistry { get; }

        public BindingContext? BindingContext { get; }

        public VariableDeclaration[] Variables { get; }
    }

    public readonly struct ActionInspectorPropertyContext
    {
        public ActionInspectorPropertyContext(
            ActionInspectorOverrideContext ownerContext,
            PropertyDefinition property,
            PropertyBag bag,
            string ownerNodeId)
        {
            OwnerContext = ownerContext;
            Property = property;
            Bag = bag;
            OwnerNodeId = ownerNodeId;
        }

        public ActionInspectorOverrideContext OwnerContext { get; }

        public PropertyDefinition Property { get; }

        public PropertyBag Bag { get; }

        public string OwnerNodeId { get; }
    }

    public interface IActionInspectorOverride
    {
        bool Supports(ActionInspectorOverrideContext context);

        bool TryDrawProperty(ActionInspectorPropertyContext context, out bool changed);
    }

    /// <summary>
    /// 可选的 override 元数据接口。
    /// 当某个 override 已经在主编辑区接管编译摘要展示时，可要求默认编译摘要段让开，避免重复渲染。
    /// </summary>
    public interface IActionInspectorOverrideMetadata
    {
        bool SuppressDefaultCompilationSection { get; }

        bool SuppressDefinitionMetadataSection { get; }

        bool SuppressDefinitionValidationSection { get; }
    }

    /// <summary>
    /// 带上下文的 override 元数据解析器。
    /// 允许单个 override 在承接多类 action type 时，按当前节点上下文动态决定
    /// 是否抑制默认编译摘要段，而不必再为少量差异保留多份 override 轻壳。
    /// </summary>
    public interface IActionInspectorOverrideMetadataResolver
    {
        bool SuppressDefaultCompilationSection(ActionInspectorOverrideContext context);

        bool SuppressDefinitionMetadataSection(ActionInspectorOverrideContext context);

        bool SuppressDefinitionValidationSection(ActionInspectorOverrideContext context);
    }
}
