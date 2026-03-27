#nullable enable
using System;
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Export;

namespace SceneBlueprint.Editor.Compilation
{
    public enum ActionCompilationSourceKind
    {
        Manual,
        Export,
        InspectorPreview
    }

    /// <summary>
    /// Action compiler 的统一输入视图。
    /// 第一版先同时承接导出期编译与 Inspector 预览，后续再逐步扩更多来源。
    /// </summary>
    public sealed class ActionCompilationContext
    {
        public const string VariableDeclarationsUserDataKey = "sceneBlueprint.variables";

        public ActionCompilationContext(
            ActionEntry action,
            IActionRegistry actionRegistry,
            float targetTickRate,
            ActionCompilationSourceKind sourceKind = ActionCompilationSourceKind.Manual,
            SceneBlueprintData? blueprintData = null,
            ExportContext? exportContext = null,
            Graph? graph = null,
            BindingContext? bindingContext = null,
            ActionDefinition? definition = null,
            ActionNodeData? nodeData = null,
            PropertyBag? propertyBag = null,
            IReadOnlyDictionary<string, object>? userData = null)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            ActionRegistry = actionRegistry ?? throw new ArgumentNullException(nameof(actionRegistry));
            TargetTickRate = targetTickRate;
            SourceKind = sourceKind;
            BlueprintData = blueprintData;
            ExportContext = exportContext;
            Graph = graph;
            BindingContext = bindingContext;
            Definition = definition;
            NodeData = nodeData;
            PropertyBag = propertyBag;
            UserData = userData ?? new Dictionary<string, object>();
        }

        public ActionEntry Action { get; }

        public string ActionId => Action.Id ?? string.Empty;

        public string ActionTypeId => Action.TypeId ?? string.Empty;

        public IActionRegistry ActionRegistry { get; }

        public float TargetTickRate { get; }

        public ActionCompilationSourceKind SourceKind { get; }

        public SceneBlueprintData? BlueprintData { get; }

        public ExportContext? ExportContext { get; }

        public Graph? Graph { get; }

        public BindingContext? BindingContext { get; }

        public ActionDefinition? Definition { get; }

        public ActionNodeData? NodeData { get; }

        public PropertyBag? PropertyBag { get; }

        public IReadOnlyDictionary<string, object> UserData { get; }

        public bool TryGetUserData<T>(string key, out T value)
        {
            if (UserData.TryGetValue(key, out var rawValue) && rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default!;
            return false;
        }

        public static ActionCompilationContext ForExport(
            ExportContext exportContext,
            ActionEntry action,
            float targetTickRate)
        {
            if (exportContext == null)
            {
                throw new ArgumentNullException(nameof(exportContext));
            }

            return new ActionCompilationContext(
                action,
                exportContext.Registry,
                targetTickRate,
                ActionCompilationSourceKind.Export,
                blueprintData: exportContext.Data,
                exportContext: exportContext,
                userData: exportContext.UserData);
        }

        public static ActionCompilationContext ForInspectorPreview(
            ActionEntry action,
            IActionRegistry actionRegistry,
            float targetTickRate,
            Graph? graph = null,
            BindingContext? bindingContext = null,
            ActionDefinition? definition = null,
            ActionNodeData? nodeData = null,
            PropertyBag? propertyBag = null,
            IReadOnlyDictionary<string, object>? userData = null)
        {
            return new ActionCompilationContext(
                action,
                actionRegistry,
                targetTickRate,
                ActionCompilationSourceKind.InspectorPreview,
                graph: graph,
                bindingContext: bindingContext,
                definition: definition,
                nodeData: nodeData,
                propertyBag: propertyBag,
                userData: userData);
        }
    }
}
