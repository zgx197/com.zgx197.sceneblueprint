#nullable enable
using SceneBlueprint.Contract;
using UnityEditor;

namespace SceneBlueprint.Editor.Compilation
{
    [ActionCompilationDebugRenderer(order: 90)]
    public sealed class FlowCompilationDebugRenderer : IActionCompilationDebugRenderer
    {
        public bool Supports(ActionCompilationDebugRenderContext context)
        {
            return context.Artifact.TryGetDebugPayload<FlowCompiledAction>(out var compiledAction)
                   && (compiledAction?.Join != null || compiledAction?.Filter != null || compiledAction?.Branch != null);
        }

        public void Draw(ActionCompilationDebugRenderContext context)
        {
            if (!context.Artifact.TryGetDebugPayload<FlowCompiledAction>(out var compiledAction)
                || compiledAction == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Flow 节点专用补充", EditorStyles.miniBoldLabel);

                if (compiledAction.Join != null)
                {
                    var graphSummary = SemanticDescriptorUtility.GetGraphSummary(
                        compiledAction.Join.Semantics,
                        compiledAction.Join.IncomingActionSummary);
                    EditorGUILayout.LabelField($"汇合输入数: {compiledAction.Join.RequiredCount}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"上游输入: {graphSummary}", EditorStyles.miniLabel);
                    return;
                }

                if (compiledAction.Filter != null)
                {
                    EditorGUILayout.LabelField($"比较操作: {compiledAction.Filter.Operator}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"常量值: {compiledAction.Filter.ConstValueText}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        $"值摘要: {SemanticDescriptorUtility.GetValueSummary(compiledAction.Filter.Semantics, compiledAction.Filter.ConstValueText)}",
                        EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        $"缺少比较输入时直接通过: {(compiledAction.Filter.MissingCompareInputPasses ? "是" : "否")}",
                        EditorStyles.miniLabel);
                    return;
                }

                if (compiledAction.Branch != null)
                {
                    EditorGUILayout.LabelField(
                        $"路由端口: {SemanticDescriptorUtility.GetGraphSummary(compiledAction.Branch.Semantics, compiledAction.Branch.RoutedPort)}",
                        EditorStyles.miniLabel);
                }
            }
        }
    }
}
