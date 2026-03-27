#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 第一版正式语义对象 helper。
    /// 负责把既有 compiled metadata / event context 的散点语义归一化成 descriptor set，
    /// 再由 Inspector、Snapshot、Runtime debug 等消费同一种结构。
    /// </summary>
    public static class SemanticDescriptorUtility
    {
        public static SubjectSemanticDescriptor BuildSubjectDescriptor(
            string slot,
            CompiledEntityRefInfo? compiledRefInfo,
            string? fallbackSerialized = null,
            string fallbackSummary = "")
        {
            var resolved = CompiledEntityRefSemanticUtility.Resolve(compiledRefInfo, fallbackSerialized, fallbackSummary);
            return new SubjectSemanticDescriptor
            {
                Slot = NormalizeSlot(slot, "subject"),
                Kind = "entity-ref",
                Reference = resolved.Serialized,
                Summary = resolved.Summary,
            };
        }

        public static SubjectSemanticDescriptor BuildSubjectDescriptor(
            string slot,
            string? serializedRef,
            string? summary = null)
        {
            var normalizedSerialized = CompiledEntityRefSemanticUtility.NormalizeSerialized(serializedRef);
            ResolveIdentityHints(normalizedSerialized, out var compiledSubjectId, out var publicSubjectId, out var runtimeEntityId);
            return new SubjectSemanticDescriptor
            {
                Slot = NormalizeSlot(slot, "subject"),
                Kind = "entity-ref",
                Reference = normalizedSerialized,
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.DescribeEntityRef(normalizedSerialized)
                    : summary.Trim(),
                CompiledSubjectId = compiledSubjectId,
                PublicSubjectId = publicSubjectId,
                RuntimeEntityId = runtimeEntityId,
            };
        }

        public static SubjectSemanticDescriptor BuildSubjectDescriptor(
            string slot,
            string kind,
            string? reference,
            string? summary,
            string? compiledSubjectId,
            string? publicSubjectId,
            string? runtimeEntityId)
        {
            return new SubjectSemanticDescriptor
            {
                Slot = NormalizeSlot(slot, "subject"),
                Kind = string.IsNullOrWhiteSpace(kind) ? "subject" : kind.Trim(),
                Reference = reference?.Trim() ?? string.Empty,
                Summary = summary?.Trim() ?? string.Empty,
                CompiledSubjectId = compiledSubjectId?.Trim() ?? string.Empty,
                PublicSubjectId = publicSubjectId?.Trim() ?? string.Empty,
                RuntimeEntityId = runtimeEntityId?.Trim() ?? string.Empty,
            };
        }

        public static TargetSemanticDescriptor BuildTargetDescriptor(
            string slot,
            CompiledEntityRefInfo? compiledRefInfo,
            string? fallbackSerialized = null,
            string fallbackSummary = "")
        {
            var resolved = CompiledEntityRefSemanticUtility.Resolve(compiledRefInfo, fallbackSerialized, fallbackSummary);
            return new TargetSemanticDescriptor
            {
                Slot = NormalizeSlot(slot, "target"),
                Kind = "entity-ref",
                Reference = resolved.Serialized,
                Summary = resolved.Summary,
            };
        }

        public static TargetSemanticDescriptor BuildTargetDescriptor(
            string slot,
            CompiledSceneBindingInfo? sceneBindingInfo,
            string fallbackSummary = "")
        {
            return new TargetSemanticDescriptor
            {
                Slot = NormalizeSlot(slot, "target"),
                Kind = "scene-binding",
                Reference = sceneBindingInfo?.StableObjectId?.Trim() ?? sceneBindingInfo?.SceneObjectId?.Trim() ?? string.Empty,
                Summary = string.IsNullOrWhiteSpace(sceneBindingInfo?.Summary)
                    ? fallbackSummary ?? string.Empty
                    : sceneBindingInfo!.Summary.Trim(),
                BindingKey = sceneBindingInfo?.BindingKey?.Trim() ?? string.Empty,
                BindingType = sceneBindingInfo?.BindingType?.Trim() ?? string.Empty,
                StableObjectId = sceneBindingInfo?.StableObjectId?.Trim() ?? string.Empty,
                SceneObjectId = sceneBindingInfo?.SceneObjectId?.Trim() ?? string.Empty,
            };
        }

        public static TargetSemanticDescriptor BuildTargetDescriptor(
            string slot,
            string? serializedRef,
            string? summary = null)
        {
            var normalizedSerialized = CompiledEntityRefSemanticUtility.NormalizeSerialized(serializedRef);
            ResolveIdentityHints(normalizedSerialized, out var compiledSubjectId, out var publicSubjectId, out var runtimeEntityId);
            return new TargetSemanticDescriptor
            {
                Slot = NormalizeSlot(slot, "target"),
                Kind = "entity-ref",
                Reference = normalizedSerialized,
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.DescribeEntityRef(normalizedSerialized)
                    : summary.Trim(),
                CompiledSubjectId = compiledSubjectId,
                PublicSubjectId = publicSubjectId,
                RuntimeEntityId = runtimeEntityId,
            };
        }

        public static TargetSemanticDescriptor BuildTargetDescriptor(
            string slot,
            string kind,
            string? reference,
            string? summary)
        {
            return new TargetSemanticDescriptor
            {
                Slot = NormalizeSlot(slot, "target"),
                Kind = string.IsNullOrWhiteSpace(kind) ? "target" : kind.Trim(),
                Reference = reference?.Trim() ?? string.Empty,
                Summary = summary?.Trim() ?? string.Empty,
            };
        }

        public static TargetSemanticDescriptor BuildTargetDescriptor(
            string slot,
            string kind,
            string? reference,
            string? summary,
            string? compiledSubjectId,
            string? publicSubjectId,
            string? runtimeEntityId)
        {
            return new TargetSemanticDescriptor
            {
                Slot = NormalizeSlot(slot, "target"),
                Kind = string.IsNullOrWhiteSpace(kind) ? "target" : kind.Trim(),
                Reference = reference?.Trim() ?? string.Empty,
                Summary = summary?.Trim() ?? string.Empty,
                CompiledSubjectId = compiledSubjectId?.Trim() ?? string.Empty,
                PublicSubjectId = publicSubjectId?.Trim() ?? string.Empty,
                RuntimeEntityId = runtimeEntityId?.Trim() ?? string.Empty,
            };
        }

        public static ConditionSemanticDescriptor BuildWaitSignalConditionDescriptor(
            string? signalTag,
            string? subjectFilterSummary,
            bool isWildcardPattern,
            float timeoutSeconds)
        {
            var normalizedTag = WaitSignalSemanticUtility.NormalizeSignalTag(signalTag);
            return new ConditionSemanticDescriptor
            {
                Kind = "signal.wait",
                Type = "WaitSignal",
                Summary = SemanticSummaryUtility.BuildWaitSignalSummary(
                    normalizedTag,
                    subjectFilterSummary,
                    isWildcardPattern),
                ParameterSummary = isWildcardPattern ? "通配匹配" : "精确匹配",
                SignalTag = normalizedTag,
                IsWildcardPattern = isWildcardPattern,
                TimeoutSeconds = Math.Max(0f, timeoutSeconds),
            };
        }

        public static ConditionSemanticDescriptor BuildWatchConditionDescriptor(
            string? conditionType,
            string? targetSummary,
            string? parameterSummary,
            string? parametersRaw,
            float timeoutSeconds,
            bool repeat)
        {
            var normalizedConditionType = ConditionWatchSemanticUtility.NormalizeConditionType(conditionType);
            return new ConditionSemanticDescriptor
            {
                Kind = "signal.watch",
                Type = normalizedConditionType,
                Summary = SemanticSummaryUtility.BuildWatchConditionSummary(
                    normalizedConditionType,
                    targetSummary,
                    parameterSummary),
                ParameterSummary = parameterSummary?.Trim() ?? string.Empty,
                ParametersRaw = parametersRaw?.Trim() ?? string.Empty,
                TimeoutSeconds = Math.Max(0f, timeoutSeconds),
                Repeat = repeat,
            };
        }

        public static ConditionSemanticDescriptor BuildCompositeConditionDescriptor(
            string? mode,
            IReadOnlyList<string>? connectedPortIds,
            float timeoutSeconds,
            string? summary = null)
        {
            var normalizedMode = SemanticSummaryUtility.NormalizeCompositeConditionMode(mode);
            return new ConditionSemanticDescriptor
            {
                Kind = "signal.composite",
                Type = "CompositeCondition",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.BuildCompositeConditionSummary(normalizedMode, connectedPortIds)
                    : summary.Trim(),
                Mode = normalizedMode,
                TimeoutSeconds = Math.Max(0f, timeoutSeconds),
                ConnectedPortIds = connectedPortIds == null ? Array.Empty<string>() : Copy(connectedPortIds),
            };
        }

        public static ConditionSemanticDescriptor BuildFlowFilterConditionDescriptor(
            string? op,
            string? constValue,
            string? summary = null)
        {
            return new ConditionSemanticDescriptor
            {
                Kind = "flow.filter",
                Type = "FlowFilter",
                Operator = op?.Trim() ?? "==",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.BuildFlowFilterConditionSummary(op, constValue)
                    : summary.Trim(),
                ParameterSummary = constValue?.Trim() ?? string.Empty,
            };
        }

        public static ConditionSemanticDescriptor BuildTriggerEnterAreaConditionDescriptor(
            string? subjectSummary,
            string? areaSummary,
            bool requireFullyInside,
            string? summary = null)
        {
            return new ConditionSemanticDescriptor
            {
                Kind = "trigger.enter-area",
                Type = "TriggerEnterArea",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.BuildTriggerEnterAreaSummary(subjectSummary, areaSummary, requireFullyInside)
                    : summary.Trim(),
                ParameterSummary = requireFullyInside ? "完全进入" : "中心点进入",
            };
        }

        public static ConditionSemanticDescriptor BuildInteractionApproachConditionDescriptor(
            string? subjectSummary,
            string? targetSummary,
            float range,
            string? summary = null)
        {
            return new ConditionSemanticDescriptor
            {
                Kind = "interaction.approach-target",
                Type = "InteractionApproachTarget",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.BuildInteractionApproachSummary(subjectSummary, targetSummary, range)
                    : summary.Trim(),
                ParameterSummary = range.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        public static GraphSemanticDescriptor BuildFlowJoinGraphDescriptor(
            int requiredCount,
            IReadOnlyList<string>? incomingActionIds,
            string? summary = null)
        {
            return new GraphSemanticDescriptor
            {
                Kind = "flow.join",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.BuildFlowJoinConditionSummary(requiredCount)
                    : summary.Trim(),
                RequiredCount = Math.Max(1, requiredCount),
                IncomingActionIds = incomingActionIds == null ? Array.Empty<string>() : Copy(incomingActionIds),
            };
        }

        public static GraphSemanticDescriptor BuildFlowBranchGraphDescriptor(
            bool conditionValue,
            string? routedPort,
            string? summary = null)
        {
            return new GraphSemanticDescriptor
            {
                Kind = "flow.branch",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.BuildFlowBranchRouteSummary(conditionValue, routedPort)
                    : summary.Trim(),
                Mode = conditionValue ? "true" : "false",
                RoutedPort = routedPort?.Trim() ?? string.Empty,
            };
        }

        public static GraphSemanticDescriptor BuildCompositeGraphDescriptor(
            string? mode,
            int connectedCount,
            int connectedMask,
            IReadOnlyList<string>? connectedPortIds,
            string? summary = null)
        {
            var normalizedMode = SemanticSummaryUtility.NormalizeCompositeConditionMode(mode);
            return new GraphSemanticDescriptor
            {
                Kind = "signal.composite",
                Summary = string.IsNullOrWhiteSpace(summary)
                    ? SemanticSummaryUtility.BuildCompositeConditionSummary(normalizedMode, connectedPortIds)
                    : summary.Trim(),
                Mode = normalizedMode,
                ConnectedCount = Math.Max(0, connectedCount),
                ConnectedMask = Math.Max(0, connectedMask),
                ConnectedPortIds = connectedPortIds == null ? Array.Empty<string>() : Copy(connectedPortIds),
            };
        }

        public static ValueSemanticDescriptor BuildBlackboardValueDescriptor(
            string? accessKind,
            string? key,
            string? valueType,
            string? rawValue,
            string? normalizedValue,
            string? variableSummary)
        {
            return new ValueSemanticDescriptor
            {
                Kind = NormalizeValueKind(accessKind),
                Key = key?.Trim() ?? string.Empty,
                ValueType = valueType?.Trim() ?? string.Empty,
                RawValue = rawValue?.Trim() ?? string.Empty,
                NormalizedValue = normalizedValue?.Trim() ?? string.Empty,
                Summary = SemanticSummaryUtility.BuildBlackboardAccessSummary(
                    accessKind,
                    variableSummary,
                    string.IsNullOrWhiteSpace(normalizedValue) ? rawValue : normalizedValue),
            };
        }

        public static EventContextSemanticDescriptor BuildEventContextDescriptor(
            string? eventKind,
            string? signalTag,
            string? subjectSummary,
            string? payloadSummary,
            string? instigatorSummary = null,
            string? targetSummary = null)
        {
            var parts = new List<string>(6);
            var normalizedEventKind = eventKind?.Trim() ?? string.Empty;
            var normalizedSignalTag = signalTag?.Trim() ?? string.Empty;
            var normalizedSubjectSummary = subjectSummary?.Trim() ?? string.Empty;
            var normalizedPayloadSummary = payloadSummary?.Trim() ?? string.Empty;
            var normalizedInstigatorSummary = instigatorSummary?.Trim() ?? string.Empty;
            var normalizedTargetSummary = targetSummary?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(normalizedEventKind))
            {
                parts.Add(normalizedEventKind);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSignalTag))
            {
                parts.Add(normalizedSignalTag);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSubjectSummary))
            {
                parts.Add($"主体 {normalizedSubjectSummary}");
            }

            if (!string.IsNullOrWhiteSpace(normalizedInstigatorSummary))
            {
                parts.Add($"发起者 {normalizedInstigatorSummary}");
            }

            if (!string.IsNullOrWhiteSpace(normalizedTargetSummary))
            {
                parts.Add($"目标 {normalizedTargetSummary}");
            }

            if (!string.IsNullOrWhiteSpace(normalizedPayloadSummary))
            {
                parts.Add($"载荷 {normalizedPayloadSummary}");
            }

            return new EventContextSemanticDescriptor
            {
                Kind = "blueprint-event-context",
                EventKind = normalizedEventKind,
                SignalTag = normalizedSignalTag,
                PayloadSummary = normalizedPayloadSummary,
                Summary = parts.Count == 0
                    ? nameof(BlueprintEventContext)
                    : string.Join(" | ", parts),
            };
        }

        public static SemanticDescriptorSet BuildEventContextDescriptorSet(BlueprintEventContext? eventContext)
        {
            if (eventContext == null)
            {
                return new SemanticDescriptorSet();
            }

            return new SemanticDescriptorSet
            {
                Subjects = new[]
                {
                    BuildSubjectDescriptor("subject", eventContext.SubjectRefSerialized, eventContext.SubjectSummary),
                    BuildSubjectDescriptor("instigator", eventContext.InstigatorRefSerialized, eventContext.InstigatorSummary),
                },
                Targets = new[]
                {
                    BuildTargetDescriptor("target", eventContext.TargetRefSerialized, eventContext.TargetSummary),
                },
                EventContexts = new[]
                {
                    BuildEventContextDescriptor(
                        eventContext.EventKind,
                        eventContext.SignalTag,
                        eventContext.SubjectSummary,
                        eventContext.PayloadSummary,
                        eventContext.InstigatorSummary,
                        eventContext.TargetSummary),
                },
            };
        }

        public static string GetSubjectSummary(SemanticDescriptorSet? semantics, string slot = "subject", string fallback = "")
        {
            var subjects = semantics?.Subjects;
            if (subjects != null)
            {
                for (var index = 0; index < subjects.Length; index++)
                {
                    var descriptor = subjects[index];
                    if (!string.IsNullOrWhiteSpace(slot)
                        && !string.Equals(descriptor.Slot, slot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(descriptor.Summary))
                    {
                        return descriptor.Summary.Trim();
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        public static string GetSubjectReference(SemanticDescriptorSet? semantics, string slot = "subject", string fallback = "")
        {
            var descriptor = FindSubject(semantics, slot);
            return !string.IsNullOrWhiteSpace(descriptor?.Reference)
                ? descriptor.Reference.Trim()
                : fallback ?? string.Empty;
        }

        public static bool TryGetSubjectDescriptor(
            SemanticDescriptorSet? semantics,
            string slot,
            out SubjectSemanticDescriptor? descriptor)
        {
            descriptor = FindSubject(semantics, slot);
            return descriptor != null;
        }

        public static string GetSubjectCompiledSubjectId(SemanticDescriptorSet? semantics, string slot = "subject", string fallback = "")
        {
            var descriptor = FindSubject(semantics, slot);
            return !string.IsNullOrWhiteSpace(descriptor?.CompiledSubjectId)
                ? descriptor.CompiledSubjectId.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetSubjectPublicSubjectId(SemanticDescriptorSet? semantics, string slot = "subject", string fallback = "")
        {
            var descriptor = FindSubject(semantics, slot);
            return !string.IsNullOrWhiteSpace(descriptor?.PublicSubjectId)
                ? descriptor.PublicSubjectId.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetSubjectRuntimeEntityId(SemanticDescriptorSet? semantics, string slot = "subject", string fallback = "")
        {
            var descriptor = FindSubject(semantics, slot);
            return !string.IsNullOrWhiteSpace(descriptor?.RuntimeEntityId)
                ? descriptor.RuntimeEntityId.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetSubjectIdentitySummary(
            SemanticDescriptorSet? semantics,
            string slot = "subject",
            bool includeSlot = false,
            string fallback = "")
        {
            return TryGetSubjectDescriptor(semantics, slot, out var descriptor)
                ? SemanticDescriptorIdentityUtility.BuildSubjectIdentityLine(descriptor, includeSlot)
                : fallback ?? string.Empty;
        }

        public static string GetTargetSummary(SemanticDescriptorSet? semantics, string slot = "target", string fallback = "")
        {
            var targets = semantics?.Targets;
            if (targets != null)
            {
                for (var index = 0; index < targets.Length; index++)
                {
                    var descriptor = targets[index];
                    if (!string.IsNullOrWhiteSpace(slot)
                        && !string.Equals(descriptor.Slot, slot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(descriptor.Summary))
                    {
                        return descriptor.Summary.Trim();
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        public static string GetTargetReference(SemanticDescriptorSet? semantics, string slot = "target", string fallback = "")
        {
            var descriptor = FindTarget(semantics, slot);
            return !string.IsNullOrWhiteSpace(descriptor?.Reference)
                ? descriptor.Reference.Trim()
                : fallback ?? string.Empty;
        }

        public static bool TryGetTargetDescriptor(
            SemanticDescriptorSet? semantics,
            string slot,
            out TargetSemanticDescriptor? descriptor)
        {
            descriptor = FindTarget(semantics, slot);
            return descriptor != null;
        }

        public static string GetTargetCompiledSubjectId(SemanticDescriptorSet? semantics, string slot = "target", string fallback = "")
        {
            var descriptor = FindTarget(semantics, slot);
            return !string.IsNullOrWhiteSpace(descriptor?.CompiledSubjectId)
                ? descriptor.CompiledSubjectId.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetTargetPublicSubjectId(SemanticDescriptorSet? semantics, string slot = "target", string fallback = "")
        {
            var descriptor = FindTarget(semantics, slot);
            return !string.IsNullOrWhiteSpace(descriptor?.PublicSubjectId)
                ? descriptor.PublicSubjectId.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetTargetRuntimeEntityId(SemanticDescriptorSet? semantics, string slot = "target", string fallback = "")
        {
            var descriptor = FindTarget(semantics, slot);
            return !string.IsNullOrWhiteSpace(descriptor?.RuntimeEntityId)
                ? descriptor.RuntimeEntityId.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetTargetIdentitySummary(
            SemanticDescriptorSet? semantics,
            string slot = "target",
            bool includeSlot = false,
            string fallback = "")
        {
            return TryGetTargetDescriptor(semantics, slot, out var descriptor)
                ? SemanticDescriptorIdentityUtility.BuildTargetIdentityLine(descriptor, includeSlot)
                : fallback ?? string.Empty;
        }

        public static string GetConditionSummary(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var conditions = semantics?.Conditions;
            if (conditions != null)
            {
                for (var index = 0; index < conditions.Length; index++)
                {
                    if (!string.IsNullOrWhiteSpace(conditions[index].Summary))
                    {
                        return conditions[index].Summary.Trim();
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        public static string GetConditionType(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var descriptor = FindCondition(semantics);
            return !string.IsNullOrWhiteSpace(descriptor?.Type)
                ? descriptor.Type.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetConditionParametersRaw(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var descriptor = FindCondition(semantics);
            return !string.IsNullOrWhiteSpace(descriptor?.ParametersRaw)
                ? descriptor.ParametersRaw.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetConditionSignalTag(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var descriptor = FindCondition(semantics);
            return !string.IsNullOrWhiteSpace(descriptor?.SignalTag)
                ? descriptor.SignalTag.Trim()
                : fallback ?? string.Empty;
        }

        public static float GetConditionTimeoutSeconds(SemanticDescriptorSet? semantics, float fallback = 0f)
        {
            var descriptor = FindCondition(semantics);
            return descriptor != null
                ? Math.Max(0f, descriptor.TimeoutSeconds)
                : Math.Max(0f, fallback);
        }

        public static bool GetConditionRepeat(SemanticDescriptorSet? semantics, bool fallback = false)
        {
            var descriptor = FindCondition(semantics);
            return descriptor?.Repeat ?? fallback;
        }

        public static bool GetConditionIsWildcardPattern(SemanticDescriptorSet? semantics, bool fallback = false)
        {
            var descriptor = FindCondition(semantics);
            return descriptor?.IsWildcardPattern ?? fallback;
        }

        public static string GetGraphSummary(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var graphs = semantics?.Graphs;
            if (graphs != null)
            {
                for (var index = 0; index < graphs.Length; index++)
                {
                    if (!string.IsNullOrWhiteSpace(graphs[index].Summary))
                    {
                        return graphs[index].Summary.Trim();
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        public static bool TryGetGraphDescriptor(
            SemanticDescriptorSet? semantics,
            string? expectedKind,
            out GraphSemanticDescriptor? descriptor)
        {
            descriptor = null;
            var graphs = semantics?.Graphs;
            if (graphs == null)
            {
                return false;
            }

            for (var index = 0; index < graphs.Length; index++)
            {
                var candidate = graphs[index];
                if (candidate == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(expectedKind)
                    && !string.Equals(candidate.Kind, expectedKind, StringComparison.Ordinal))
                {
                    continue;
                }

                descriptor = candidate;
                return true;
            }

            return false;
        }

        public static string GetValueSummary(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var values = semantics?.Values;
            if (values != null)
            {
                for (var index = 0; index < values.Length; index++)
                {
                    if (!string.IsNullOrWhiteSpace(values[index].Summary))
                    {
                        return values[index].Summary.Trim();
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        public static string GetValueNormalized(SemanticDescriptorSet? semantics, string key, string fallback = "")
        {
            var descriptor = FindValue(semantics, key);
            return !string.IsNullOrWhiteSpace(descriptor?.NormalizedValue)
                ? descriptor.NormalizedValue.Trim()
                : fallback ?? string.Empty;
        }

        public static string GetValueRaw(SemanticDescriptorSet? semantics, string key, string fallback = "")
        {
            var descriptor = FindValue(semantics, key);
            return !string.IsNullOrWhiteSpace(descriptor?.RawValue)
                ? descriptor.RawValue.Trim()
                : fallback ?? string.Empty;
        }

        public static float GetValueFloat(SemanticDescriptorSet? semantics, string key, float fallback = 0f)
        {
            var text = GetValueNormalized(semantics, key);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = GetValueRaw(semantics, key);
            }

            return float.TryParse(
                text,
                System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
                ? value
                : fallback;
        }

        public static bool GetValueBool(SemanticDescriptorSet? semantics, string key, bool fallback = false)
        {
            var text = GetValueNormalized(semantics, key);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = GetValueRaw(semantics, key);
            }

            if (bool.TryParse(text, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(text, out var intValue))
            {
                return intValue != 0;
            }

            return fallback;
        }

        public static string GetEventSummary(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var eventContexts = semantics?.EventContexts;
            if (eventContexts != null)
            {
                for (var index = 0; index < eventContexts.Length; index++)
                {
                    if (!string.IsNullOrWhiteSpace(eventContexts[index].Summary))
                    {
                        return eventContexts[index].Summary.Trim();
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        public static string GetEventSignalTag(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var eventContexts = semantics?.EventContexts;
            if (eventContexts != null)
            {
                for (var index = 0; index < eventContexts.Length; index++)
                {
                    if (!string.IsNullOrWhiteSpace(eventContexts[index].SignalTag))
                    {
                        return eventContexts[index].SignalTag.Trim();
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        public static string GetEventKind(SemanticDescriptorSet? semantics, string fallback = "")
        {
            var eventContexts = semantics?.EventContexts;
            if (eventContexts != null)
            {
                for (var index = 0; index < eventContexts.Length; index++)
                {
                    if (!string.IsNullOrWhiteSpace(eventContexts[index].EventKind))
                    {
                        return eventContexts[index].EventKind.Trim();
                    }
                }
            }

            return fallback ?? string.Empty;
        }

        private static string NormalizeSlot(string? slot, string fallback)
        {
            return string.IsNullOrWhiteSpace(slot)
                ? fallback
                : slot.Trim();
        }

        private static SubjectSemanticDescriptor? FindSubject(SemanticDescriptorSet? semantics, string slot)
        {
            var subjects = semantics?.Subjects;
            if (subjects == null)
            {
                return null;
            }

            for (var index = 0; index < subjects.Length; index++)
            {
                var descriptor = subjects[index];
                if (!string.IsNullOrWhiteSpace(slot)
                    && !string.Equals(descriptor.Slot, slot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return descriptor;
            }

            return null;
        }

        private static ValueSemanticDescriptor? FindValue(SemanticDescriptorSet? semantics, string key)
        {
            var values = semantics?.Values;
            if (values == null)
            {
                return null;
            }

            for (var index = 0; index < values.Length; index++)
            {
                var descriptor = values[index];
                if (descriptor == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(key)
                    && !string.Equals(descriptor.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return descriptor;
            }

            return null;
        }

        private static void ResolveIdentityHints(
            string serializedRef,
            out string compiledSubjectId,
            out string publicSubjectId,
            out string runtimeEntityId)
        {
            compiledSubjectId = string.Empty;
            publicSubjectId = string.Empty;
            runtimeEntityId = string.Empty;
            if (string.IsNullOrWhiteSpace(serializedRef))
            {
                return;
            }

            var entityRef = EntityRefCodec.Parse(serializedRef);
            switch (entityRef.Mode)
            {
                case EntityRefMode.ByRole:
                    compiledSubjectId = entityRef.Role?.Trim() ?? string.Empty;
                    break;
                case EntityRefMode.ByAlias:
                    publicSubjectId = entityRef.Alias?.Trim() ?? string.Empty;
                    break;
            }
        }

        private static TargetSemanticDescriptor? FindTarget(SemanticDescriptorSet? semantics, string slot)
        {
            var targets = semantics?.Targets;
            if (targets == null)
            {
                return null;
            }

            for (var index = 0; index < targets.Length; index++)
            {
                var descriptor = targets[index];
                if (!string.IsNullOrWhiteSpace(slot)
                    && !string.Equals(descriptor.Slot, slot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return descriptor;
            }

            return null;
        }

        private static ConditionSemanticDescriptor? FindCondition(SemanticDescriptorSet? semantics)
        {
            var conditions = semantics?.Conditions;
            if (conditions == null)
            {
                return null;
            }

            for (var index = 0; index < conditions.Length; index++)
            {
                var descriptor = conditions[index];
                if (!string.IsNullOrWhiteSpace(descriptor.Type)
                    || !string.IsNullOrWhiteSpace(descriptor.SignalTag)
                    || !string.IsNullOrWhiteSpace(descriptor.Summary))
                {
                    return descriptor;
                }
            }

            return null;
        }

        private static string NormalizeValueKind(string? accessKind)
        {
            return accessKind?.Trim().ToLowerInvariant() switch
            {
                "get" => "blackboard.get",
                "set" => "blackboard.set",
                _ => "value",
            };
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            var result = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                result[index] = values[index] ?? string.Empty;
            }

            return result;
        }
    }
}
