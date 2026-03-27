#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    [Serializable]
    public sealed class EventHistoryQuery
    {
        public EventHistoryRecordKind? RecordKind { get; set; }

        public string Category { get; set; } = string.Empty;

        public string ActionId { get; set; } = string.Empty;

        public int? ActionIndex { get; set; }

        public string EventKind { get; set; } = string.Empty;

        public string SignalTag { get; set; } = string.Empty;

        public string SubjectText { get; set; } = string.Empty;

        public string InstigatorText { get; set; } = string.Empty;

        public string TargetText { get; set; } = string.Empty;

        public string IdentityText { get; set; } = string.Empty;

        public string PayloadText { get; set; } = string.Empty;

        public string FreeText { get; set; } = string.Empty;

        public long? MinimumSequence { get; set; }

        public long? MaximumSequence { get; set; }

        public int? MinimumTick { get; set; }

        public int? MaximumTick { get; set; }

        public bool NewestFirst { get; set; } = true;

        public int Limit { get; set; }

        public bool HasFilters =>
            RecordKind.HasValue
            || !string.IsNullOrWhiteSpace(Category)
            || !string.IsNullOrWhiteSpace(ActionId)
            || ActionIndex.HasValue
            || !string.IsNullOrWhiteSpace(EventKind)
            || !string.IsNullOrWhiteSpace(SignalTag)
            || !string.IsNullOrWhiteSpace(SubjectText)
            || !string.IsNullOrWhiteSpace(InstigatorText)
            || !string.IsNullOrWhiteSpace(TargetText)
            || !string.IsNullOrWhiteSpace(IdentityText)
            || !string.IsNullOrWhiteSpace(PayloadText)
            || !string.IsNullOrWhiteSpace(FreeText)
            || MinimumSequence.HasValue
            || MaximumSequence.HasValue
            || MinimumTick.HasValue
            || MaximumTick.HasValue;
    }

    public static class EventHistoryQueryUtility
    {
        public static bool Matches(BlueprintEventHistoryState? state, EventHistoryQuery? query)
        {
            if (state == null)
            {
                return false;
            }

            var projection = ResolveProjection(state);

            if (query == null || !query.HasFilters)
            {
                return true;
            }

            if (query.RecordKind.HasValue && state.RecordKind != query.RecordKind.Value)
            {
                return false;
            }

            if (!ContainsIgnoreCase(
                    EventHistoryProjectionUtility.BuildCategory(state.EventKind, state.RecordKind),
                    query.Category))
            {
                return false;
            }

            if (!ContainsIgnoreCase(state.ActionId, query.ActionId))
            {
                return false;
            }

            if (query.ActionIndex.HasValue && state.ActionIndex != query.ActionIndex.Value)
            {
                return false;
            }

            if (!ContainsIgnoreCase(projection.EventKind, query.EventKind))
            {
                return false;
            }

            if (!ContainsIgnoreCase(projection.SignalTag, query.SignalTag))
            {
                return false;
            }

            if (!MatchesParticipantText(projection.Subject, query.SubjectText)
                && !MatchesParticipantText(projection.Instigator, query.SubjectText))
            {
                return false;
            }

            if (!MatchesParticipantText(projection.Instigator, query.InstigatorText))
            {
                return false;
            }

            if (!MatchesParticipantText(projection.Target, query.TargetText))
            {
                return false;
            }

            if (!ContainsIgnoreCase(projection.PayloadSummary, query.PayloadText)
                && !ContainsIgnoreCase(BuildPayloadSearchText(state.EventContext), query.PayloadText))
            {
                return false;
            }

            if (!MatchesIdentityText(projection, query.IdentityText))
            {
                return false;
            }

            if (query.MinimumSequence.HasValue && state.Sequence < query.MinimumSequence.Value)
            {
                return false;
            }

            if (query.MaximumSequence.HasValue && state.Sequence > query.MaximumSequence.Value)
            {
                return false;
            }

            if (query.MinimumTick.HasValue && state.Tick < query.MinimumTick.Value)
            {
                return false;
            }

            if (query.MaximumTick.HasValue && state.Tick > query.MaximumTick.Value)
            {
                return false;
            }

            return ContainsIgnoreCase(BuildSearchText(state), query.FreeText);
        }

        public static bool Matches(EventHistoryStatePayload? payload, EventHistoryQuery? query)
        {
            if (payload == null)
            {
                return false;
            }

            return Matches(payload.ToState(), query);
        }

        public static string BuildSummary(BlueprintEventHistoryState? state)
        {
            return EventHistoryProjectionUtility.BuildSummary(state);
        }

        public static string BuildSummary(EventHistoryStatePayload? payload)
        {
            return payload == null
                ? "事件历史"
                : BuildSummary(payload.ToState());
        }

        public static string BuildSearchText(BlueprintEventHistoryState? state)
        {
            return EventHistoryProjectionUtility.BuildSearchText(state);
        }

        private static bool ContainsIgnoreCase(string? source, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(source)
                && source.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesIdentityText(BlueprintEventContextSemanticProjection projection, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return ContainsIgnoreCase(BuildParticipantSearchText(projection.Subject), filter)
                   || ContainsIgnoreCase(BuildParticipantSearchText(projection.Instigator), filter)
                   || ContainsIgnoreCase(BuildParticipantSearchText(projection.Target), filter);
        }

        private static string BuildPayloadSearchText(BlueprintEventContext? eventContext)
        {
            if (eventContext?.Payload == null || eventContext.Payload.Count == 0)
            {
                return string.Empty;
            }

            var parts = new System.Collections.Generic.List<string>(eventContext.Payload.Count * 2);
            var keys = new System.Collections.Generic.List<string>(eventContext.Payload.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                parts.Add(key);
                if (eventContext.Payload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value);
                }
            }

            return string.Join(" | ", parts);
        }

        private static BlueprintEventContextSemanticProjection ResolveProjection(BlueprintEventHistoryState state)
        {
            if (state.EventContext != null
                && (!string.IsNullOrWhiteSpace(state.EventContext.EventKind)
                    || !string.IsNullOrWhiteSpace(state.EventContext.SignalTag)
                    || !string.IsNullOrWhiteSpace(state.EventContext.SubjectRefSerialized)
                    || !string.IsNullOrWhiteSpace(state.EventContext.SubjectSummary)
                    || !string.IsNullOrWhiteSpace(state.EventContext.InstigatorRefSerialized)
                    || !string.IsNullOrWhiteSpace(state.EventContext.InstigatorSummary)
                    || !string.IsNullOrWhiteSpace(state.EventContext.TargetRefSerialized)
                    || !string.IsNullOrWhiteSpace(state.EventContext.TargetSummary)
                    || !string.IsNullOrWhiteSpace(state.EventContext.PayloadSummary)
                    || (state.EventContext.Payload?.Count ?? 0) > 0))
            {
                return BlueprintEventContextSemanticUtility.BuildProjection(state.EventContext);
            }

            return BlueprintEventContextSemanticUtility.BuildProjection(
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

        private static bool MatchesParticipantText(BlueprintEventContextParticipantProjection participant, string? filter)
        {
            return ContainsIgnoreCase(participant.Summary, filter)
                   || ContainsIgnoreCase(participant.Reference, filter)
                   || ContainsIgnoreCase(BuildParticipantSearchText(participant), filter);
        }

        private static string BuildParticipantSearchText(BlueprintEventContextParticipantProjection participant)
        {
            return string.Join(
                " | ",
                participant.Summary,
                participant.Reference,
                participant.IdentitySummary,
                participant.PublicSubjectId,
                participant.CompiledSubjectId,
                participant.RuntimeEntityId);
        }
    }
}
