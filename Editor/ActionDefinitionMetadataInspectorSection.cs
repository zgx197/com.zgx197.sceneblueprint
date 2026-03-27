#nullable enable
using UnityEditor;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 把 definition 已声明的场景绑定要求和输出变量直接投影到 Inspector，
    /// 让这两类协议不再只是导出/运行时隐式约束。
    /// </summary>
    [ActionInspectorSection(order: 30)]
    public sealed class ActionDefinitionMetadataInspectorSection : IActionInspectorSection
    {
        public bool Supports(ActionInspectorSectionContext context)
        {
            if (context.SuppressDefinitionMetadataSection)
            {
                return false;
            }

            return context.Definition.HasSceneBindingDeclarations()
                || context.Definition.HasOutputVariableDeclarations()
                || context.Definition.HasGraphDeclarations();
        }

        public bool Draw(ActionInspectorSectionContext context)
        {
            var hasSceneBindings = context.Definition.HasSceneBindingDeclarations();
            var hasOutputVariables = context.Definition.HasOutputVariableDeclarations();
            var hasGraphDeclarations = context.Definition.HasGraphDeclarations();
            if (!hasSceneBindings && !hasOutputVariables && !hasGraphDeclarations)
            {
                return false;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("定义声明", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                ActionDefinitionDeclarationGui.Draw(
                    context.Definition,
                    context.Data.Properties,
                    showConfigurationStatus: true);
            }

            return false;
        }
    }
}
