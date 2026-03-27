#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Editor.Compilation;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// Blackboard 域第一版统一 compiler。
    /// 把变量解析结果、类型化值与访问摘要收成结构化产物，避免 Inspector / runtime 再各自解释原始字符串。
    /// </summary>
    [ActionCompiler(order: 95)]
    public sealed class BlackboardActionCompiler : IActionCompiler
    {
        public bool Supports(ActionCompilationContext context)
        {
            return string.Equals(context.ActionTypeId, AT.Blackboard.Get, StringComparison.Ordinal)
                   || string.Equals(context.ActionTypeId, AT.Blackboard.Set, StringComparison.Ordinal);
        }

        public ActionCompilationArtifact Compile(ActionCompilationContext context)
        {
            var compilerId = GetType().FullName ?? GetType().Name;
            var diagnostics = new List<ActionCompilationDiagnostic>();
            var compiledAction = new BlackboardCompiledAction
            {
                SchemaVersion = 1,
                ActionId = context.ActionId,
                ActionTypeId = context.ActionTypeId,
            };

            if (string.Equals(context.ActionTypeId, AT.Blackboard.Get, StringComparison.Ordinal))
            {
                compiledAction.Get = BuildGet(context, diagnostics, compilerId);
            }
            else if (string.Equals(context.ActionTypeId, AT.Blackboard.Set, StringComparison.Ordinal))
            {
                compiledAction.Set = BuildSet(context, diagnostics, compilerId);
            }

            var metadataEntries = new[]
            {
                new PropertyValue
                {
                    Key = BlackboardCompiledActionMetadata.BuildMetadataKey(context.ActionId),
                    ValueType = "json",
                    Value = BlackboardCompiledActionMetadata.Serialize(compiledAction),
                }
            };
            var diagnosticsArray = diagnostics.ToArray();
            var semantics = compiledAction.Get?.Semantics
                            ?? compiledAction.Set?.Semantics
                            ?? new SemanticDescriptorSet();

            return new ActionCompilationArtifact(
                context.ActionId,
                context.ActionTypeId,
                compilerId,
                semanticModel: compiledAction,
                compiledPlan: compiledAction,
                debugPayload: compiledAction,
                diagnostics: diagnosticsArray,
                metadataEntries: metadataEntries,
                debugProjection: new ActionCompilationDebugProjection(
                    compiledAction,
                    semantics,
                    DebugProjectionModelFactory.CreateDefault(
                        compiledAction,
                        compiledAction,
                        compiledAction,
                        semantics,
                        diagnosticsArray,
                        metadataEntries,
                        BuildReadbackSummary(compiledAction))));
        }

        private static BlackboardGetCompiledData BuildGet(
            ActionCompilationContext context,
            List<ActionCompilationDiagnostic> diagnostics,
            string compilerId)
        {
            var authoring = BlackboardAuthoringUtility.ReadGet(context.Action, context.Definition);
            var variable = ResolveVariable(context, authoring.VariableIndex);
            var compiledVariable = BuildVariableData(variable, authoring.VariableIndex);

            AddVariableDiagnostics(diagnostics, compilerId, context, authoring.VariableIndex, variable);

            return new BlackboardGetCompiledData
            {
                Variable = compiledVariable,
                AccessSummary = SemanticSummaryUtility.BuildBlackboardAccessSummary(
                    "get",
                    compiledVariable.VariableSummary),
                Semantics = new SemanticDescriptorSet
                {
                    Targets = new[]
                    {
                        SemanticDescriptorUtility.BuildTargetDescriptor(
                            "variable",
                            "blackboard-variable",
                            compiledVariable.VariableIndex >= 0
                                ? compiledVariable.VariableIndex.ToString()
                                : string.Empty,
                            compiledVariable.VariableSummary),
                    },
                    Values = new[]
                    {
                        SemanticDescriptorUtility.BuildBlackboardValueDescriptor(
                            "get",
                            compiledVariable.VariableName,
                            compiledVariable.VariableType,
                            string.Empty,
                            string.Empty,
                            compiledVariable.VariableSummary),
                    },
                },
            };
        }

        private static BlackboardSetCompiledData BuildSet(
            ActionCompilationContext context,
            List<ActionCompilationDiagnostic> diagnostics,
            string compilerId)
        {
            var authoring = BlackboardAuthoringUtility.ReadSet(context.Action, context.Definition);
            var variable = ResolveVariable(context, authoring.VariableIndex);
            var compiledVariable = BuildVariableData(variable, authoring.VariableIndex);
            var normalizedValueText = BlackboardAuthoringUtility.NormalizeValueText(
                variable?.Type,
                authoring.ValueText,
                out var usedFallback);

            AddVariableDiagnostics(diagnostics, compilerId, context, authoring.VariableIndex, variable);

            if (usedFallback
                && !string.IsNullOrWhiteSpace(authoring.ValueText)
                && variable != null)
            {
                diagnostics.Add(ActionCompilationDiagnostic.Warning(
                    compilerId,
                    context.ActionId,
                    context.ActionTypeId,
                    "blackboard.set.value.normalized-with-fallback",
                    $"Blackboard.Set 的值无法按变量类型 {variable.Type} 解析，已回退为 {normalizedValueText}。",
                    ActionCompilationDiagnosticStage.CompatibilityFallback));
            }
            else if (!string.Equals(authoring.ValueText, normalizedValueText, StringComparison.Ordinal))
            {
                diagnostics.Add(ActionCompilationDiagnostic.Info(
                    compilerId,
                    context.ActionId,
                    context.ActionTypeId,
                    "blackboard.set.value.normalized",
                    $"Blackboard.Set 的写入值已归一化为 {normalizedValueText}。",
                    ActionCompilationDiagnosticStage.SemanticNormalization));
            }

            return new BlackboardSetCompiledData
            {
                Variable = compiledVariable,
                RawValueText = authoring.ValueText,
                NormalizedValueText = normalizedValueText,
                AccessSummary = SemanticSummaryUtility.BuildBlackboardAccessSummary(
                    "set",
                    compiledVariable.VariableSummary,
                    normalizedValueText),
                Semantics = new SemanticDescriptorSet
                {
                    Targets = new[]
                    {
                        SemanticDescriptorUtility.BuildTargetDescriptor(
                            "variable",
                            "blackboard-variable",
                            compiledVariable.VariableIndex >= 0
                                ? compiledVariable.VariableIndex.ToString()
                                : string.Empty,
                            compiledVariable.VariableSummary),
                    },
                    Values = new[]
                    {
                        SemanticDescriptorUtility.BuildBlackboardValueDescriptor(
                            "set",
                            compiledVariable.VariableName,
                            compiledVariable.VariableType,
                            authoring.ValueText,
                            normalizedValueText,
                            compiledVariable.VariableSummary),
                    },
                },
            };
        }

        private static BlackboardVariableCompiledData BuildVariableData(VariableDeclaration? variable, int variableIndex)
        {
            return new BlackboardVariableCompiledData
            {
                VariableIndex = variable?.Index ?? variableIndex,
                Scope = variable?.Scope ?? string.Empty,
                VariableName = variable?.Name ?? string.Empty,
                VariableType = variable?.Type ?? string.Empty,
                VariableSummary = BlackboardAuthoringUtility.BuildVariableSummary(variable, variableIndex),
            };
        }

        private static void AddVariableDiagnostics(
            List<ActionCompilationDiagnostic> diagnostics,
            string compilerId,
            ActionCompilationContext context,
            int variableIndex,
            VariableDeclaration? variable)
        {
            if (variableIndex < 0)
            {
                diagnostics.Add(ActionCompilationDiagnostic.Warning(
                    compilerId,
                    context.ActionId,
                    context.ActionTypeId,
                    "blackboard.variable.not-configured",
                    "Blackboard 节点未配置 variableIndex。",
                    ActionCompilationDiagnosticStage.SemanticNormalization));
                return;
            }

            if (variable == null)
            {
                diagnostics.Add(ActionCompilationDiagnostic.Warning(
                    compilerId,
                    context.ActionId,
                    context.ActionTypeId,
                    "blackboard.variable.not-found",
                    $"Blackboard 节点引用的变量索引 {variableIndex} 未在当前蓝图变量表中找到。",
                    ActionCompilationDiagnosticStage.SemanticNormalization));
            }
        }

        private static VariableDeclaration? ResolveVariable(ActionCompilationContext context, int variableIndex)
        {
            if (variableIndex < 0)
            {
                return null;
            }

            var variables = ResolveVariables(context);
            for (var index = 0; index < variables.Length; index++)
            {
                if (variables[index] != null && variables[index].Index == variableIndex)
                {
                    return variables[index];
                }
            }

            return null;
        }

        private static VariableDeclaration[] ResolveVariables(ActionCompilationContext context)
        {
            if (context.BlueprintData?.Variables != null && context.BlueprintData.Variables.Length > 0)
            {
                return context.BlueprintData.Variables;
            }

            if (context.TryGetUserData<VariableDeclaration[]>(
                    ActionCompilationContext.VariableDeclarationsUserDataKey,
                    out var variables)
                && variables != null)
            {
                return variables;
            }

            return Array.Empty<VariableDeclaration>();
        }

        private static DebugProjectionReadbackSummary BuildReadbackSummary(BlackboardCompiledAction compiledAction)
        {
            if (compiledAction.Get != null)
            {
                return new DebugProjectionReadbackSummary(
                    definitionSummary: $"读取 {compiledAction.Get.Variable.VariableSummary}",
                    semanticSummary: compiledAction.Get.AccessSummary,
                    planSummary: compiledAction.Get.AccessSummary);
            }

            if (compiledAction.Set != null)
            {
                var definitionSummary =
                    $"{compiledAction.Set.Variable.VariableSummary} <- {compiledAction.Set.NormalizedValueText}";
                return new DebugProjectionReadbackSummary(
                    definitionSummary: definitionSummary,
                    semanticSummary: compiledAction.Set.AccessSummary,
                    planSummary: compiledAction.Set.AccessSummary);
            }

            return DebugProjectionReadbackSummary.Empty;
        }
    }
}
