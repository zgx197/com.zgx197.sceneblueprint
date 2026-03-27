#nullable enable
using SceneBlueprint.Contract;
using UnityEditor;

namespace SceneBlueprint.Editor.Compilation
{
    [ActionCompilationDebugRenderer(order: 95)]
    public sealed class BlackboardCompilationDebugRenderer : IActionCompilationDebugRenderer
    {
        public bool Supports(ActionCompilationDebugRenderContext context)
        {
            return context.Artifact.TryGetDebugPayload<BlackboardCompiledAction>(out var compiledAction)
                   && (compiledAction?.Get != null || compiledAction?.Set != null);
        }

        public void Draw(ActionCompilationDebugRenderContext context)
        {
            if (!context.Artifact.TryGetDebugPayload<BlackboardCompiledAction>(out var compiledAction)
                || compiledAction == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Blackboard 节点专用补充", EditorStyles.miniBoldLabel);

                if (compiledAction.Get != null)
                {
                    DrawVariable(compiledAction.Get.Variable, compiledAction.Get.Semantics);
                    return;
                }

                if (compiledAction.Set != null)
                {
                    DrawVariable(compiledAction.Set.Variable, compiledAction.Set.Semantics);
                    EditorGUILayout.LabelField($"原始值: {compiledAction.Set.RawValueText}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"归一化值: {compiledAction.Set.NormalizedValueText}", EditorStyles.miniLabel);
                }
            }
        }

        private static void DrawVariable(BlackboardVariableCompiledData variable, SemanticDescriptorSet semantics)
        {
            EditorGUILayout.LabelField(
                $"变量: {SemanticDescriptorUtility.GetTargetSummary(semantics, "variable", variable.VariableSummary)}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"索引: {variable.VariableIndex}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"作用域: {variable.Scope}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"类型: {variable.VariableType}", EditorStyles.miniLabel);
        }
    }
}
