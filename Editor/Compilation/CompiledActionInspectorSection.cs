#nullable enable
using System;
using SceneBlueprint.Editor;
using SceneBlueprint.Runtime;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Compilation
{
    /// <summary>
    /// 通用编译摘要段。
    /// 只要节点已注册 compiler，就默认尝试在 Inspector 中展示编译诊断与调试摘要；
    /// 业务节点只在需要特殊 UI 时再补 debug renderer，而不是继续新增独立 section。
    /// </summary>
    [ActionInspectorSection(order: 100)]
    public sealed class CompiledActionInspectorSection : IActionInspectorSection
    {
        public bool Supports(ActionInspectorSectionContext context)
        {
            if (context.SuppressDefaultCompilationSection)
            {
                return false;
            }

            return ActionCompilationPreviewSupport.TryResolveCompiler(
                context.Node.Id,
                context.Data.ActionTypeId,
                context.Definition,
                context.ActionRegistry,
                context.Data.Properties,
                BlueprintRuntimeSettings.Instance.TargetTickRate,
                context.Graph,
                context.BindingContext,
                context.Data,
                userData: ActionCompilationPreviewSupport.BuildVariableUserData(context.Variables),
                out _,
                out _);
        }

        public bool Draw(ActionInspectorSectionContext context)
        {
            if (!ActionCompilationPreviewSupport.TryCompile(
                    context.Node.Id,
                    context.Data.ActionTypeId,
                    context.Definition,
                    context.ActionRegistry,
                    context.Data.Properties,
                    BlueprintRuntimeSettings.Instance.TargetTickRate,
                    context.Graph,
                    context.BindingContext,
                    context.Data,
                    userData: ActionCompilationPreviewSupport.BuildVariableUserData(context.Variables),
                    out var artifact,
                    out var failureMessage,
                    out var failureType))
            {
                return false;
            }

            if (!artifact.HasOutput)
            {
                return false;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("语义解析结果与编译计划", EditorStyles.boldLabel);

            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                EditorGUILayout.HelpBox(failureMessage, failureType);
            }

            DrawDiagnostics(artifact);
            DrawDebugProjectionModel(artifact.DebugModel);

            var renderContext = new ActionCompilationDebugRenderContext(context, artifact);
            if (ActionCompilationDebugRendererRegistry.TryResolve(renderContext, out var renderer))
            {
                renderer.Draw(renderContext);
                return false;
            }

            DrawFallback(artifact);
            return false;
        }

        private static void DrawDiagnostics(ActionCompilationArtifact artifact)
        {
            if (!artifact.HasDiagnostics)
            {
                return;
            }

            EditorGUILayout.LabelField("定义校验与编译诊断", EditorStyles.miniBoldLabel);
            for (var index = 0; index < artifact.Diagnostics.Length; index++)
            {
                var diagnostic = artifact.Diagnostics[index];
                var messageType = diagnostic.Severity == ActionCompilationDiagnosticSeverity.Error
                    ? MessageType.Error
                    : diagnostic.Severity == ActionCompilationDiagnosticSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(BuildDiagnosticMessage(diagnostic), messageType);
            }
        }

        private static string BuildDiagnosticMessage(ActionCompilationDiagnostic diagnostic)
        {
            var stageLabel = ResolveDiagnosticStageLabel(diagnostic);
            if (string.IsNullOrWhiteSpace(stageLabel))
            {
                return diagnostic.Message;
            }

            return $"[{stageLabel}] {diagnostic.Message}";
        }

        private static string ResolveDiagnosticStageLabel(ActionCompilationDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                return string.Empty;
            }

            switch (diagnostic.Stage)
            {
                case ActionCompilationDiagnosticStage.DefinitionValidation:
                    return "定义校验";
                case ActionCompilationDiagnosticStage.SemanticNormalization:
                    return "语义归一化";
                case ActionCompilationDiagnosticStage.PlanCompilation:
                    return "编译计划";
                case ActionCompilationDiagnosticStage.CompatibilityFallback:
                    return "兼容回退";
                case ActionCompilationDiagnosticStage.CompilerException:
                    return "编译异常";
            }

            if (diagnostic.Code.StartsWith("definition.validation", StringComparison.Ordinal))
            {
                return "定义校验";
            }

            if (diagnostic.Code.StartsWith("compiler.", StringComparison.Ordinal))
            {
                return "编译阶段";
            }

            if (!string.IsNullOrWhiteSpace(diagnostic.CompilerId))
            {
                return diagnostic.CompilerId;
            }

            return string.Empty;
        }

        private static void DrawFallback(ActionCompilationArtifact artifact)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Compiler: {artifact.CompilerId}", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"语义解析结果: {GetTypeLabel(artifact.SemanticModel)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"编译计划: {GetTypeLabel(artifact.CompiledPlan)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"调试投影: {GetTypeLabel(artifact.DebugPayload)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"正式语义对象: {GetSemanticLabel(artifact)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"运输元数据: {artifact.TransportMetadataSummary}", EditorStyles.miniLabel);
            }
        }

        internal static void DrawDebugProjectionModel(DebugProjectionModel? model)
        {
            if (model == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!string.IsNullOrWhiteSpace(model.Title))
                {
                    EditorGUILayout.LabelField(model.Title, EditorStyles.miniBoldLabel);
                }

                if (!string.IsNullOrWhiteSpace(model.Summary))
                {
                    ActionInspectorAuthoringGui.DrawWrappedMiniLabel(model.Summary);
                }

                DrawReadbackSummary(model.Readback);

                var sections = ObservationNoiseReductionUtility.BuildVisibleDebugProjectionSections(model);
                for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
                {
                    var section = sections[sectionIndex];
                    var sectionTitle = !string.IsNullOrWhiteSpace(section.Title)
                        ? section.Title
                        : ObservationStageUtility.GetTitle(section.Stage);
                    if (!string.IsNullOrWhiteSpace(sectionTitle))
                    {
                        EditorGUILayout.LabelField(sectionTitle, EditorStyles.miniBoldLabel);
                    }

                    var fields = section.Fields;
                    for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                    {
                        var field = fields[fieldIndex];
                        if (string.IsNullOrWhiteSpace(field.Value))
                        {
                            continue;
                        }

                        EditorGUILayout.LabelField($"{field.Label}: {field.Value}", EditorStyles.miniLabel);
                    }
                }
            }
        }

        private static void DrawReadbackSummary(DebugProjectionReadbackSummary readback)
        {
            if (readback == null || !readback.HasContent)
            {
                return;
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("主线回读", EditorStyles.miniBoldLabel);
            DrawReadbackField("定义摘要", readback.DefinitionSummary);
            DrawReadbackField("语义摘要", readback.SemanticSummary);
            DrawReadbackField("计划摘要", readback.PlanSummary);
        }

        private static void DrawReadbackField(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            ActionInspectorAuthoringGui.DrawWrappedMiniLabel($"{label}: {value}");
        }

        private static string GetTypeLabel(object? value)
        {
            return value == null
                ? "无"
                : value.GetType().Name;
        }

        private static string GetSemanticLabel(ActionCompilationArtifact artifact)
        {
            if (!artifact.TryGetSemanticDescriptors(out var semantics) || semantics == null)
            {
                return "无";
            }

            return $"S{semantics.Subjects.Length}/T{semantics.Targets.Length}/C{semantics.Conditions.Length}/V{semantics.Values.Length}/G{semantics.Graphs.Length}/E{semantics.EventContexts.Length}";
        }
    }
}
