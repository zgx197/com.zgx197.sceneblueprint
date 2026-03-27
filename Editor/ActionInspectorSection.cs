#nullable enable
using System;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ActionInspectorSectionAttribute : Attribute
    {
        public ActionInspectorSectionAttribute(int order = 0)
        {
            Order = order;
        }

        public int Order { get; }
    }

    public readonly struct ActionInspectorSectionContext
    {
        public ActionInspectorSectionContext(
            Node node,
            Graph? graph,
            ActionDefinition definition,
            ActionNodeData data,
            IActionRegistry actionRegistry,
            BindingContext? bindingContext,
            bool suppressDefinitionMetadataSection,
            bool suppressDefinitionValidationSection,
            bool suppressDefaultCompilationSection,
            VariableDeclaration[]? variables)
        {
            Node = node;
            Graph = graph;
            Definition = definition;
            Data = data;
            ActionRegistry = actionRegistry;
            BindingContext = bindingContext;
            SuppressDefinitionMetadataSection = suppressDefinitionMetadataSection;
            SuppressDefinitionValidationSection = suppressDefinitionValidationSection;
            SuppressDefaultCompilationSection = suppressDefaultCompilationSection;
            Variables = variables ?? Array.Empty<VariableDeclaration>();
        }

        public Node Node { get; }

        public Graph? Graph { get; }

        public ActionDefinition Definition { get; }

        public ActionNodeData Data { get; }

        public IActionRegistry ActionRegistry { get; }

        public BindingContext? BindingContext { get; }

        public bool SuppressDefinitionMetadataSection { get; }

        public bool SuppressDefinitionValidationSection { get; }

        public bool SuppressDefaultCompilationSection { get; }

        public VariableDeclaration[] Variables { get; }
    }

    public interface IActionInspectorSection
    {
        bool Supports(ActionInspectorSectionContext context);

        bool Draw(ActionInspectorSectionContext context);
    }
}
