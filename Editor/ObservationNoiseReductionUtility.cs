#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor.Compilation;

namespace SceneBlueprint.Editor
{
    internal static class ObservationNoiseReductionUtility
    {
        public const int DefaultPayloadKeyLimit = 6;
        public const int DefaultDebugProjectionFieldLimit = 8;

        public static string NormalizeObservationText(string? value)
        {
            return ObservationIdentityUtility.NormalizeObservationText(value);
        }

        public static bool AreEquivalentObservationValues(string? left, string? right)
        {
            return ObservationIdentityUtility.AreEquivalentObservationValues(left, right);
        }

        public static string BuildParticipantIdentityValue(
            string? summary,
            string? identitySummary,
            string? publicSubjectId,
            string? compiledSubjectId,
            string? runtimeEntityId)
        {
            return ObservationIdentityUtility.BuildParticipantIdentityValue(
                summary,
                identitySummary,
                publicSubjectId,
                compiledSubjectId,
                runtimeEntityId);
        }

        public static bool IsLowSignalRuntimePayloadProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return true;
            }

            return propertyName.EndsWith("EntryRef", StringComparison.Ordinal)
                || propertyName.EndsWith("EntryRefs", StringComparison.Ordinal)
                || propertyName.EndsWith("Serialized", StringComparison.Ordinal)
                || propertyName.EndsWith("Summary", StringComparison.Ordinal)
                || propertyName.EndsWith("Subjects", StringComparison.Ordinal)
                || propertyName.EndsWith("Descriptors", StringComparison.Ordinal)
                || propertyName.EndsWith("Handle", StringComparison.Ordinal)
                || propertyName.StartsWith("Reactive", StringComparison.Ordinal)
                || propertyName.StartsWith("Scheduling", StringComparison.Ordinal)
                || string.Equals(propertyName, "ExporterId", StringComparison.Ordinal)
                || string.Equals(propertyName, "PayloadTypeId", StringComparison.Ordinal);
        }

        public static bool IsHighSignalRuntimePayloadProperty(string propertyName)
        {
            switch (propertyName)
            {
                case "StartTick":
                case "TargetTick":
                case "TimeoutTargetTick":
                case "TimeoutSeconds":
                case "LastEvaluationTick":
                case "EvaluationCount":
                case "RequestedSpawnCount":
                case "PublicSubjectCount":
                case "LastSpawnCount":
                case "LastErrorMessage":
                case "CurrentWaveId":
                case "CurrentWaveIndex":
                case "CurrentWaveDelayTicks":
                case "CurrentWaveRequestedSpawnCount":
                case "CurrentWaveSubjectSlotCount":
                case "CurrentWavePublicSubjectCount":
                case "NextWaveId":
                case "NextWaveRequestedSpawnCount":
                case "NextWaveSubjectSlotCount":
                case "NextWavePublicSubjectCount":
                case "TotalInitialCount":
                case "RemainingTotal":
                case "LastSpawnTick":
                case "Entries":
                case "RequireFullyInside":
                case "HasTriggerAreaBinding":
                case "TreatAsAlwaysSatisfied":
                case "TriggerRange":
                case "LastWaitReason":
                case "RequiredCount":
                case "ReceivedCount":
                case "ConnectedMask":
                case "TriggeredMask":
                case "ConditionMet":
                case "RoutedPort":
                case "IsTerminal":
                case "Tick":
                case "Duration":
                case "DurationSeconds":
                case "Intensity":
                case "Frequency":
                case "FlashColor":
                case "Text":
                case "Style":
                case "FontSize":
                case "ValueText":
                case "CompareValueText":
                case "ConstValueText":
                case "AccessKind":
                case "VariableSummary":
                case "VariableName":
                case "Scope":
                case "VariableType":
                case "Succeeded":
                case "FailureReason":
                    return true;
                default:
                    return false;
            }
        }

        public static IReadOnlyList<DebugProjectionSection> BuildVisibleDebugProjectionSections(
            DebugProjectionModel? model,
            int maxFieldsPerSection = DefaultDebugProjectionFieldLimit)
        {
            if (model == null || model.Sections.Count == 0)
            {
                return Array.Empty<DebugProjectionSection>();
            }

            var sections = new List<DebugProjectionSection>(model.Sections.Count);
            for (var sectionIndex = 0; sectionIndex < model.Sections.Count; sectionIndex++)
            {
                var section = model.Sections[sectionIndex];
                var candidates = new List<DebugProjectionCandidate>(section.Fields.Count);
                for (var fieldIndex = 0; fieldIndex < section.Fields.Count; fieldIndex++)
                {
                    var field = section.Fields[fieldIndex];
                    if (string.IsNullOrWhiteSpace(field.Label) || string.IsNullOrWhiteSpace(field.Value))
                    {
                        continue;
                    }

                    candidates.Add(new DebugProjectionCandidate(field, fieldIndex, GetDebugProjectionFieldPriority(field.Label)));
                }

                if (candidates.Count == 0)
                {
                    continue;
                }

                candidates.Sort(static (left, right) =>
                {
                    var priorityComparison = left.Priority.CompareTo(right.Priority);
                    return priorityComparison != 0
                        ? priorityComparison
                        : left.Index.CompareTo(right.Index);
                });

                var visible = new List<DebugProjectionCandidate>(Math.Min(candidates.Count, maxFieldsPerSection));
                var emittedLabels = new HashSet<string>(StringComparer.Ordinal);
                for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    var candidate = candidates[candidateIndex];
                    if (!emittedLabels.Add(candidate.Field.Label))
                    {
                        continue;
                    }

                    if (visible.Count >= maxFieldsPerSection)
                    {
                        continue;
                    }

                    visible.Add(candidate);
                }

                visible.Sort(static (left, right) => left.Index.CompareTo(right.Index));
                var fields = new List<DebugProjectionField>(visible.Count + 1);
                for (var visibleIndex = 0; visibleIndex < visible.Count; visibleIndex++)
                {
                    fields.Add(visible[visibleIndex].Field);
                }

                if (visible.Count < candidates.Count)
                {
                    fields.Add(new DebugProjectionField(
                        "更多字段",
                        $"已折叠 {candidates.Count - visible.Count} 项"));
                }

                sections.Add(new DebugProjectionSection(section.Title, fields, section.Stage));
            }

            return sections;
        }

        private static int GetDebugProjectionFieldPriority(string label)
        {
            switch (label)
            {
                case "主体":
                case "目标":
                case "条件":
                case "值":
                case "图结构":
                case "事件上下文":
                case "Semantic Payload":
                case "Plan Payload":
                case "Debug Payload":
                case "诊断数":
                case "总条目":
                case "Compiled 条目":
                case "Auxiliary 条目":
                case "Family":
                    return 0;
            }

            if (label.EndsWith("标识", StringComparison.Ordinal))
            {
                return 1;
            }

            if (label.EndsWith("公共ID", StringComparison.Ordinal)
                || label.EndsWith("编译ID", StringComparison.Ordinal)
                || label.EndsWith("实体ID", StringComparison.Ordinal))
            {
                return 2;
            }

            return 1;
        }

        private readonly struct DebugProjectionCandidate
        {
            public DebugProjectionCandidate(DebugProjectionField field, int index, int priority)
            {
                Field = field;
                Index = index;
                Priority = priority;
            }

            public DebugProjectionField Field { get; }

            public int Index { get; }

            public int Priority { get; }
        }
    }
}
