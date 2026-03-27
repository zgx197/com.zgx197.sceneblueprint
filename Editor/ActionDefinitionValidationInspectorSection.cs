#nullable enable
using System;
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using UnityEditor;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 把 ActionDefinition.Validator 升级为 Inspector 内的正式定义层 validation 段，
    /// 避免节点继续各自堆 HelpBox。
    /// </summary>
    [ActionInspectorSection(order: 90)]
    public sealed class ActionDefinitionValidationInspectorSection : IActionInspectorSection
    {
        public bool Supports(ActionInspectorSectionContext context)
        {
            if (context.SuppressDefinitionValidationSection)
            {
                return false;
            }

            return ActionDefinitionValidationSupport.HasValidationHooks(context.Definition);
        }

        public bool Draw(ActionInspectorSectionContext context)
        {
            var result = EvaluateIssues(context);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("定义校验", EditorStyles.boldLabel);
            ActionDefinitionValidationGui.DrawResult(
                result,
                "当前节点的定义层约束已通过。后续若编译仍报错，应优先查看语义解析或编译计划阶段的诊断。");

            return false;
        }

        private static ActionDefinitionValidationResult EvaluateIssues(
            ActionInspectorSectionContext context)
        {
            try
            {
                return ActionDefinitionValidationSupport.EvaluateResult(
                    context.Node.Id,
                    context.Definition,
                    context.Graph,
                    context.Data.Properties,
                    context.Variables ?? Array.Empty<VariableDeclaration>());
            }
            catch (Exception ex)
            {
                return new ActionDefinitionValidationResult(new[]
                {
                    ValidationIssue.Warning($"定义层校验执行失败: {ex.Message}")
                });
            }
        }
    }
}
