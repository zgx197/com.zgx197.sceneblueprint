#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class EventHistoryProjectionModel
    {
        public EventHistoryProjectionModel(
            string category,
            string summary,
            string searchText,
            long sequence = 0L,
            int tick = 0,
            string? slotKey = null,
            string? locatorText = null,
            IReadOnlyList<EventHistoryProjectionField>? detailFields = null)
        {
            Category = category ?? string.Empty;
            Summary = summary ?? string.Empty;
            SearchText = searchText ?? string.Empty;
            Sequence = Math.Max(0L, sequence);
            Tick = Math.Max(0, tick);
            SlotKey = slotKey ?? string.Empty;
            LocatorText = locatorText ?? string.Empty;
            DetailFields = detailFields ?? Array.Empty<EventHistoryProjectionField>();
        }

        public string Category { get; }

        public string Summary { get; }

        public string SearchText { get; }

        public long Sequence { get; }

        public int Tick { get; }

        public string SlotKey { get; }

        public string LocatorText { get; }

        public IReadOnlyList<EventHistoryProjectionField> DetailFields { get; }
    }

    public readonly struct EventHistoryProjectionField
    {
        public EventHistoryProjectionField(string label, string value, string? sectionTitle = null)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            SectionTitle = sectionTitle ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }

        public string SectionTitle { get; }
    }

    public static class EventHistoryProjectionUtility
    {
        public const string EventHistorySectionTitle = "事件历史";
        public const string SemanticAnalysisSectionTitle = "语义解析";

        public static EventHistoryProjectionModel Build(BlueprintEventHistoryState? state)
        {
            if (state == null)
            {
                return new EventHistoryProjectionModel("事件历史", "事件历史", string.Empty);
            }

            var summary = BuildSummary(state);
            var details = BuildDetailFields(state);
            var slotKey = BuildSlotKey(state);
            var locatorText = BuildLocatorText(state);
            return new EventHistoryProjectionModel(
                category: BuildCategory(state.EventKind, state.RecordKind),
                summary: summary,
                searchText: BuildSearchText(state),
                sequence: state.Sequence,
                tick: state.Tick,
                slotKey: slotKey,
                locatorText: locatorText,
                detailFields: details);
        }

        public static string BuildCategory(string? eventKind, EventHistoryRecordKind recordKind)
        {
            var normalizedEventKind = eventKind?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedEventKind))
            {
                var dotIndex = normalizedEventKind.IndexOf('.');
                return dotIndex > 0
                    ? normalizedEventKind.Substring(0, dotIndex)
                    : normalizedEventKind;
            }

            return recordKind == EventHistoryRecordKind.Unknown
                ? "事件历史"
                : recordKind.ToString();
        }

        public static string BuildSummary(BlueprintEventHistoryState? state)
        {
            if (state == null)
            {
                return "事件历史";
            }

            var projection = ResolveProjection(state);
            var eventSummary = projection.Summary;

            if (state.RecordKind == EventHistoryRecordKind.Unknown)
            {
                return string.IsNullOrWhiteSpace(eventSummary) ? "事件历史" : eventSummary;
            }

            return string.IsNullOrWhiteSpace(eventSummary)
                ? state.RecordKind.ToString()
                : $"{state.RecordKind} | {eventSummary}";
        }

        public static string BuildEventContextSummary(BlueprintEventContext? eventContext)
        {
            return ResolveProjection(eventContext).Summary;
        }

        public static string BuildSearchText(BlueprintEventHistoryState? state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            var projection = ResolveProjection(state);
            var parts = new List<string>(16)
            {
                BuildCategory(state.EventKind, state.RecordKind),
            };

            if (state.RecordKind != EventHistoryRecordKind.Unknown)
            {
                parts.Add(state.RecordKind.ToString());
            }

            AppendIfNotEmpty(parts, state.EventKind);
            AppendIfNotEmpty(parts, state.ActionId);
            if (state.ActionIndex >= 0)
            {
                parts.Add(state.ActionIndex.ToString());
            }

            if (state.Tick > 0)
            {
                parts.Add(state.Tick.ToString());
            }

            AppendIfNotEmpty(parts, BuildSlotKey(state));
            AppendIfNotEmpty(parts, BuildLocatorText(state));
            AppendIfNotEmpty(parts, projection.SignalTag);
            AppendParticipantSearch(parts, projection.Subject);
            AppendParticipantSearch(parts, projection.Instigator);
            AppendParticipantSearch(parts, projection.Target);
            AppendIfNotEmpty(parts, projection.PayloadSummary);
            AppendPayloadSearch(parts, state.EventContext);
            return string.Join(" | ", parts);
        }

        public static IReadOnlyList<EventHistoryProjectionField> BuildEventContextDetailFields(BlueprintEventContext? eventContext)
        {
            var normalizedEventContext = BlueprintEventContextSemanticUtility.Normalize(eventContext) ?? eventContext ?? new BlueprintEventContext();
            var projection = ResolveProjection(normalizedEventContext);
            var fields = new List<EventHistoryProjectionField>(24);
            AddField(fields, "事件类型", projection.EventKind, EventHistorySectionTitle);
            AddField(fields, "来源节点", normalizedEventContext.ActionId, EventHistorySectionTitle);
            AddField(fields, "来源索引", normalizedEventContext.ActionIndex >= 0 ? normalizedEventContext.ActionIndex.ToString() : string.Empty, EventHistorySectionTitle);
            AddField(fields, "Tick", normalizedEventContext.Tick > 0 ? normalizedEventContext.Tick.ToString() : string.Empty, EventHistorySectionTitle);
            AddField(fields, "信号标签", projection.SignalTag, EventHistorySectionTitle);
            AddParticipantFields(fields, "主体", projection.Subject);
            AddParticipantFields(fields, "发起者", projection.Instigator);
            AddParticipantFields(fields, "目标", projection.Target);
            AddField(fields, "载荷", projection.PayloadSummary, EventHistorySectionTitle);
            AddPayloadFields(fields, normalizedEventContext);
            return fields;
        }

        public static IReadOnlyList<EventHistoryProjectionField> BuildDetailFields(BlueprintEventHistoryState? state)
        {
            if (state == null)
            {
                return Array.Empty<EventHistoryProjectionField>();
            }

            var projection = ResolveProjection(state);
            var fields = new List<EventHistoryProjectionField>(28);
            AddField(fields, "类别", BuildCategory(state.EventKind, state.RecordKind), EventHistorySectionTitle);
            AddField(fields, "记录序号", state.Sequence > 0 ? state.Sequence.ToString() : string.Empty, EventHistorySectionTitle);
            AddField(fields, "事件槽位", BuildSlotKey(state), EventHistorySectionTitle);
            AddField(fields, "定位", BuildLocatorText(state), EventHistorySectionTitle);
            AddField(fields, "记录类型", state.RecordKind.ToString(), EventHistorySectionTitle);
            fields.AddRange(BuildEventContextDetailFields(state.EventContext));
            return fields;
        }

        public static string BuildSlotKey(BlueprintEventHistoryState? state)
        {
            return state != null && state.Sequence > 0
                ? EventHistoryStateDomain.CreateSlotKey(state.Sequence)
                : string.Empty;
        }

        public static string BuildLocatorText(BlueprintEventHistoryState? state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            var slotKey = BuildSlotKey(state);
            if (string.IsNullOrWhiteSpace(slotKey))
            {
                return string.Empty;
            }

            var parts = new List<string>(4)
            {
                $"{EventHistoryStateDomain.EventHistoryDomainId}|{EventHistoryStateDomain.EventHistoryOwnerRef.Kind}|{EventHistoryStateDomain.EventHistoryOwnerRef.LogicalKey}|{slotKey}",
            };

            if (state.Tick > 0)
            {
                parts.Add($"T={state.Tick}");
            }

            if (!string.IsNullOrWhiteSpace(state.ActionId))
            {
                parts.Add(state.ActionId);
            }

            if (state.ActionIndex >= 0)
            {
                parts.Add($"idx={state.ActionIndex}");
            }

            return string.Join(" | ", parts);
        }

        private static BlueprintEventContextSemanticProjection ResolveProjection(BlueprintEventHistoryState state)
        {
            return ResolveProjection(
                state.EventContext,
                state.EventKind,
                state.SignalTag,
                state.SubjectRefSerialized,
                state.SubjectSummary,
                state.InstigatorRefSerialized,
                state.InstigatorSummary,
                state.TargetRefSerialized,
                state.TargetSummary,
                state.PayloadSummary);
        }

        private static BlueprintEventContextSemanticProjection ResolveProjection(
            BlueprintEventContext? eventContext,
            string? fallbackEventKind = null,
            string? fallbackSignalTag = null,
            string? fallbackSubjectRefSerialized = null,
            string? fallbackSubjectSummary = null,
            string? fallbackInstigatorRefSerialized = null,
            string? fallbackInstigatorSummary = null,
            string? fallbackTargetRefSerialized = null,
            string? fallbackTargetSummary = null,
            string? fallbackPayloadSummary = null)
        {
            if (HasEventContextData(eventContext))
            {
                return BlueprintEventContextSemanticUtility.BuildProjection(eventContext);
            }

            return BlueprintEventContextSemanticUtility.BuildProjection(
                fallbackEventKind,
                fallbackSignalTag,
                fallbackSubjectRefSerialized,
                fallbackSubjectSummary,
                fallbackInstigatorRefSerialized,
                fallbackInstigatorSummary,
                fallbackTargetRefSerialized,
                fallbackTargetSummary,
                fallbackPayloadSummary);
        }

        private static void AddParticipantFields(
            List<EventHistoryProjectionField> fields,
            string labelPrefix,
            BlueprintEventContextParticipantProjection projection)
        {
            AddField(fields, labelPrefix, projection.Summary, SemanticAnalysisSectionTitle);
            AddField(fields, $"{labelPrefix}引用", projection.Reference, SemanticAnalysisSectionTitle);
            AddField(
                fields,
                $"{labelPrefix}标识",
                ObservationIdentityUtility.BuildParticipantIdentityValue(
                    projection.Summary,
                    projection.IdentitySummary,
                    projection.PublicSubjectId,
                    projection.CompiledSubjectId,
                    projection.RuntimeEntityId),
                SemanticAnalysisSectionTitle);
        }

        private static void AppendParticipantSearch(
            List<string> parts,
            BlueprintEventContextParticipantProjection projection)
        {
            AppendIfNotEmpty(parts, projection.Summary);
            AppendIfNotEmpty(parts, projection.Reference);
            AppendIfNotEmpty(parts, projection.IdentitySummary);
            AppendIfNotEmpty(parts, projection.PublicSubjectId);
            AppendIfNotEmpty(parts, projection.CompiledSubjectId);
            AppendIfNotEmpty(parts, projection.RuntimeEntityId);
        }

        private static void AppendPayloadSearch(List<string> parts, BlueprintEventContext? eventContext)
        {
            var payload = eventContext?.Payload;
            if (payload == null || payload.IsEmpty)
            {
                return;
            }

            var keys = new List<string>(payload.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                if (!payload.TryGetValue(key, out var value))
                {
                    continue;
                }

                AppendIfNotEmpty(parts, key);
                AppendIfNotEmpty(parts, value);
            }
        }

        private static void AddPayloadFields(List<EventHistoryProjectionField> fields, BlueprintEventContext? eventContext)
        {
            var payload = eventContext?.Payload;
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

                if (visibleCount >= 6)
                {
                    continue;
                }

                AddField(fields, $"载荷.{key}", value, EventHistorySectionTitle);
                visibleCount++;
            }

            if (keys.Count > visibleCount)
            {
                AddField(fields, "更多载荷", $"已折叠 {keys.Count - visibleCount} 项", EventHistorySectionTitle);
            }
        }

        private static void AppendIfNotEmpty(List<string> parts, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value.Trim());
            }
        }

        private static bool HasEventContextData(BlueprintEventContext? eventContext)
        {
            return eventContext != null
                && (!string.IsNullOrWhiteSpace(eventContext.EventKind)
                    || !string.IsNullOrWhiteSpace(eventContext.SignalTag)
                    || !string.IsNullOrWhiteSpace(eventContext.SubjectRefSerialized)
                    || !string.IsNullOrWhiteSpace(eventContext.SubjectSummary)
                    || !string.IsNullOrWhiteSpace(eventContext.InstigatorRefSerialized)
                    || !string.IsNullOrWhiteSpace(eventContext.InstigatorSummary)
                    || !string.IsNullOrWhiteSpace(eventContext.TargetRefSerialized)
                    || !string.IsNullOrWhiteSpace(eventContext.TargetSummary)
                    || !string.IsNullOrWhiteSpace(eventContext.PayloadSummary)
                    || (eventContext.Payload?.Count ?? 0) > 0);
        }

        private static void AddField(List<EventHistoryProjectionField> fields, string label, string? value, string? sectionTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields.Add(new EventHistoryProjectionField(label, value.Trim(), sectionTitle));
            }
        }
    }
}
