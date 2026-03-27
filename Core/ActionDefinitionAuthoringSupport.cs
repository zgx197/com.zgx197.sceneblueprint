#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 把 definition 层的 property rule + action validator 收成统一入口，
    /// 供 Inspector、分析层和后续更多 authoring 消费链共享。
    /// </summary>
    public static class ActionDefinitionAuthoringSupport
    {
        public static bool TryApplyPropertyNormalization(
            string nodeId,
            ActionDefinition definition,
            PropertyDefinition property,
            PropertyBag bag,
            IReadOnlyCollection<string>? connectedPortSemanticIds = null,
            IReadOnlyList<VariableDeclaration>? variables = null)
        {
            if (property.AuthoringRule == null)
            {
                return false;
            }

            var context = new PropertyAuthoringContext(
                nodeId,
                definition,
                property,
                bag,
                connectedPortSemanticIds,
                variables);
            var currentValue = bag.GetRaw(property.Key);
            if (!property.AuthoringRule.TryNormalize(context, currentValue, out var normalizedValue))
            {
                return false;
            }

            if (ValuesEqual(currentValue, normalizedValue))
            {
                return false;
            }

            if (normalizedValue != null)
            {
                bag.Set(property.Key, normalizedValue);
                return true;
            }

            return false;
        }

        public static IReadOnlyList<ValidationIssue> Validate(
            NodeValidationContext context)
        {
            var issues = new List<ValidationIssue>();
            var properties = context.Definition.Properties ?? Array.Empty<PropertyDefinition>();
            for (var index = 0; index < properties.Length; index++)
            {
                var property = properties[index];
                if (property.AuthoringRule == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(property.VisibleWhen)
                    && !VisibleWhenEvaluator.Evaluate(property.VisibleWhen, context.Properties))
                {
                    continue;
                }

                var propertyContext = new PropertyAuthoringContext(
                    context.NodeId,
                    context.Definition,
                    property,
                    context.Properties,
                    context.ConnectedPortSemanticIds,
                    context.Variables);
                foreach (var issue in property.AuthoringRule.Validate(propertyContext))
                {
                    issues.Add(new ValidationIssue(
                        $"{property.DisplayName}: {issue.Message}",
                        issue.IsError,
                        issue.PortId));
                }
            }

            if (context.Definition.Validator != null)
            {
                issues.AddRange(context.Definition.Validator.Validate(context));
            }

            return issues;
        }

        private static bool ValuesEqual(object? left, object? right)
        {
            if (left == null && right == null)
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left is string leftText && right is string rightText)
            {
                return string.Equals(leftText, rightText, StringComparison.Ordinal);
            }

            return left.Equals(right);
        }
    }
}
