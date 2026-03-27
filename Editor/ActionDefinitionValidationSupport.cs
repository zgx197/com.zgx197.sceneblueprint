#nullable enable
using System;
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Compilation;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 统一 definition 层 validation 的上下文构建、通用规则补充和 diagnostic 转换，
    /// 让 Inspector / preview compile / export 尽量共用同一套校验入口。
    /// </summary>
    public static class ActionDefinitionValidationSupport
    {
        public static bool HasValidationHooks(ActionDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            if (definition.Validator != null)
            {
                return true;
            }

            var properties = definition.Properties ?? Array.Empty<PropertyDefinition>();
            for (var index = 0; index < properties.Length; index++)
            {
                var property = properties[index];
                if (property.AuthoringRule != null)
                {
                    return true;
                }

            }

            var sceneBindingProperties = definition.FindSceneBindingProperties();
            for (var index = 0; index < sceneBindingProperties.Length; index++)
            {
                if (sceneBindingProperties[index].BindingRequirement?.Required == true)
                {
                    return true;
                }
            }

            return definition.HasOutputVariableDeclarations();
        }

        public static IReadOnlyList<ValidationIssue> Evaluate(
            string nodeId,
            ActionDefinition definition,
            Graph? graph,
            PropertyBag propertyBag,
            IReadOnlyList<VariableDeclaration>? variables)
        {
            return EvaluateResult(nodeId, definition, graph, propertyBag, variables).Issues;
        }

        public static IReadOnlyList<ValidationIssue> Evaluate(NodeValidationContext context)
        {
            return EvaluateResult(context).Issues;
        }

        public static ActionDefinitionValidationResult EvaluateDeclarationResult(ActionDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var issues = new List<ValidationIssue>();
            AppendOutputVariableIssues(definition, issues);
            AppendGraphDeclarationIssues(definition, issues);
            return new ActionDefinitionValidationResult(issues.ToArray());
        }

        public static ActionDefinitionValidationResult EvaluateResult(
            string nodeId,
            ActionDefinition definition,
            Graph? graph,
            PropertyBag propertyBag,
            IReadOnlyList<VariableDeclaration>? variables)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (propertyBag == null)
            {
                throw new ArgumentNullException(nameof(propertyBag));
            }

            var validationContext = new NodeValidationContext(
                nodeId,
                definition,
                ResolveConnectedPortSemanticIds(nodeId, graph),
                propertyBag,
                variables ?? Array.Empty<VariableDeclaration>());

            return EvaluateResult(validationContext);
        }

        public static ActionDefinitionValidationResult EvaluateResult(NodeValidationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<ValidationIssue>();
            AppendOutputVariableIssues(context.Definition, issues);
            AppendGraphDeclarationIssues(context.Definition, issues);
            AppendSceneBindingIssues(context, issues);
            issues.AddRange(ActionDefinitionAuthoringSupport.Validate(context));
            return new ActionDefinitionValidationResult(issues.ToArray());
        }

        public static ActionCompilationDiagnostic[] BuildDiagnostics(
            ActionCompilationContext context,
            string compilerId = "definition.validation")
        {
            if (!TryCreateValidationContext(context, out var validationContext))
            {
                return Array.Empty<ActionCompilationDiagnostic>();
            }

            var result = EvaluateResult(validationContext);
            if (!result.HasIssues)
            {
                return Array.Empty<ActionCompilationDiagnostic>();
            }

            var diagnostics = new ActionCompilationDiagnostic[result.Issues.Length];
            for (var index = 0; index < result.Issues.Length; index++)
            {
                var issue = result.Issues[index];
                var message = issue.PortId == null
                    ? $"定义校验: {issue.Message}"
                    : $"定义校验: {issue.Message} (端口: {issue.PortId})";
                diagnostics[index] = issue.IsError
                    ? ActionCompilationDiagnostic.Error(
                        compilerId,
                        context.ActionId,
                        context.ActionTypeId,
                        "definition.validation.error",
                        message,
                        ActionCompilationDiagnosticStage.DefinitionValidation)
                    : ActionCompilationDiagnostic.Warning(
                        compilerId,
                        context.ActionId,
                        context.ActionTypeId,
                        "definition.validation.warning",
                        message,
                        ActionCompilationDiagnosticStage.DefinitionValidation);
            }

            return diagnostics;
        }

        public static bool TryCreateValidationContext(
            ActionCompilationContext context,
            out NodeValidationContext validationContext)
        {
            validationContext = null!;
            if (context == null)
            {
                return false;
            }

            var definition = context.Definition;
            if (definition == null
                && !context.ActionRegistry.TryGet(context.ActionTypeId, out definition))
            {
                return false;
            }

            var propertyBag = context.PropertyBag
                ?? PropertyDefinitionValueUtility.CreatePropertyBag(
                    definition.Properties,
                    context.Action.Properties,
                    context.Action.SceneBindings);
            var variables = ResolveVariables(context);
            validationContext = new NodeValidationContext(
                context.ActionId,
                definition,
                ResolveConnectedPortSemanticIds(context.ActionId, context.Graph),
                propertyBag,
                variables);
            return true;
        }

        private static VariableDeclaration[] ResolveVariables(ActionCompilationContext context)
        {
            if (context.TryGetUserData(ActionCompilationContext.VariableDeclarationsUserDataKey, out VariableDeclaration[] variables)
                && variables != null)
            {
                return variables;
            }

            return Array.Empty<VariableDeclaration>();
        }

        private static IReadOnlyCollection<string> ResolveConnectedPortSemanticIds(string nodeId, Graph? graph)
        {
            if (graph == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return Array.Empty<string>();
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in graph.Edges)
            {
                var sourcePort = graph.FindPort(edge.SourcePortId);
                var targetPort = graph.FindPort(edge.TargetPortId);
                if (sourcePort != null && string.Equals(sourcePort.NodeId, nodeId, StringComparison.Ordinal))
                {
                    result.Add(sourcePort.SemanticId);
                }

                if (targetPort != null && string.Equals(targetPort.NodeId, nodeId, StringComparison.Ordinal))
                {
                    result.Add(targetPort.SemanticId);
                }
            }

            return result;
        }

        private static void AppendSceneBindingIssues(
            NodeValidationContext context,
            List<ValidationIssue> issues)
        {
            var properties = context.Definition.FindSceneBindingProperties();
            for (var index = 0; index < properties.Length; index++)
            {
                var property = properties[index];
                var requirement = property.BindingRequirement;
                if (requirement == null || !requirement.Required)
                {
                    continue;
                }

                var rawValue = context.Properties.Get<string>(property.Key) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(rawValue))
                {
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(requirement.DisplayName)
                    ? property.DisplayName
                    : requirement.DisplayName;
                issues.Add(ValidationIssue.Error($"{displayName} 是必需场景绑定，当前尚未配置。"));
            }
        }

        private static void AppendOutputVariableIssues(
            ActionDefinition definition,
            List<ValidationIssue> issues)
        {
            var outputVariables = definition.GetDeclaredOutputVariables();
            if (outputVariables.Length == 0)
            {
                return;
            }

            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < outputVariables.Length; index++)
            {
                var variable = outputVariables[index];
                if (string.IsNullOrWhiteSpace(variable.Name))
                {
                    issues.Add(ValidationIssue.Error($"输出变量 #{index + 1} 缺少 Name，无法作为正式输出声明。"));
                    continue;
                }

                if (!seenNames.Add(variable.Name))
                {
                    issues.Add(ValidationIssue.Error($"输出变量 {variable.Name} 在当前节点定义中重复声明，必须先消除重复声明。"));
                }
            }
        }

        private static void AppendGraphDeclarationIssues(
            ActionDefinition definition,
            List<ValidationIssue> issues)
        {
            var ports = definition.FindGraphDeclarationPorts();
            if (ports.Length == 0)
            {
                return;
            }

            var roleCounts = new Dictionary<PortGraphRole, int>();
            for (var index = 0; index < ports.Length; index++)
            {
                var role = ports[index].GraphRole;
                if (AllowsMultipleGraphRole(role))
                {
                    continue;
                }

                if (roleCounts.TryGetValue(role, out var count))
                {
                    roleCounts[role] = count + 1;
                }
                else
                {
                    roleCounts[role] = 1;
                }
            }

            foreach (var pair in roleCounts)
            {
                if (pair.Value <= 1)
                {
                    continue;
                }

                issues.Add(ValidationIssue.Error(
                    $"图结构角色 {pair.Key} 在当前节点定义中重复声明了 {pair.Value} 次，必须收敛为单一正式入口。"));
            }
        }

        private static bool AllowsMultipleGraphRole(PortGraphRole graphRole)
        {
            return graphRole == PortGraphRole.JoinInput
                || graphRole == PortGraphRole.ConditionInput
                || graphRole == PortGraphRole.TriggerBranch;
        }
    }
}
