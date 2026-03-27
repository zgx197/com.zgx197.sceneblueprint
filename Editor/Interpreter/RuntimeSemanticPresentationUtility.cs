#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Editor.Interpreter
{
    internal sealed class RuntimeSemanticPayloadProjection
    {
        public RuntimeSemanticPayloadProjection(
            string summary,
            IReadOnlyList<RuntimeSnapshotBrowserDetailRowViewModel> details,
            IReadOnlyList<string> consumedMemberNames)
        {
            Summary = summary ?? string.Empty;
            Details = details ?? Array.Empty<RuntimeSnapshotBrowserDetailRowViewModel>();
            ConsumedMemberNames = consumedMemberNames ?? Array.Empty<string>();
        }

        public string Summary { get; }

        public IReadOnlyList<RuntimeSnapshotBrowserDetailRowViewModel> Details { get; }

        public IReadOnlyList<string> ConsumedMemberNames { get; }
    }

    internal static class RuntimeSemanticPresentationUtility
    {
        public static string BuildEventSummary(
            string? eventKind,
            string? signalTag,
            string? subjectRefSerialized,
            string? subjectSummary,
            string? instigatorRefSerialized,
            string? instigatorSummary,
            string? targetRefSerialized,
            string? targetSummary,
            string? payloadSummary)
        {
            return SemanticDescriptorUtility.GetEventSummary(
                BlueprintEventContextSemanticUtility.BuildSemantics(
                    eventKind,
                    signalTag,
                    subjectRefSerialized,
                    subjectSummary,
                    instigatorRefSerialized,
                    instigatorSummary,
                    targetRefSerialized,
                    targetSummary,
                    payloadSummary),
                nameof(BlueprintEventContext));
        }

        public static SemanticDescriptorSet BuildWaitSignalSemantics(
            string? signalTag,
            string? subjectFilterSerialized,
            string? subjectFilterSummary,
            bool isWildcardPattern,
            float timeoutSeconds,
            string? fallbackConditionSummary = null)
        {
            var subject = SemanticDescriptorUtility.BuildSubjectDescriptor(
                "subject-filter",
                subjectFilterSerialized,
                subjectFilterSummary);
            return new SemanticDescriptorSet
            {
                Subjects = new[]
                {
                    subject,
                },
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildWaitSignalConditionDescriptor(
                        signalTag,
                        subject.Summary,
                        isWildcardPattern,
                        timeoutSeconds),
                },
            };
        }

        public static SemanticDescriptorSet BuildWatchConditionSemantics(
            string? conditionType,
            string? targetRefSerialized,
            string? targetSummary,
            string? parametersRaw,
            float timeoutSeconds,
            bool repeat,
            string? fallbackConditionSummary = null)
        {
            var target = SemanticDescriptorUtility.BuildTargetDescriptor(
                "target",
                "entity-ref",
                targetRefSerialized,
                targetSummary);
            var normalizedParametersRaw = ConditionWatchSemanticUtility.SerializeParameters(conditionType, parametersRaw);
            return new SemanticDescriptorSet
            {
                Targets = new[]
                {
                    target,
                },
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildWatchConditionDescriptor(
                        conditionType,
                        target.Summary,
                        ConditionWatchSemanticUtility.BuildParameterSummary(conditionType, normalizedParametersRaw),
                        normalizedParametersRaw,
                        timeoutSeconds,
                        repeat),
                },
            };
        }

        public static SemanticDescriptorSet BuildTriggerEnterAreaSemantics(
            string? subjectRefSerialized,
            string? subjectSummary,
            string? triggerAreaReference,
            string? triggerAreaSummary,
            bool requireFullyInside,
            string? fallbackConditionSummary = null)
        {
            var subject = SemanticDescriptorUtility.BuildSubjectDescriptor(
                "subject",
                subjectRefSerialized,
                subjectSummary);
            var area = SemanticDescriptorUtility.BuildTargetDescriptor(
                "area",
                "spatial-area",
                triggerAreaReference,
                triggerAreaSummary);
            return new SemanticDescriptorSet
            {
                Subjects = new[]
                {
                    subject,
                },
                Targets = new[]
                {
                    area,
                },
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildTriggerEnterAreaConditionDescriptor(
                        subject.Summary,
                        area.Summary,
                        requireFullyInside,
                        fallbackConditionSummary),
                },
            };
        }

        public static SemanticDescriptorSet BuildInteractionApproachSemantics(
            string? subjectRefSerialized,
            string? subjectSummary,
            string? targetRefSerialized,
            string? targetSummary,
            float triggerRange,
            string? fallbackConditionSummary = null)
        {
            var subject = SemanticDescriptorUtility.BuildSubjectDescriptor(
                "subject",
                subjectRefSerialized,
                subjectSummary);
            var target = SemanticDescriptorUtility.BuildTargetDescriptor(
                "target",
                "entity-ref",
                targetRefSerialized,
                targetSummary);
            return new SemanticDescriptorSet
            {
                Subjects = new[]
                {
                    subject,
                },
                Targets = new[]
                {
                    target,
                },
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildInteractionApproachConditionDescriptor(
                        subject.Summary,
                        target.Summary,
                        triggerRange,
                        fallbackConditionSummary),
                },
            };
        }

        public static SemanticDescriptorSet BuildFlowFilterSemantics(
            string? op,
            string? constValue,
            string? fallbackConditionSummary = null)
        {
            return new SemanticDescriptorSet
            {
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildFlowFilterConditionDescriptor(
                        op,
                        constValue,
                        fallbackConditionSummary),
                },
            };
        }

        public static SemanticDescriptorSet BuildFlowBranchSemantics(
            bool conditionValue,
            string? routedPort,
            string? fallbackGraphSummary = null)
        {
            return new SemanticDescriptorSet
            {
                Graphs = new[]
                {
                    SemanticDescriptorUtility.BuildFlowBranchGraphDescriptor(
                        conditionValue,
                        routedPort,
                        fallbackGraphSummary),
                },
            };
        }

        public static SemanticDescriptorSet BuildCompositeConditionSemantics(
            string? mode,
            int connectedCount,
            int connectedMask,
            string? connectedPortSummary,
            float timeoutSeconds,
            string? fallbackConditionSummary = null)
        {
            var connectedPorts = SplitSummaryTokens(connectedPortSummary);
            return new SemanticDescriptorSet
            {
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildCompositeConditionDescriptor(
                        mode,
                        connectedPorts,
                        timeoutSeconds,
                        fallbackConditionSummary),
                },
                Graphs = new[]
                {
                    SemanticDescriptorUtility.BuildCompositeGraphDescriptor(
                        mode,
                        connectedCount,
                        connectedMask,
                        connectedPorts,
                        connectedPortSummary),
                },
            };
        }

        public static SemanticDescriptorSet BuildFlowJoinSemantics(
            int requiredCount,
            string? incomingActionSummary,
            string? fallbackGraphSummary = null)
        {
            return new SemanticDescriptorSet
            {
                Graphs = new[]
                {
                    SemanticDescriptorUtility.BuildFlowJoinGraphDescriptor(
                        requiredCount,
                        SplitSummaryTokens(incomingActionSummary),
                        fallbackGraphSummary),
                },
            };
        }

        public static bool TryBuildRuntimeStatePayloadProjection(
            object payload,
            out RuntimeSemanticPayloadProjection projection)
        {
            if (TryBuildSpawnPresetPayloadProjection(payload, out projection)
                || TryBuildSpawnWavePayloadProjection(payload, out projection)
                || TryBuildWaitSignalPayloadProjection(payload, out projection)
                || TryBuildWatchConditionPayloadProjection(payload, out projection)
                || TryBuildTriggerEnterAreaPayloadProjection(payload, out projection)
                || TryBuildInteractionApproachPayloadProjection(payload, out projection))
            {
                return true;
            }

            projection = null!;
            return false;
        }

        private static bool TryBuildSpawnPresetPayloadProjection(
            object payload,
            out RuntimeSemanticPayloadProjection projection)
        {
            var requestedSpawnCount = GetInt(payload, "RequestedSpawnCount");
            var publicSubjectCount = GetInt(payload, "PublicSubjectCount");
            var subjectIdentitySummary = GetString(payload, "SubjectIdentitySummary");
            var subjects = GetSubjects(payload, "Subjects");
            if (requestedSpawnCount <= 0
                && publicSubjectCount <= 0
                && string.IsNullOrWhiteSpace(subjectIdentitySummary)
                && subjects.Length == 0)
            {
                projection = null!;
                return false;
            }

            var resolvedIdentitySummary = !string.IsNullOrWhiteSpace(subjectIdentitySummary)
                ? subjectIdentitySummary
                : SemanticDescriptorIdentityUtility.BuildSubjectIdentitySummary(subjects);
            var summary = FirstNonEmpty(
                GetString(payload, "ExecutionSummary"),
                resolvedIdentitySummary,
                GetString(payload, "PlanSummary"));
            projection = new RuntimeSemanticPayloadProjection(
                summary,
                new[]
                {
                    CreateDetail("主体身份", resolvedIdentitySummary, ObservationStage.SemanticAnalysis),
                    CreateDetail("请求数量", FormatInt(requestedSpawnCount), ObservationStage.RuntimeState),
                    CreateDetail("公共主体数量", FormatInt(publicSubjectCount), ObservationStage.RuntimeState),
                    CreateDetail("主体槽位数", FormatInt(subjects.Length), ObservationStage.RuntimeState),
                    CreateDetail("最近刷怪数量", FormatInt(GetInt(payload, "LastSpawnCount")), ObservationStage.RuntimeState),
                    CreateDetail("最近错误", GetString(payload, "LastErrorMessage"), ObservationStage.RuntimeState),
                },
                new[]
                {
                    "SubjectIdentitySummary",
                    "RequestedSpawnCount",
                    "PublicSubjectCount",
                    "Subjects",
                    "LastSpawnCount",
                    "LastErrorMessage",
                });
            return true;
        }

        private static bool TryBuildSpawnWavePayloadProjection(
            object payload,
            out RuntimeSemanticPayloadProjection projection)
        {
            var currentWaveRequestedSpawnCount = GetInt(payload, "CurrentWaveRequestedSpawnCount");
            var currentWaveSubjectIdentitySummary = GetString(payload, "CurrentWaveSubjectIdentitySummary");
            var nextWaveSubjectIdentitySummary = GetString(payload, "NextWaveSubjectIdentitySummary");
            var currentWaveSubjects = GetSubjects(payload, "CurrentWaveSubjects");
            var nextWaveSubjects = GetSubjects(payload, "NextWaveSubjects");
            var supplyProjection = BuildSpawnWaveSupplyProjection(payload);
            if (currentWaveRequestedSpawnCount <= 0
                && string.IsNullOrWhiteSpace(currentWaveSubjectIdentitySummary)
                && string.IsNullOrWhiteSpace(nextWaveSubjectIdentitySummary)
                && currentWaveSubjects.Length == 0
                && nextWaveSubjects.Length == 0
                && supplyProjection.EntryCount <= 0)
            {
                projection = null!;
                return false;
            }

            var resolvedCurrentIdentitySummary = !string.IsNullOrWhiteSpace(currentWaveSubjectIdentitySummary)
                ? currentWaveSubjectIdentitySummary
                : SemanticDescriptorIdentityUtility.BuildSubjectIdentitySummary(currentWaveSubjects);
            var resolvedNextIdentitySummary = !string.IsNullOrWhiteSpace(nextWaveSubjectIdentitySummary)
                ? nextWaveSubjectIdentitySummary
                : SemanticDescriptorIdentityUtility.BuildSubjectIdentitySummary(nextWaveSubjects);
            var summary = FirstNonEmpty(
                GetString(payload, "ExecutionSummary"),
                resolvedCurrentIdentitySummary,
                supplyProjection.InventorySummary,
                GetString(payload, "PlanSummary"));
            var details = new List<RuntimeSnapshotBrowserDetailRowViewModel>(10);
            if (supplyProjection.EntryCount > 0)
            {
                details.Add(CreateDetail("供给身份摘要", supplyProjection.IdentitySummary, ObservationStage.PlanCompilation));
                details.Add(CreateDetail("供给条目数", FormatInt(supplyProjection.EntryCount), ObservationStage.PlanCompilation));
                details.Add(CreateDetail("MonsterId 已解析", supplyProjection.ResolvedSummary, ObservationStage.PlanCompilation));
                details.Add(CreateDetail("供给库存", supplyProjection.InventorySummary, ObservationStage.RuntimeState));
            }

            details.Add(CreateDetail("当前波主体身份", resolvedCurrentIdentitySummary, ObservationStage.SemanticAnalysis));
            details.Add(CreateDetail("当前波主体槽位数", FormatInt(currentWaveSubjects.Length > 0 ? currentWaveSubjects.Length : GetInt(payload, "CurrentWaveSubjectSlotCount")), ObservationStage.RuntimeState));
            details.Add(CreateDetail("当前波公共主体数", FormatInt(GetInt(payload, "CurrentWavePublicSubjectCount")), ObservationStage.RuntimeState));
            details.Add(CreateDetail("下一波主体身份", resolvedNextIdentitySummary, ObservationStage.SemanticAnalysis));
            details.Add(CreateDetail("下一波主体槽位数", FormatInt(nextWaveSubjects.Length > 0 ? nextWaveSubjects.Length : GetInt(payload, "NextWaveSubjectSlotCount")), ObservationStage.RuntimeState));
            details.Add(CreateDetail("下一波公共主体数", FormatInt(GetInt(payload, "NextWavePublicSubjectCount")), ObservationStage.RuntimeState));
            projection = new RuntimeSemanticPayloadProjection(
                summary,
                details,
                new[]
                {
                    "Entries",
                    "TotalInitialCount",
                    "RemainingTotal",
                    "CurrentWaveSubjectIdentitySummary",
                    "CurrentWaveSubjects",
                    "CurrentWaveSubjectSlotCount",
                    "CurrentWavePublicSubjectCount",
                    "NextWaveSubjectIdentitySummary",
                    "NextWaveSubjects",
                    "NextWaveSubjectSlotCount",
                    "NextWavePublicSubjectCount",
                });
            return true;
        }

        private static bool TryBuildWaitSignalPayloadProjection(
            object payload,
            out RuntimeSemanticPayloadProjection projection)
        {
            var signalTag = GetString(payload, "SignalTag");
            var subjectFilterSerialized = GetString(payload, "SubjectRefFilterSerialized");
            var subjectFilterSummary = GetString(payload, "SubjectRefFilterSummary");
            if (string.IsNullOrWhiteSpace(signalTag)
                && string.IsNullOrWhiteSpace(subjectFilterSerialized)
                && string.IsNullOrWhiteSpace(subjectFilterSummary))
            {
                projection = null!;
                return false;
            }

            var semantics = BuildWaitSignalSemantics(
                signalTag,
                subjectFilterSerialized,
                subjectFilterSummary,
                GetBool(payload, "IsWildcardPattern"),
                GetFloat(payload, "TimeoutSeconds"),
                GetString(payload, "ConditionSummary"));
            var resolvedFilterSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "subject-filter",
                subjectFilterSummary);
            var resolvedFilterSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "subject-filter",
                subjectFilterSerialized);
            var summary = FirstNonEmpty(
                GetString(payload, "ExecutionSummary"),
                SemanticDescriptorUtility.GetConditionSummary(semantics, GetString(payload, "ConditionSummary")),
                GetString(payload, "PlanSummary"));
            projection = new RuntimeSemanticPayloadProjection(
                summary,
                new[]
                {
                    CreateDetail("主体过滤", resolvedFilterSummary, ObservationStage.SemanticAnalysis),
                    CreateDetail("主体过滤引用", resolvedFilterSerialized, ObservationStage.SemanticAnalysis),
                    CreateDetail("信号标签", SemanticDescriptorUtility.GetConditionSignalTag(semantics, signalTag), ObservationStage.SemanticAnalysis),
                    CreateDetail("通配匹配", FormatBool(GetBool(payload, "IsWildcardPattern")), ObservationStage.RuntimeState),
                },
                new[]
                {
                    "SubjectRefFilterSummary",
                    "SubjectRefFilterSerialized",
                    "SignalTag",
                    "IsWildcardPattern",
                });
            return true;
        }

        private static bool TryBuildWatchConditionPayloadProjection(
            object payload,
            out RuntimeSemanticPayloadProjection projection)
        {
            var conditionType = GetString(payload, "ConditionType");
            var targetRefSerialized = GetString(payload, "TargetRefSerialized");
            var targetSummary = GetString(payload, "TargetSummary");
            if (string.IsNullOrWhiteSpace(conditionType)
                && string.IsNullOrWhiteSpace(targetRefSerialized)
                && string.IsNullOrWhiteSpace(targetSummary))
            {
                projection = null!;
                return false;
            }

            var semantics = BuildWatchConditionSemantics(
                conditionType,
                targetRefSerialized,
                targetSummary,
                GetString(payload, "ParametersRaw"),
                GetFloat(payload, "TimeoutSeconds"),
                GetBool(payload, "Repeat"),
                GetString(payload, "ConditionSummary"));
            var resolvedTargetSummary = SemanticDescriptorUtility.GetTargetSummary(
                semantics,
                "target",
                targetSummary);
            var resolvedTargetSerialized = SemanticDescriptorUtility.GetTargetReference(
                semantics,
                "target",
                targetRefSerialized);
            var summary = FirstNonEmpty(
                GetString(payload, "ExecutionSummary"),
                SemanticDescriptorUtility.GetConditionSummary(semantics, GetString(payload, "ConditionSummary")),
                GetString(payload, "PlanSummary"));
            projection = new RuntimeSemanticPayloadProjection(
                summary,
                new[]
                {
                    CreateDetail("条件类型", SemanticDescriptorUtility.GetConditionType(semantics, conditionType), ObservationStage.SemanticAnalysis),
                    CreateDetail("目标", resolvedTargetSummary, ObservationStage.SemanticAnalysis),
                    CreateDetail("目标引用", resolvedTargetSerialized, ObservationStage.SemanticAnalysis),
                    CreateDetail("条件参数", SemanticDescriptorUtility.GetConditionParametersRaw(semantics, GetString(payload, "ParametersRaw")), ObservationStage.SemanticAnalysis),
                    CreateDetail("重复触发", FormatBool(GetBool(payload, "Repeat")), ObservationStage.RuntimeState),
                },
                new[]
                {
                    "ConditionType",
                    "TargetSummary",
                    "TargetRefSerialized",
                    "ParametersRaw",
                    "Repeat",
                });
            return true;
        }

        private static bool TryBuildTriggerEnterAreaPayloadProjection(
            object payload,
            out RuntimeSemanticPayloadProjection projection)
        {
            var subjectRefSerialized = GetString(payload, "SubjectRefSerialized");
            var subjectSummary = GetString(payload, "SubjectSummary");
            var triggerAreaSummary = GetString(payload, "TriggerAreaSummary");
            if (string.IsNullOrWhiteSpace(subjectRefSerialized)
                && string.IsNullOrWhiteSpace(subjectSummary)
                && string.IsNullOrWhiteSpace(triggerAreaSummary)
                && !HasProperty(payload, "RequireFullyInside"))
            {
                projection = null!;
                return false;
            }

            var semantics = BuildTriggerEnterAreaSemantics(
                subjectRefSerialized,
                subjectSummary,
                GetString(payload, "TriggerAreaPayloadJson"),
                triggerAreaSummary,
                GetBool(payload, "RequireFullyInside"),
                GetString(payload, "ConditionSummary"));
            var resolvedSubjectSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "subject",
                subjectSummary);
            var resolvedSubjectSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "subject",
                subjectRefSerialized);
            var resolvedAreaSummary = SemanticDescriptorUtility.GetTargetSummary(
                semantics,
                "area",
                triggerAreaSummary);
            var summary = FirstNonEmpty(
                GetString(payload, "ExecutionSummary"),
                GetString(payload, "LastWaitReason"),
                SemanticDescriptorUtility.GetConditionSummary(semantics, GetString(payload, "ConditionSummary")),
                GetString(payload, "PlanSummary"));
            projection = new RuntimeSemanticPayloadProjection(
                summary,
                new[]
                {
                    CreateDetail("主体", resolvedSubjectSummary, ObservationStage.SemanticAnalysis),
                    CreateDetail("主体引用", resolvedSubjectSerialized, ObservationStage.SemanticAnalysis),
                    CreateDetail("区域", resolvedAreaSummary, ObservationStage.SemanticAnalysis),
                    CreateDetail("需要完全进入", FormatBool(GetBool(payload, "RequireFullyInside")), ObservationStage.RuntimeState),
                    CreateDetail("已绑定区域", FormatBool(GetBool(payload, "HasTriggerAreaBinding")), ObservationStage.RuntimeState),
                    CreateDetail("退化为默认满足", FormatBool(GetBool(payload, "TreatAsAlwaysSatisfied")), ObservationStage.RuntimeState),
                    CreateDetail("当前等待原因", GetString(payload, "LastWaitReason"), ObservationStage.RuntimeState),
                },
                new[]
                {
                    "SubjectSummary",
                    "SubjectRefSerialized",
                    "TriggerAreaSummary",
                    "RequireFullyInside",
                    "HasTriggerAreaBinding",
                    "TreatAsAlwaysSatisfied",
                    "LastWaitReason",
                });
            return true;
        }

        private static bool TryBuildInteractionApproachPayloadProjection(
            object payload,
            out RuntimeSemanticPayloadProjection projection)
        {
            var subjectRefSerialized = GetString(payload, "SubjectRefSerialized");
            var subjectSummary = GetString(payload, "SubjectSummary");
            var targetRefSerialized = GetString(payload, "TargetRefSerialized");
            var targetSummary = GetString(payload, "TargetSummary");
            if (string.IsNullOrWhiteSpace(subjectRefSerialized)
                && string.IsNullOrWhiteSpace(subjectSummary)
                && string.IsNullOrWhiteSpace(targetRefSerialized)
                && string.IsNullOrWhiteSpace(targetSummary)
                && !HasProperty(payload, "TriggerRange"))
            {
                projection = null!;
                return false;
            }

            var semantics = BuildInteractionApproachSemantics(
                subjectRefSerialized,
                subjectSummary,
                targetRefSerialized,
                targetSummary,
                GetFloat(payload, "TriggerRange"),
                GetString(payload, "ConditionSummary"));
            var resolvedSubjectSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "subject",
                subjectSummary);
            var resolvedSubjectSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "subject",
                subjectRefSerialized);
            var resolvedTargetSummary = SemanticDescriptorUtility.GetTargetSummary(
                semantics,
                "target",
                targetSummary);
            var resolvedTargetSerialized = SemanticDescriptorUtility.GetTargetReference(
                semantics,
                "target",
                targetRefSerialized);
            var summary = FirstNonEmpty(
                GetString(payload, "ExecutionSummary"),
                GetString(payload, "LastWaitReason"),
                SemanticDescriptorUtility.GetConditionSummary(semantics, GetString(payload, "ConditionSummary")),
                GetString(payload, "PlanSummary"));
            projection = new RuntimeSemanticPayloadProjection(
                summary,
                new[]
                {
                    CreateDetail("主体", resolvedSubjectSummary, ObservationStage.SemanticAnalysis),
                    CreateDetail("主体引用", resolvedSubjectSerialized, ObservationStage.SemanticAnalysis),
                    CreateDetail("目标", resolvedTargetSummary, ObservationStage.SemanticAnalysis),
                    CreateDetail("目标引用", resolvedTargetSerialized, ObservationStage.SemanticAnalysis),
                    CreateDetail("触发距离", FormatFloat(GetFloat(payload, "TriggerRange")), ObservationStage.RuntimeState),
                    CreateDetail("当前等待原因", GetString(payload, "LastWaitReason"), ObservationStage.RuntimeState),
                },
                new[]
                {
                    "SubjectSummary",
                    "SubjectRefSerialized",
                    "TargetSummary",
                    "TargetRefSerialized",
                    "TriggerRange",
                    "LastWaitReason",
                });
            return true;
        }

        private static IReadOnlyList<string> SplitSummaryTokens(string? summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return Array.Empty<string>();
            }

            var parts = summary.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(parts.Length);
            for (var index = 0; index < parts.Length; index++)
            {
                var normalized = parts[index].Trim();
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
        }

        private static string FirstNonEmpty(params string?[] candidates)
        {
            for (var index = 0; index < candidates.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(candidates[index]))
                {
                    return candidates[index]!.Trim();
                }
            }

            return string.Empty;
        }

        private static bool HasProperty(object source, string propertyName)
        {
            return source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public) != null;
        }

        private static string GetString(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return string.Empty;
            }

            return property.GetValue(source) as string ?? string.Empty;
        }

        private static bool GetBool(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return false;
            }

            return property.GetValue(source) switch
            {
                bool boolValue => boolValue,
                string text when bool.TryParse(text, out var parsed) => parsed,
                _ => false,
            };
        }

        private static float GetFloat(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return 0f;
            }

            return property.GetValue(source) switch
            {
                float floatValue => floatValue,
                double doubleValue => (float)doubleValue,
                decimal decimalValue => (float)decimalValue,
                int intValue => intValue,
                long longValue => longValue,
                string text when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0f,
            };
        }

        private static int GetInt(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return 0;
            }

            return property.GetValue(source) switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                short shortValue => shortValue,
                byte byteValue => byteValue,
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0,
            };
        }

        private static SubjectSemanticDescriptor[] GetSubjects(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return Array.Empty<SubjectSemanticDescriptor>();
            }

            return property.GetValue(source) switch
            {
                SubjectSemanticDescriptor[] array => array,
                IReadOnlyList<SubjectSemanticDescriptor> readOnlyList => CopySubjects(readOnlyList),
                _ => Array.Empty<SubjectSemanticDescriptor>(),
            };
        }

        private static SpawnWaveSupplyProjection BuildSpawnWaveSupplyProjection(object payload)
        {
            var entries = GetObjects(payload, "Entries");
            if (entries.Length == 0)
            {
                return SpawnWaveSupplyProjection.Empty;
            }

            var resolvedCount = 0;
            var identityLines = new List<string>(Math.Min(entries.Length, 4));
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                var entryId = GetString(entry, "EntryId");
                var monsterType = GetInt(entry, "MonsterType");
                var monsterId = GetString(entry, "MonsterId");
                if (!string.IsNullOrWhiteSpace(monsterId))
                {
                    resolvedCount++;
                }

                if (identityLines.Count < 4)
                {
                    var entryLabel = string.IsNullOrWhiteSpace(entryId)
                        ? $"(未指定 EntryId #{index + 1})"
                        : entryId.Trim();
                    var resolvedLabel = string.IsNullOrWhiteSpace(monsterId) ? "<unresolved>" : monsterId.Trim();
                    identityLines.Add($"{entryLabel} -> monsterType={monsterType}, monsterId={resolvedLabel}");
                }
            }

            if (entries.Length > identityLines.Count)
            {
                identityLines.Add($"... 另有 {entries.Length - identityLines.Count} 个供给条目");
            }

            var remainingTotal = GetInt(payload, "RemainingTotal");
            var totalInitialCount = GetInt(payload, "TotalInitialCount");
            return new SpawnWaveSupplyProjection(
                entries.Length,
                resolvedCount,
                string.Join("\n", identityLines),
                $"{resolvedCount}/{entries.Length}",
                FormatInventorySummary(remainingTotal, totalInitialCount));
        }

        private static object[] GetObjects(object source, string propertyName)
        {
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return Array.Empty<object>();
            }

            return property.GetValue(source) switch
            {
                Array array => CopyObjects(array),
                System.Collections.IEnumerable enumerable when property.PropertyType != typeof(string) => CopyObjects(enumerable),
                _ => Array.Empty<object>(),
            };
        }

        private static SubjectSemanticDescriptor[] CopySubjects(IReadOnlyList<SubjectSemanticDescriptor> subjects)
        {
            if (subjects == null || subjects.Count == 0)
            {
                return Array.Empty<SubjectSemanticDescriptor>();
            }

            var copy = new SubjectSemanticDescriptor[subjects.Count];
            for (var index = 0; index < subjects.Count; index++)
            {
                copy[index] = subjects[index];
            }

            return copy;
        }

        private static object[] CopyObjects(Array source)
        {
            if (source.Length == 0)
            {
                return Array.Empty<object>();
            }

            var copy = new object[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                copy[index] = source.GetValue(index) ?? new object();
            }

            return copy;
        }

        private static object[] CopyObjects(System.Collections.IEnumerable source)
        {
            var items = new List<object>();
            foreach (var item in source)
            {
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items.Count == 0 ? Array.Empty<object>() : items.ToArray();
        }

        private static string FormatBool(bool value)
        {
            return value ? "是" : "否";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatInt(int value)
        {
            return value > 0 ? value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string FormatInventorySummary(int remainingTotal, int totalInitialCount)
        {
            if (remainingTotal <= 0 && totalInitialCount <= 0)
            {
                return string.Empty;
            }

            return $"{remainingTotal}/{totalInitialCount}";
        }

        private static RuntimeSnapshotBrowserDetailRowViewModel CreateDetail(
            string label,
            string value,
            ObservationStage stage)
        {
            return new RuntimeSnapshotBrowserDetailRowViewModel(
                label,
                value,
                ObservationStageUtility.GetTitle(stage));
        }

        private readonly struct SpawnWaveSupplyProjection
        {
            public static SpawnWaveSupplyProjection Empty => default;

            public SpawnWaveSupplyProjection(
                int entryCount,
                int resolvedCount,
                string identitySummary,
                string resolvedSummary,
                string inventorySummary)
            {
                EntryCount = entryCount;
                ResolvedCount = resolvedCount;
                IdentitySummary = identitySummary ?? string.Empty;
                ResolvedSummary = resolvedSummary ?? string.Empty;
                InventorySummary = inventorySummary ?? string.Empty;
            }

            public int EntryCount { get; }

            public int ResolvedCount { get; }

            public string IdentitySummary { get; }

            public string ResolvedSummary { get; }

            public string InventorySummary { get; }
        }
    }
}
