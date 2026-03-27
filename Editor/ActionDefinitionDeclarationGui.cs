#nullable enable
using SceneBlueprint.Core;
using UnityEditor;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 统一绘制定义层声明的 Inspector/预览 GUI。
    /// 避免节点 Inspector、模板 Inspector、后续更多 definition-first 入口继续各自维护
    /// SceneBinding / OutputVariable / GraphRole 的展示协议。
    /// </summary>
    public static class ActionDefinitionDeclarationGui
    {
        public static bool Draw(
            ActionDefinition definition,
            PropertyBag? propertyBag = null,
            bool showConfigurationStatus = true)
        {
            var hasSceneBindings = definition.HasSceneBindingDeclarations();
            var hasOutputVariables = definition.HasOutputVariableDeclarations();
            var hasGraphDeclarations = definition.HasGraphDeclarations();
            if (!hasSceneBindings && !hasOutputVariables && !hasGraphDeclarations)
            {
                return false;
            }

            if (hasSceneBindings)
            {
                DrawSceneBindingDeclarations(definition, propertyBag, showConfigurationStatus);
            }

            if (hasSceneBindings && hasOutputVariables)
            {
                EditorGUILayout.Space(4);
            }

            if (hasOutputVariables)
            {
                DrawOutputVariables(definition);
            }

            if ((hasSceneBindings || hasOutputVariables) && hasGraphDeclarations)
            {
                EditorGUILayout.Space(4);
            }

            if (hasGraphDeclarations)
            {
                DrawGraphDeclarations(definition);
            }

            return true;
        }

        public static void DrawSceneBindingDeclarations(
            ActionDefinition definition,
            PropertyBag? propertyBag,
            bool showConfigurationStatus)
        {
            EditorGUILayout.LabelField("场景绑定要求", EditorStyles.miniBoldLabel);

            var bindings = SceneBindingDeclarationSupport.CollectDeclaredBindings(
                definition,
                propertyBag,
                includeEmpty: true);
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                var title = SceneBindingDeclarationSupport.ResolveTitle(binding);
                var summary = $"{title} · {binding.BindingType}";
                if (showConfigurationStatus)
                {
                    summary = $"{summary} · {SceneBindingDeclarationSupport.ResolveConfigurationStatus(binding)}";
                }

                EditorGUILayout.LabelField(summary, EditorStyles.label);
                EditorGUILayout.LabelField(
                    SceneBindingDeclarationSupport.BuildDetailSummary(binding),
                    EditorStyles.miniLabel);
            }
        }

        public static void DrawOutputVariables(ActionDefinition definition)
        {
            EditorGUILayout.LabelField("输出变量声明", EditorStyles.miniBoldLabel);
            var outputVariables = definition.GetDeclaredOutputVariables();
            for (var index = 0; index < outputVariables.Length; index++)
            {
                var outputVariable = outputVariables[index];
                var title = string.IsNullOrWhiteSpace(outputVariable.DisplayName)
                    ? outputVariable.Name
                    : $"{outputVariable.DisplayName} ({outputVariable.Name})";
                EditorGUILayout.LabelField(title, EditorStyles.label);
                EditorGUILayout.LabelField(
                    $"类型: {outputVariable.Type} | 作用域: {outputVariable.Scope}",
                    EditorStyles.miniLabel);
            }
        }

        public static void DrawGraphDeclarations(ActionDefinition definition)
        {
            EditorGUILayout.LabelField("图结构声明", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"持续类型: {definition.Duration} | 端口数: {(definition.Ports?.Length ?? 0)}",
                EditorStyles.miniLabel);

            var ports = definition.FindGraphDeclarationPorts();
            for (var index = 0; index < ports.Length; index++)
            {
                var port = ports[index];
                var title = string.IsNullOrWhiteSpace(port.DisplayName)
                    ? port.Id
                    : $"{port.DisplayName} ({port.Id})";
                EditorGUILayout.LabelField(
                    $"{title} · {port.Direction} {port.Kind} · {port.GraphRole}",
                    EditorStyles.label);

                var summaryLabel = string.IsNullOrWhiteSpace(port.SummaryLabel)
                    ? title
                    : port.SummaryLabel;
                var detail = $"摘要: {summaryLabel}";
                if (port.MinConnections > 0)
                {
                    detail = $"{detail} | 建议至少连接 {port.MinConnections} 条";
                }

                EditorGUILayout.LabelField(detail, EditorStyles.miniLabel);
            }
        }
    }
}
