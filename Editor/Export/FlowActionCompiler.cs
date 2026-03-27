#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Editor.Compilation;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// Flow 域第一版统一 compiler。
    /// 当前覆盖 Flow.Join / Flow.Filter / Flow.Branch / Flow.Delay，
    /// 把图结构汇合规则、过滤比较规则与延迟配置前移到编译层。
    /// </summary>
    [ActionCompiler(order: 90)]
    public sealed class FlowActionCompiler : IActionCompiler
    {
        public bool Supports(ActionCompilationContext context)
        {
            return string.Equals(context.ActionTypeId, AT.Flow.Join, StringComparison.Ordinal)
                   || string.Equals(context.ActionTypeId, AT.Flow.Filter, StringComparison.Ordinal)
                   || string.Equals(context.ActionTypeId, AT.Flow.Branch, StringComparison.Ordinal)
                   || string.Equals(context.ActionTypeId, AT.Flow.Delay, StringComparison.Ordinal);
        }

        public ActionCompilationArtifact Compile(ActionCompilationContext context)
        {
            var compilerId = GetType().FullName ?? GetType().Name;
            var diagnostics = new List<ActionCompilationDiagnostic>();
            var compiledAction = new FlowCompiledAction
            {
                SchemaVersion = 1,
                ActionId = context.ActionId,
                ActionTypeId = context.ActionTypeId,
            };

            if (string.Equals(context.ActionTypeId, AT.Flow.Join, StringComparison.Ordinal))
            {
                compiledAction.Join = BuildJoin(context, diagnostics, compilerId);
            }
            else if (string.Equals(context.ActionTypeId, AT.Flow.Filter, StringComparison.Ordinal))
            {
                compiledAction.Filter = BuildFilter(context, diagnostics, compilerId);
            }
            else if (string.Equals(context.ActionTypeId, AT.Flow.Branch, StringComparison.Ordinal))
            {
                compiledAction.Branch = BuildBranch(context);
            }
            else if (string.Equals(context.ActionTypeId, AT.Flow.Delay, StringComparison.Ordinal))
            {
                compiledAction.Delay = BuildDelay(context, diagnostics, compilerId);
            }

            var metadataEntries = new[]
            {
                new PropertyValue
                {
                    Key = FlowCompiledActionMetadata.BuildMetadataKey(context.ActionId),
                    ValueType = "json",
                    Value = FlowCompiledActionMetadata.Serialize(compiledAction),
                }
            };
            var readback = BuildReadbackSummary(compiledAction);

            return new ActionCompilationArtifact(
                context.ActionId,
                context.ActionTypeId,
                compilerId,
                semanticModel: compiledAction,
                compiledPlan: compiledAction,
                debugPayload: compiledAction,
                diagnostics: diagnostics.ToArray(),
                metadataEntries: metadataEntries,
                debugProjection: new ActionCompilationDebugProjection(
                    compiledAction,
                    ResolveSemantics(compiledAction),
                    DebugProjectionModelFactory.CreateDefault(
                        compiledAction,
                        compiledAction,
                        compiledAction,
                        ResolveSemantics(compiledAction),
                        diagnostics.ToArray(),
                        metadataEntries,
                        readback)));
        }

        private static FlowJoinCompiledData BuildJoin(
            ActionCompilationContext context,
            List<ActionCompilationDiagnostic> diagnostics,
            string compilerId)
        {
            var incomingActionIds = ResolveIncomingActionIds(context);
            var requiredCount = incomingActionIds.Length > 0
                ? incomingActionIds.Length
                : ResolveFallbackRequiredCount(context.Action);

            if (incomingActionIds.Length == 0)
            {
                diagnostics.Add(ActionCompilationDiagnostic.Warning(
                    compilerId,
                    context.ActionId,
                    context.ActionTypeId,
                    "flow.join.no-incoming-flow",
                    "Flow.Join 未发现有效的控制流入边，将无法被正常激活。",
                    ActionCompilationDiagnosticStage.PlanCompilation));
            }

            return new FlowJoinCompiledData
            {
                RequiredCount = Math.Max(1, requiredCount),
                IncomingActionIds = incomingActionIds,
                IncomingActionSummary = SemanticSummaryUtility.BuildFlowJoinIncomingSummary(incomingActionIds),
                ConditionSummary = SemanticSummaryUtility.BuildFlowJoinConditionSummary(requiredCount),
                Semantics = new SemanticDescriptorSet
                {
                    Conditions = new[]
                    {
                        new ConditionSemanticDescriptor
                        {
                            Kind = "flow.join",
                            Type = "FlowJoin",
                            Summary = SemanticSummaryUtility.BuildFlowJoinConditionSummary(requiredCount),
                            RequiredCount = Math.Max(1, requiredCount),
                            IncomingActionIds = incomingActionIds,
                        },
                    },
                    Graphs = new[]
                    {
                        SemanticDescriptorUtility.BuildFlowJoinGraphDescriptor(
                            requiredCount,
                            incomingActionIds,
                            SemanticSummaryUtility.BuildFlowJoinIncomingSummary(incomingActionIds)),
                    },
                },
            };
        }

        private static FlowFilterCompiledData BuildFilter(
            ActionCompilationContext context,
            List<ActionCompilationDiagnostic> diagnostics,
            string compilerId)
        {
            var reader = CreatePropertyReader(context);
            var rawOperator = reader.GetString(ActionPortIds.FlowFilter.Op.Key, "==");
            var rawConstValue = reader.GetString(ActionPortIds.FlowFilter.ConstValue.Key, "0");
            var authoring = FlowFilterAuthoringUtility.Read(reader);

            if (!string.Equals(rawOperator, authoring.Operator, StringComparison.Ordinal)
                || !string.Equals(rawConstValue, authoring.ConstValue, StringComparison.Ordinal))
            {
                diagnostics.Add(ActionCompilationDiagnostic.Info(
                    compilerId,
                    context.ActionId,
                    context.ActionTypeId,
                    "flow.filter.normalized",
                    $"Flow.Filter 的比较规则已归一化为 {authoring.Operator} {authoring.ConstValue}。",
                    ActionCompilationDiagnosticStage.SemanticNormalization));
            }

            return new FlowFilterCompiledData
            {
                Operator = authoring.Operator,
                ConstValueText = authoring.ConstValue,
                ConditionSummary = FlowFilterAuthoringUtility.BuildConditionSummary(
                    authoring.Operator,
                    authoring.ConstValue),
                MissingCompareInputPasses = true,
                Semantics = new SemanticDescriptorSet
                {
                    Conditions = new[]
                    {
                        SemanticDescriptorUtility.BuildFlowFilterConditionDescriptor(
                            authoring.Operator,
                            authoring.ConstValue,
                            FlowFilterAuthoringUtility.BuildConditionSummary(
                                authoring.Operator,
                                authoring.ConstValue)),
                    },
                    Values = new[]
                    {
                        new ValueSemanticDescriptor
                        {
                            Kind = "flow.filter.const",
                            Key = "const",
                            ValueType = "string",
                            RawValue = authoring.ConstValue,
                            NormalizedValue = authoring.ConstValue,
                            Summary = authoring.ConstValue,
                        },
                    },
                },
            };
        }

        private static FlowBranchCompiledData BuildBranch(ActionCompilationContext context)
        {
            var authoring = FlowBranchAuthoringUtility.Read(context.Action, context.Definition);
            var routedPort = authoring.ConditionValue
                ? ActionPortIds.FlowBranch.True
                : ActionPortIds.FlowBranch.False;

            return new FlowBranchCompiledData
            {
                ConditionValue = authoring.ConditionValue,
                ConditionSummary = FlowBranchAuthoringUtility.BuildConditionSummary(authoring.ConditionValue),
                RoutedPort = routedPort,
                Semantics = new SemanticDescriptorSet
                {
                    Conditions = new[]
                    {
                        new ConditionSemanticDescriptor
                        {
                            Kind = "flow.branch",
                            Type = "FlowBranch",
                            Summary = FlowBranchAuthoringUtility.BuildConditionSummary(authoring.ConditionValue),
                            Mode = authoring.ConditionValue ? "true" : "false",
                            RoutedPort = routedPort,
                        },
                    },
                    Graphs = new[]
                    {
                        SemanticDescriptorUtility.BuildFlowBranchGraphDescriptor(
                            authoring.ConditionValue,
                            routedPort,
                            FlowBranchAuthoringUtility.BuildRouteSummary(authoring.ConditionValue, routedPort)),
                    },
                },
            };
        }

        private static FlowDelayCompiledData BuildDelay(
            ActionCompilationContext context,
            List<ActionCompilationDiagnostic> diagnostics,
            string compilerId)
        {
            var reader = CreatePropertyReader(context);
            var rawDelaySeconds = reader.GetFloat(ActionPortIds.FlowDelay.Duration.Key, 1f);
            var effectiveDelaySeconds = rawDelaySeconds > 0f ? rawDelaySeconds : 1f;
            if (rawDelaySeconds <= 0f)
            {
                diagnostics.Add(ActionCompilationDiagnostic.Info(
                    compilerId,
                    context.ActionId,
                    context.ActionTypeId,
                    "flow.delay.normalized",
                    $"Flow.Delay 的 Duration 已归一化为 {effectiveDelaySeconds.ToString("0.###", CultureInfo.InvariantCulture)} 秒。",
                    ActionCompilationDiagnosticStage.SemanticNormalization));
            }

            var conditionSummary = BuildDelayConditionSummary(effectiveDelaySeconds);
            return new FlowDelayCompiledData
            {
                RawDelaySeconds = rawDelaySeconds,
                EffectiveDelaySeconds = effectiveDelaySeconds,
                ConditionSummary = conditionSummary,
                Semantics = new SemanticDescriptorSet
                {
                    Values = new[]
                    {
                        new ValueSemanticDescriptor
                        {
                            Kind = "flow.delay.duration",
                            Key = "duration",
                            ValueType = "float",
                            RawValue = rawDelaySeconds.ToString("0.###", CultureInfo.InvariantCulture),
                            NormalizedValue = effectiveDelaySeconds.ToString("0.###", CultureInfo.InvariantCulture),
                            Summary = conditionSummary,
                        },
                    },
                },
            };
        }

        private static string[] ResolveIncomingActionIds(ActionCompilationContext context)
        {
            var joinInputPortIds = ResolveJoinInputPortIds(context.Definition);
            var incomingActionIds = new List<string>();
            var seenActionIds = new HashSet<string>(StringComparer.Ordinal);

            var transitions = context.BlueprintData?.Transitions ?? Array.Empty<TransitionEntry>();
            for (var index = 0; index < transitions.Length; index++)
            {
                var transition = transitions[index];
                if (!string.Equals(transition.ToActionId, context.ActionId, StringComparison.Ordinal)
                    || !ContainsPortId(joinInputPortIds, transition.ToPortId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(transition.FromActionId))
                {
                    AddIncomingActionId(incomingActionIds, seenActionIds, transition.FromActionId);
                }
            }

            if (incomingActionIds.Count > 0 || context.Graph == null)
            {
                return incomingActionIds.ToArray();
            }

            foreach (var edge in context.Graph.Edges)
            {
                var sourcePort = context.Graph.FindPort(edge.SourcePortId);
                var targetPort = context.Graph.FindPort(edge.TargetPortId);
                if (sourcePort == null
                    || targetPort == null
                    || sourcePort.Kind == PortKind.Data
                    || !string.Equals(targetPort.NodeId, context.ActionId, StringComparison.Ordinal)
                    || !ContainsPortId(joinInputPortIds, targetPort.SemanticId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sourcePort.NodeId))
                {
                    AddIncomingActionId(incomingActionIds, seenActionIds, sourcePort.NodeId);
                }
            }

            return incomingActionIds.ToArray();
        }

        private static void AddIncomingActionId(List<string> incomingActionIds, HashSet<string> seenActionIds, string actionId)
        {
            var normalizedActionId = actionId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedActionId) || !seenActionIds.Add(normalizedActionId))
            {
                return;
            }

            incomingActionIds.Add(normalizedActionId);
        }

        private static string[] ResolveJoinInputPortIds(ActionDefinition? definition)
        {
            if (definition == null)
            {
                return new[] { ActionPortIds.FlowJoin.In };
            }

            var joinPorts = definition.FindPortsByGraphRole(PortGraphRole.JoinInput);
            if (joinPorts.Length == 0)
            {
                return new[] { ActionPortIds.FlowJoin.In };
            }

            var result = new string[joinPorts.Length];
            for (var index = 0; index < joinPorts.Length; index++)
            {
                result[index] = joinPorts[index].Id;
            }

            return result;
        }

        private static bool ContainsPortId(string[] portIds, string? portId)
        {
            if (string.IsNullOrWhiteSpace(portId))
            {
                return false;
            }

            for (var index = 0; index < portIds.Length; index++)
            {
                if (string.Equals(portIds[index], portId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveFallbackRequiredCount(ActionEntry action)
        {
            var properties = action.Properties ?? Array.Empty<PropertyValue>();
            for (var index = 0; index < properties.Length; index++)
            {
                var property = properties[index];
                if (string.Equals(property.Key, ActionPortIds.FlowJoin.InEdgeCount.Key, StringComparison.Ordinal)
                    && int.TryParse(property.Value, out var count)
                    && count > 0)
                {
                    return count;
                }
            }

            return 1;
        }

        private static PropertyValueReader CreatePropertyReader(ActionCompilationContext context)
        {
            var definition = context.Definition;
            if (definition == null)
            {
                context.ActionRegistry.TryGet(context.ActionTypeId, out definition!);
            }

            return new PropertyValueReader(context.Action, definition);
        }

        private static string BuildDelayConditionSummary(float delaySeconds)
        {
            return $"延迟 {delaySeconds.ToString("0.###", CultureInfo.InvariantCulture)} 秒";
        }

        private static SemanticDescriptorSet ResolveSemantics(FlowCompiledAction compiledAction)
        {
            return compiledAction.Join?.Semantics
                   ?? compiledAction.Filter?.Semantics
                   ?? compiledAction.Branch?.Semantics
                   ?? compiledAction.Delay?.Semantics
                   ?? new SemanticDescriptorSet();
        }

        private static DebugProjectionReadbackSummary BuildReadbackSummary(FlowCompiledAction compiledAction)
        {
            if (compiledAction.Join != null)
            {
                var incomingSummary = string.IsNullOrWhiteSpace(compiledAction.Join.IncomingActionSummary)
                    ? "无上游输入"
                    : compiledAction.Join.IncomingActionSummary;
                return new DebugProjectionReadbackSummary(
                    definitionSummary: compiledAction.Join.ConditionSummary,
                    semanticSummary: incomingSummary,
                    planSummary: $"{compiledAction.Join.ConditionSummary} | {incomingSummary}");
            }

            if (compiledAction.Filter != null)
            {
                return new DebugProjectionReadbackSummary(
                    definitionSummary: compiledAction.Filter.ConditionSummary,
                    semanticSummary: compiledAction.Filter.ConditionSummary,
                    planSummary: $"{compiledAction.Filter.Operator} {compiledAction.Filter.ConstValueText}");
            }

            if (compiledAction.Branch != null)
            {
                var routeSummary = FlowBranchAuthoringUtility.BuildRouteSummary(
                    compiledAction.Branch.ConditionValue,
                    compiledAction.Branch.RoutedPort);
                return new DebugProjectionReadbackSummary(
                    definitionSummary: routeSummary,
                    semanticSummary: routeSummary,
                    planSummary: routeSummary);
            }

            if (compiledAction.Delay != null)
            {
                return new DebugProjectionReadbackSummary(
                    definitionSummary: compiledAction.Delay.ConditionSummary,
                    semanticSummary: compiledAction.Delay.ConditionSummary,
                    planSummary: compiledAction.Delay.ConditionSummary);
            }

            return DebugProjectionReadbackSummary.Empty;
        }
    }
}
