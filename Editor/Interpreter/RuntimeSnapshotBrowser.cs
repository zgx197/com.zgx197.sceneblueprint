#nullable enable
using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Reflection;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Editor.Interpreter
{
    internal sealed class RuntimeSnapshotBrowserPanelModel
    {
        public RuntimeSnapshotBrowserPanelModel(
            string snapshotFilterText,
            string entryFilterText,
            string selectedSnapshotId,
            string selectedEntryKey,
            IReadOnlyList<RuntimeSnapshotBrowserSnapshotViewModel> visibleSnapshots,
            RuntimeSnapshotBrowserSnapshotViewModel? selectedSnapshot,
            IReadOnlyList<RuntimeSnapshotBrowserEntryViewModel> visibleEntries,
            RuntimeSnapshotBrowserEntryViewModel? selectedEntry)
        {
            SnapshotFilterText = snapshotFilterText ?? string.Empty;
            EntryFilterText = entryFilterText ?? string.Empty;
            SelectedSnapshotId = selectedSnapshotId ?? string.Empty;
            SelectedEntryKey = selectedEntryKey ?? string.Empty;
            VisibleSnapshots = visibleSnapshots ?? Array.Empty<RuntimeSnapshotBrowserSnapshotViewModel>();
            SelectedSnapshot = selectedSnapshot;
            VisibleEntries = visibleEntries ?? Array.Empty<RuntimeSnapshotBrowserEntryViewModel>();
            SelectedEntry = selectedEntry;
        }

        public string SnapshotFilterText { get; }

        public string EntryFilterText { get; }

        public string SelectedSnapshotId { get; }

        public string SelectedEntryKey { get; }

        public IReadOnlyList<RuntimeSnapshotBrowserSnapshotViewModel> VisibleSnapshots { get; }

        public RuntimeSnapshotBrowserSnapshotViewModel? SelectedSnapshot { get; }

        public IReadOnlyList<RuntimeSnapshotBrowserEntryViewModel> VisibleEntries { get; }

        public RuntimeSnapshotBrowserEntryViewModel? SelectedEntry { get; }
    }

    internal sealed class RuntimeSnapshotBrowserSnapshotViewModel
    {
        public RuntimeSnapshotBrowserSnapshotViewModel(RuntimeSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Title = string.IsNullOrWhiteSpace(snapshot.Tag)
                ? snapshot.SnapshotId
                : $"{snapshot.Tag} | {snapshot.SnapshotId}";
            SummaryText = $"{snapshot.Entries.Count} 条目 | {snapshot.TargetKind}";
        }

        public RuntimeSnapshot Snapshot { get; }

        public string SnapshotId => Snapshot.SnapshotId;

        public string Title { get; }

        public string SummaryText { get; }
    }

    internal sealed class RuntimeSnapshotBrowserEntryViewModel
    {
        public RuntimeSnapshotBrowserEntryViewModel(
            SnapshotEntry entry,
            string payloadSummary,
            IReadOnlyList<RuntimeSnapshotBrowserDetailRowViewModel>? payloadDetails = null)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            PayloadSummary = payloadSummary ?? string.Empty;
            PayloadDetails = payloadDetails ?? Array.Empty<RuntimeSnapshotBrowserDetailRowViewModel>();
            Title = entry.LogicalEntryKey;
            Subtitle = $"{entry.DomainId} | {entry.ExportMode}/{entry.RestoreMode}";
            SummaryText = $"{entry.PayloadKind} | {PayloadSummary}";
        }

        public SnapshotEntry Entry { get; }

        public string Title { get; }

        public string Subtitle { get; }

        public string SummaryText { get; }

        public string PayloadSummary { get; }

        public IReadOnlyList<RuntimeSnapshotBrowserDetailRowViewModel> PayloadDetails { get; }
    }

    internal readonly struct RuntimeSnapshotBrowserDetailRowViewModel
    {
        public RuntimeSnapshotBrowserDetailRowViewModel(string label, string value, string? sectionTitle = null)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            SectionTitle = sectionTitle ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }

        public string SectionTitle { get; }
    }

    internal static class RuntimeSnapshotBrowserBuilder
    {
        public static RuntimeSnapshotBrowserPanelModel Build(
            IReadOnlyList<RuntimeSnapshot> snapshots,
            string? snapshotFilterText,
            string? selectedSnapshotId,
            string? entryFilterText,
            string? selectedEntryKey)
        {
            var normalizedSnapshotFilter = (snapshotFilterText ?? string.Empty).Trim();
            var normalizedEntryFilter = (entryFilterText ?? string.Empty).Trim();
            var visibleSnapshots = new List<RuntimeSnapshotBrowserSnapshotViewModel>(snapshots?.Count ?? 0);

            if (snapshots != null)
            {
                for (var index = snapshots.Count - 1; index >= 0; index--)
                {
                    var viewModel = new RuntimeSnapshotBrowserSnapshotViewModel(snapshots[index]);
                    if (MatchesSnapshotFilter(viewModel, normalizedSnapshotFilter))
                    {
                        visibleSnapshots.Add(viewModel);
                    }
                }
            }

            var normalizedSelectedSnapshotId = selectedSnapshotId ?? string.Empty;
            RuntimeSnapshotBrowserSnapshotViewModel? selectedSnapshot = null;
            if (visibleSnapshots.Count > 0)
            {
                for (var index = 0; index < visibleSnapshots.Count; index++)
                {
                    if (!string.Equals(visibleSnapshots[index].SnapshotId, normalizedSelectedSnapshotId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    selectedSnapshot = visibleSnapshots[index];
                    break;
                }

                if (selectedSnapshot == null)
                {
                    selectedSnapshot = visibleSnapshots[0];
                    normalizedSelectedSnapshotId = selectedSnapshot.SnapshotId;
                }
            }
            else
            {
                normalizedSelectedSnapshotId = string.Empty;
            }

            var visibleEntries = new List<RuntimeSnapshotBrowserEntryViewModel>();
            if (selectedSnapshot != null)
            {
                for (var index = 0; index < selectedSnapshot.Snapshot.Entries.Count; index++)
                {
                    var entryViewModel = new RuntimeSnapshotBrowserEntryViewModel(
                        selectedSnapshot.Snapshot.Entries[index],
                        CreatePayloadSummary(selectedSnapshot.Snapshot.Entries[index]),
                        CreatePayloadDetails(selectedSnapshot.Snapshot.Entries[index]));
                    if (MatchesEntryFilter(entryViewModel, normalizedEntryFilter))
                    {
                        visibleEntries.Add(entryViewModel);
                    }
                }
            }

            var normalizedSelectedEntryKey = selectedEntryKey ?? string.Empty;
            RuntimeSnapshotBrowserEntryViewModel? selectedEntry = null;
            if (visibleEntries.Count > 0)
            {
                for (var index = 0; index < visibleEntries.Count; index++)
                {
                    if (!string.Equals(visibleEntries[index].Entry.LogicalEntryKey, normalizedSelectedEntryKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    selectedEntry = visibleEntries[index];
                    break;
                }

                if (selectedEntry == null)
                {
                    selectedEntry = visibleEntries[0];
                    normalizedSelectedEntryKey = selectedEntry.Entry.LogicalEntryKey;
                }
            }
            else
            {
                normalizedSelectedEntryKey = string.Empty;
            }

            return new RuntimeSnapshotBrowserPanelModel(
                normalizedSnapshotFilter,
                normalizedEntryFilter,
                normalizedSelectedSnapshotId,
                normalizedSelectedEntryKey,
                visibleSnapshots,
                selectedSnapshot,
                visibleEntries,
                selectedEntry);
        }

        private static bool MatchesSnapshotFilter(RuntimeSnapshotBrowserSnapshotViewModel snapshot, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                return true;
            }

            return ContainsIgnoreCase(snapshot.Title, filterText)
                || ContainsIgnoreCase(snapshot.SummaryText, filterText);
        }

        private static bool MatchesEntryFilter(RuntimeSnapshotBrowserEntryViewModel entry, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                return true;
            }

            return ContainsIgnoreCase(entry.Title, filterText)
                || ContainsIgnoreCase(entry.Subtitle, filterText)
                || ContainsIgnoreCase(entry.SummaryText, filterText)
                || MatchesDetailFilter(entry.PayloadDetails, filterText);
        }

        private static bool ContainsIgnoreCase(string? source, string filterText)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CreatePayloadSummary(SnapshotEntry entry)
        {
            if (entry.Payload == null)
            {
                return "无载荷";
            }

            return entry.Payload switch
            {
                ObservationFieldNode node => $"摘要字段: {node.FieldName}",
                RuntimeStateSnapshotPayload statePayload when statePayload.Payload is EventHistoryStatePayload historyPayload => EventHistoryQueryUtility.BuildSummary(historyPayload),
                RuntimeStateSnapshotPayload statePayload => CreateRuntimeStatePayloadSummary(statePayload),
                BlueprintEventContext eventContext => CreateEventContextSummary(eventContext),
                _ => entry.Payload.ToString() ?? entry.Payload.GetType().Name
            };
        }

        private static IReadOnlyList<RuntimeSnapshotBrowserDetailRowViewModel> CreatePayloadDetails(SnapshotEntry entry)
        {
            if (entry.Payload is RuntimeStateSnapshotPayload statePayload
                && statePayload.Payload is EventHistoryStatePayload historyPayload)
            {
                var historyState = historyPayload.ToState();
                var projection = EventHistoryProjectionUtility.Build(historyState);
                var historyDetails = new List<RuntimeSnapshotBrowserDetailRowViewModel>(projection.DetailFields.Count + 1);
                for (var index = 0; index < projection.DetailFields.Count; index++)
                {
                    if (string.IsNullOrWhiteSpace(projection.DetailFields[index].Value))
                    {
                        continue;
                    }

                    historyDetails.Add(CreateDetailRow(
                        projection.DetailFields[index].Label,
                        projection.DetailFields[index].Value,
                        projection.DetailFields[index].SectionTitle));
                }

                historyDetails.Add(CreateDetailRow("Schema", $"{statePayload.SchemaId}@{statePayload.SchemaVersion}", ObservationStage.Metadata));
                return historyDetails;
            }

            if (entry.Payload is RuntimeStateSnapshotPayload runtimeStatePayload)
            {
                return CreateRuntimeStatePayloadDetails(runtimeStatePayload);
            }

            if (entry.Payload is not BlueprintEventContext eventContext)
            {
                return Array.Empty<RuntimeSnapshotBrowserDetailRowViewModel>();
            }

            var eventContextDetails = new List<RuntimeSnapshotBrowserDetailRowViewModel>(24);
            var projectionFields = EventHistoryProjectionUtility.BuildEventContextDetailFields(eventContext);
            for (var index = 0; index < projectionFields.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(projectionFields[index].Value))
                {
                    continue;
                }

                eventContextDetails.Add(CreateDetailRow(
                    projectionFields[index].Label,
                    projectionFields[index].Value,
                    projectionFields[index].SectionTitle));
            }

            return eventContextDetails;
        }

        private static string CreateEventContextSummary(BlueprintEventContext eventContext)
        {
            return EventHistoryProjectionUtility.BuildEventContextSummary(eventContext);
        }

        private static string CreateRuntimeStatePayloadSummary(RuntimeStateSnapshotPayload payload)
        {
            if (payload.Payload == null)
            {
                return payload.PayloadTypeId;
            }

            if (RuntimeSemanticPresentationUtility.TryBuildRuntimeStatePayloadProjection(payload.Payload, out var semanticProjection)
                && !string.IsNullOrWhiteSpace(semanticProjection.Summary))
            {
                return semanticProjection.Summary;
            }

            var executionSummary = TryGetMemberValueSummary(payload.Payload, "ExecutionSummary");
            if (!string.IsNullOrWhiteSpace(executionSummary))
            {
                return executionSummary;
            }

            var planSummary = TryGetMemberValueSummary(payload.Payload, "PlanSummary");
            if (!string.IsNullOrWhiteSpace(planSummary))
            {
                return planSummary;
            }

            var conditionSummary = TryGetMemberValueSummary(payload.Payload, "ConditionSummary");
            if (!string.IsNullOrWhiteSpace(conditionSummary))
            {
                return conditionSummary;
            }

            return payload.PayloadTypeId;
        }

        private static IReadOnlyList<RuntimeSnapshotBrowserDetailRowViewModel> CreateRuntimeStatePayloadDetails(
            RuntimeStateSnapshotPayload payload)
        {
            if (payload.Payload == null)
            {
                return new[]
                {
                    CreateDetailRow("载荷类型", payload.PayloadTypeId, ObservationStage.Metadata),
                    CreateDetailRow("Schema", $"{payload.SchemaId}@{payload.SchemaVersion}", ObservationStage.Metadata),
                };
            }

            var details = new List<RuntimeSnapshotBrowserDetailRowViewModel>();
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            details.Add(CreateDetailRow("载荷类型", payload.PayloadTypeId, ObservationStage.Metadata));
            AddPreferredDetail(details, emitted, payload.Payload, "PlanSource", "计划来源", ObservationStage.PlanCompilation);
            AddPreferredDetail(details, emitted, payload.Payload, "PlanSummary", "计划摘要", ObservationStage.PlanCompilation);
            AddPreferredDetail(details, emitted, payload.Payload, "ConditionSummary", "语义摘要", ObservationStage.SemanticAnalysis);
            AddPreferredDetail(details, emitted, payload.Payload, "ExecutionSummary", "执行摘要", ObservationStage.RuntimeState);
            var hasFormalProjection = details.Count > 1;

            if (RuntimeSemanticPresentationUtility.TryBuildRuntimeStatePayloadProjection(payload.Payload, out var semanticProjection))
            {
                hasFormalProjection = true;
                for (var index = 0; index < semanticProjection.Details.Count; index++)
                {
                    if (string.IsNullOrWhiteSpace(semanticProjection.Details[index].Value))
                    {
                        continue;
                    }

                    details.Add(CreateDetailRow(
                        semanticProjection.Details[index].Label,
                        semanticProjection.Details[index].Value,
                        string.IsNullOrWhiteSpace(semanticProjection.Details[index].SectionTitle)
                            ? ObservationStageUtility.GetTitle(
                                ObservationStageUtility.InferFromLabel(semanticProjection.Details[index].Label))
                            : semanticProjection.Details[index].SectionTitle));
                }

                for (var index = 0; index < semanticProjection.ConsumedMemberNames.Count; index++)
                {
                    emitted.Add(semanticProjection.ConsumedMemberNames[index]);
                }
            }

            if (!hasFormalProjection)
            {
                details.Add(CreateDetailRow(
                    "正式投影",
                    "当前 schema 未提供专用观察投影",
                    ObservationStage.Metadata));
            }

            details.Add(CreateDetailRow("Schema", $"{payload.SchemaId}@{payload.SchemaVersion}", ObservationStage.Metadata));
            return details;
        }

        private static void AddPreferredDetail(
            List<RuntimeSnapshotBrowserDetailRowViewModel> details,
            HashSet<string> emitted,
            object payload,
            string memberName,
            string label,
            ObservationStage stage)
        {
            var value = TryGetMemberValueSummary(payload, memberName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            emitted.Add(memberName);
            details.Add(CreateDetailRow(label, value, stage));
        }

        private static string TryGetMemberValueSummary(object payload, string memberName)
        {
            var property = payload.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
            {
                return string.Empty;
            }

            return FormatMemberValue(property.GetValue(payload));
        }

        private static string FormatMemberValue(object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is string text)
            {
                return text;
            }

            if (value is bool boolValue)
            {
                return boolValue ? "True" : "False";
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            if (value is IEnumerable enumerable and not string)
            {
                var count = TryGetCollectionCount(enumerable);
                return count.HasValue ? $"Count={count.Value.ToString(CultureInfo.InvariantCulture)}" : value.GetType().Name;
            }

            return value.ToString() ?? string.Empty;
        }

        private static int? TryGetCollectionCount(IEnumerable enumerable)
        {
            if (enumerable is ICollection collection)
            {
                return collection.Count;
            }

            var count = 0;
            var hasAny = false;
            foreach (var _ in enumerable)
            {
                hasAny = true;
                count++;
                if (count > 256)
                {
                    break;
                }
            }

            return hasAny ? count : 0;
        }

        private static void AddParticipantRows(
            List<RuntimeSnapshotBrowserDetailRowViewModel> rows,
            string labelPrefix,
            BlueprintEventContextParticipantProjection projection,
            ObservationStage stage)
        {
            AddRow(rows, labelPrefix, projection.Summary, stage);
            AddRow(rows, $"{labelPrefix}引用", projection.Reference, stage);
            AddRow(
                rows,
                $"{labelPrefix}标识",
                ObservationNoiseReductionUtility.BuildParticipantIdentityValue(
                    projection.Summary,
                    projection.IdentitySummary,
                    projection.PublicSubjectId,
                    projection.CompiledSubjectId,
                    projection.RuntimeEntityId),
                stage);
        }

        private static void AddPayloadRows(List<RuntimeSnapshotBrowserDetailRowViewModel> rows, SignalPayload? payload, ObservationStage stage)
        {
            if (payload == null || payload.IsEmpty)
            {
                return;
            }

            var keys = new List<string>(payload.Keys);
            keys.Sort(StringComparer.Ordinal);
            var visibleCount = 0;
            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                if (!payload.TryGetValue(key, out var value))
                {
                    continue;
                }

                if (visibleCount >= ObservationNoiseReductionUtility.DefaultPayloadKeyLimit)
                {
                    continue;
                }

                AddRow(rows, $"载荷.{key}", value, stage);
                visibleCount++;
            }

            if (keys.Count > visibleCount)
            {
                AddRow(
                    rows,
                    "更多载荷",
                    $"已折叠 {(keys.Count - visibleCount).ToString(CultureInfo.InvariantCulture)} 项",
                    stage);
            }
        }

        private static void AddRow(
            List<RuntimeSnapshotBrowserDetailRowViewModel> rows,
            string label,
            string? value,
            ObservationStage stage = ObservationStage.None)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                rows.Add(CreateDetailRow(label, value.Trim(), stage));
            }
        }

        private static RuntimeSnapshotBrowserDetailRowViewModel CreateDetailRow(
            string label,
            string value,
            ObservationStage stage)
        {
            return CreateDetailRow(label, value, ObservationStageUtility.GetTitle(stage));
        }

        private static RuntimeSnapshotBrowserDetailRowViewModel CreateDetailRow(
            string label,
            string value,
            string? sectionTitle)
        {
            return new RuntimeSnapshotBrowserDetailRowViewModel(
                label,
                value,
                sectionTitle);
        }

        private static bool MatchesDetailFilter(
            IReadOnlyList<RuntimeSnapshotBrowserDetailRowViewModel> details,
            string filterText)
        {
            for (var index = 0; index < details.Count; index++)
            {
                if (ContainsIgnoreCase(details[index].Label, filterText)
                    || ContainsIgnoreCase(details[index].Value, filterText))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
