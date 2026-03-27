#nullable enable
using System;
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Settings;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;
using UnityEditor;

namespace SceneBlueprint.Editor.Compilation
{
    /// <summary>
    /// Inspector 侧 action 编译预览公共辅助。
    /// 把“临时 ActionEntry 构建 + ForInspectorPreview 上下文拼装”统一收口，
    /// 避免业务节点各自复制一套预览编译接线。
    /// </summary>
    internal static class ActionCompilationPreviewSupport
    {
        public static ActionCompilationContext CreateInspectorPreviewContext(
            string nodeId,
            string actionTypeId,
            ActionDefinition definition,
            IActionRegistry actionRegistry,
            PropertyBag propertyBag,
            float targetTickRate,
            Graph? graph,
            BindingContext? bindingContext,
            ActionNodeData? nodeData,
            IReadOnlyDictionary<string, object>? userData)
        {
            var action = BuildTemporaryActionEntry(
                nodeId,
                actionTypeId,
                definition,
                propertyBag,
                bindingContext);

            return ActionCompilationContext.ForInspectorPreview(
                action,
                actionRegistry,
                targetTickRate,
                graph: graph,
                bindingContext: bindingContext,
                definition: definition,
                nodeData: nodeData,
                propertyBag: propertyBag,
                userData: userData);
        }

        public static bool TryResolveCompiler(
            string nodeId,
            string actionTypeId,
            ActionDefinition definition,
            IActionRegistry actionRegistry,
            PropertyBag propertyBag,
            float targetTickRate,
            Graph? graph,
            BindingContext? bindingContext,
            ActionNodeData? nodeData,
            IReadOnlyDictionary<string, object>? userData,
            out ActionCompilationContext compilationContext,
            out IActionCompiler compiler)
        {
            compilationContext = CreateInspectorPreviewContext(
                nodeId,
                actionTypeId,
                definition,
                actionRegistry,
                propertyBag,
                targetTickRate,
                graph,
                bindingContext,
                nodeData,
                userData);

            return ActionCompilerRegistry.Default.TryResolve(compilationContext, out compiler);
        }

        public static bool TryCompile(
            string nodeId,
            string actionTypeId,
            ActionDefinition definition,
            IActionRegistry actionRegistry,
            PropertyBag propertyBag,
            float targetTickRate,
            Graph? graph,
            BindingContext? bindingContext,
            ActionNodeData? nodeData,
            IReadOnlyDictionary<string, object>? userData,
            out ActionCompilationArtifact artifact,
            out string failureMessage,
            out MessageType failureType)
        {
            var compilationContext = CreateInspectorPreviewContext(
                nodeId,
                actionTypeId,
                definition,
                actionRegistry,
                propertyBag,
                targetTickRate,
                graph,
                bindingContext,
                nodeData,
                userData);

            if (!ActionCompilerRegistry.Default.TryCompile(compilationContext, out artifact))
            {
                failureMessage = "当前节点未找到可用的编译器。";
                failureType = MessageType.Warning;
                return false;
            }

            failureMessage = string.Empty;
            failureType = MessageType.Info;
            return true;
        }

        public static IReadOnlyDictionary<string, object>? BuildVariableUserData(VariableDeclaration[]? variables)
        {
            Dictionary<string, object>? userData = null;

            if (variables != null && variables.Length > 0)
            {
                userData ??= new Dictionary<string, object>(2);
                userData[ActionCompilationContext.VariableDeclarationsUserDataKey] = variables;
            }

            var levelId = SceneBlueprintWindow.GetCurrentLevelId();
            if (levelId > 0)
            {
                userData ??= new Dictionary<string, object>(2);
                userData[ActionCompilationUserDataKeys.LevelId] = levelId;
                userData[ActionCompilationUserDataKeys.MonsterMappingSnapshot] = SceneBlueprintSettingsService.MonsterMapping;
            }

            return userData;
        }

        public static bool TryGetFailureMessage(
            ActionCompilationArtifact artifact,
            out string failureMessage,
            out MessageType failureType)
        {
            if (artifact.Diagnostics.Length > 0)
            {
                failureMessage = artifact.Diagnostics[0].Message;
                failureType = artifact.Diagnostics[0].Severity == ActionCompilationDiagnosticSeverity.Error
                    ? MessageType.Error
                    : artifact.Diagnostics[0].Severity == ActionCompilationDiagnosticSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                return true;
            }

            failureMessage = "当前节点未产出可供 Inspector 展示的编译结果。";
            failureType = MessageType.Warning;
            return false;
        }

        public static ActionEntry BuildTemporaryActionEntry(
            string nodeId,
            string actionTypeId,
            ActionDefinition definition,
            PropertyBag propertyBag,
            BindingContext? bindingContext)
        {
            var sceneBindings = new List<SceneBindingEntry>();
            var declaredBindings = SceneBindingDeclarationSupport.CollectDeclaredBindings(definition, propertyBag);
            for (var index = 0; index < declaredBindings.Count; index++)
            {
                var binding = BuildSceneBindingEntry(
                    nodeId,
                    actionTypeId,
                    bindingContext,
                    declaredBindings[index]);
                if (binding != null)
                {
                    sceneBindings.Add(binding);
                }
            }

            var definitions = definition.Properties ?? Array.Empty<PropertyDefinition>();

            return new ActionEntry
            {
                Id = nodeId,
                TypeId = actionTypeId,
                Properties = PropertyDefinitionValueUtility.BuildSerializedPropertyValues(
                    definitions,
                    propertyBag,
                    static property => property.Type != PropertyType.SceneBinding),
                SceneBindings = sceneBindings.ToArray(),
            };
        }

        private static SceneBindingEntry? BuildSceneBindingEntry(
            string nodeId,
            string actionTypeId,
            BindingContext? bindingContext,
            SceneBindingDeclarationSupport.DeclaredSceneBindingValue declaration)
        {
            var rawValue = declaration.RawValue;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var binding = new SceneBindingEntry
            {
                BindingKey = declaration.BindingKey,
                BindingType = declaration.BindingType,
                SceneObjectId = rawValue,
                StableObjectId = rawValue,
                AdapterType = "Unity3D",
                SpatialPayloadJson = "{}",
                SourceActionTypeId = actionTypeId,
            };

            if (bindingContext == null)
            {
                return binding;
            }

            var boundObject = bindingContext.Get(BuildScopedBindingKey(nodeId, declaration.BindingKey));
            if (boundObject == null)
            {
                return binding;
            }

            var marker = boundObject.GetComponent<SceneMarker>();
            if (marker != null && !string.IsNullOrWhiteSpace(marker.MarkerId))
            {
                binding.SceneObjectId = marker.MarkerId;
                binding.StableObjectId = marker.MarkerId;
            }

            if (marker is AreaMarker areaMarker)
            {
                binding.SpatialPayloadJson = AnnotationExportHelper.BuildAreaSpatialPayload(areaMarker);
                binding.Annotations = AnnotationExportHelper.CollectAnnotationsFromMarker(areaMarker, actionTypeId);
            }
            else if (marker is PointMarker pointMarker)
            {
                binding.SpatialPayloadJson = AnnotationExportHelper.BuildPointSpatialPayload(pointMarker);
                binding.Annotations = AnnotationExportHelper.CollectAnnotations(pointMarker, actionTypeId);
            }
            else if (marker != null)
            {
                binding.Annotations = AnnotationExportHelper.CollectAnnotationsFromMarker(marker, actionTypeId);
            }

            return binding;
        }
        private static string BuildScopedBindingKey(string nodeId, string bindingKey)
        {
            return $"{nodeId}/{bindingKey}";
        }
    }
}
