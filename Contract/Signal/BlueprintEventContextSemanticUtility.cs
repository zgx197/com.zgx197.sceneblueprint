#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    public sealed class BlueprintEventContextParticipantProjection
    {
        public BlueprintEventContextParticipantProjection(
            string summary,
            string reference,
            string identitySummary,
            string publicSubjectId,
            string compiledSubjectId,
            string runtimeEntityId)
        {
            Summary = summary ?? string.Empty;
            Reference = reference ?? string.Empty;
            IdentitySummary = identitySummary ?? string.Empty;
            PublicSubjectId = publicSubjectId ?? string.Empty;
            CompiledSubjectId = compiledSubjectId ?? string.Empty;
            RuntimeEntityId = runtimeEntityId ?? string.Empty;
        }

        public string Summary { get; }

        public string Reference { get; }

        public string IdentitySummary { get; }

        public string PublicSubjectId { get; }

        public string CompiledSubjectId { get; }

        public string RuntimeEntityId { get; }
    }

    public sealed class BlueprintEventContextSemanticProjection
    {
        public BlueprintEventContextSemanticProjection(
            SemanticDescriptorSet semantics,
            string summary,
            string eventKind,
            string signalTag,
            string payloadSummary,
            BlueprintEventContextParticipantProjection subject,
            BlueprintEventContextParticipantProjection instigator,
            BlueprintEventContextParticipantProjection target)
        {
            Semantics = semantics ?? new SemanticDescriptorSet();
            Summary = summary ?? string.Empty;
            EventKind = eventKind ?? string.Empty;
            SignalTag = signalTag ?? string.Empty;
            PayloadSummary = payloadSummary ?? string.Empty;
            Subject = subject ?? new BlueprintEventContextParticipantProjection(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            Instigator = instigator ?? new BlueprintEventContextParticipantProjection(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            Target = target ?? new BlueprintEventContextParticipantProjection(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }

        public SemanticDescriptorSet Semantics { get; }

        public string Summary { get; }

        public string EventKind { get; }

        public string SignalTag { get; }

        public string PayloadSummary { get; }

        public BlueprintEventContextParticipantProjection Subject { get; }

        public BlueprintEventContextParticipantProjection Instigator { get; }

        public BlueprintEventContextParticipantProjection Target { get; }
    }

    /// <summary>
    /// BlueprintEventContext 最小语义工具。
    /// 统一收口 Emit 事件上下文里的主体引用规范化与摘要投影，避免 runtime system 继续现场拼 event context。
    /// </summary>
    public static class BlueprintEventContextSemanticUtility
    {
        public static SemanticDescriptorSet BuildEventContextSemantics(
            SemanticDescriptorSet? semantics,
            string? fallbackEventKind,
            string? fallbackSignalTag,
            SignalPayload? payload,
            string? fallbackSubjectRefSerialized = null,
            string? fallbackSubjectSummary = null,
            string? fallbackInstigatorRefSerialized = null,
            string? fallbackInstigatorSummary = null,
            string? fallbackTargetRefSerialized = null,
            string? fallbackTargetSummary = null)
        {
            var payloadSummary = BuildPayloadSummary(payload);
            var eventKind = SemanticDescriptorUtility.GetEventKind(semantics, fallbackEventKind);
            var signalTag = SemanticDescriptorUtility.GetEventSignalTag(semantics, fallbackSignalTag);
            var subjectRefSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "subject",
                fallbackSubjectRefSerialized ?? string.Empty);
            var subjectSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "subject",
                fallbackSubjectSummary ?? string.Empty);
            var instigatorRefSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "instigator",
                fallbackInstigatorRefSerialized ?? string.Empty);
            var instigatorSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "instigator",
                fallbackInstigatorSummary ?? string.Empty);
            var targetRefSerialized = SemanticDescriptorUtility.GetTargetReference(
                semantics,
                "target",
                fallbackTargetRefSerialized ?? string.Empty);
            var targetSummary = SemanticDescriptorUtility.GetTargetSummary(
                semantics,
                "target",
                fallbackTargetSummary ?? string.Empty);

            return new SemanticDescriptorSet
            {
                Subjects = new[]
                {
                    SemanticDescriptorUtility.BuildSubjectDescriptor(
                        "subject",
                        subjectRefSerialized,
                        subjectSummary),
                    SemanticDescriptorUtility.BuildSubjectDescriptor(
                        "instigator",
                        instigatorRefSerialized,
                        instigatorSummary),
                },
                Targets = new[]
                {
                    SemanticDescriptorUtility.BuildTargetDescriptor(
                        "target",
                        targetRefSerialized,
                        targetSummary),
                },
                Conditions = semantics?.Conditions ?? Array.Empty<ConditionSemanticDescriptor>(),
                EventContexts = new[]
                {
                    SemanticDescriptorUtility.BuildEventContextDescriptor(
                        eventKind,
                        signalTag,
                        subjectSummary,
                        payloadSummary,
                        instigatorSummary,
                        targetSummary),
                },
                Graphs = semantics?.Graphs ?? Array.Empty<GraphSemanticDescriptor>(),
                Values = semantics?.Values ?? Array.Empty<ValueSemanticDescriptor>(),
            };
        }

        public static SemanticDescriptorSet BuildSemantics(BlueprintEventContext? source)
        {
            var normalized = Normalize(source);
            if (normalized == null)
            {
                return new SemanticDescriptorSet();
            }

            return BuildSemantics(
                normalized.EventKind,
                normalized.SignalTag,
                normalized.SubjectRefSerialized,
                normalized.SubjectSummary,
                normalized.InstigatorRefSerialized,
                normalized.InstigatorSummary,
                normalized.TargetRefSerialized,
                normalized.TargetSummary,
                normalized.PayloadSummary);
        }

        public static SemanticDescriptorSet BuildSemantics(
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
            var normalizedSubject = ResolveContextRef(subjectRefSerialized, null, subjectSummary);
            var normalizedInstigator = ResolveContextRef(instigatorRefSerialized, null, instigatorSummary);
            var normalizedTarget = ResolveContextRef(targetRefSerialized, null, targetSummary);
            var normalized = new BlueprintEventContext
            {
                EventKind = eventKind?.Trim() ?? string.Empty,
                SignalTag = signalTag?.Trim() ?? string.Empty,
                SubjectRefSerialized = normalizedSubject.Serialized,
                SubjectSummary = normalizedSubject.Summary,
                InstigatorRefSerialized = normalizedInstigator.Serialized,
                InstigatorSummary = normalizedInstigator.Summary,
                TargetRefSerialized = normalizedTarget.Serialized,
                TargetSummary = normalizedTarget.Summary,
                PayloadSummary = payloadSummary?.Trim() ?? string.Empty,
            };

            return SemanticDescriptorUtility.BuildEventContextDescriptorSet(normalized);
        }

        public static string BuildSummary(BlueprintEventContext? source)
        {
            return SemanticDescriptorUtility.GetEventSummary(
                BuildSemantics(source),
                nameof(BlueprintEventContext));
        }

        public static BlueprintEventContextSemanticProjection BuildProjection(BlueprintEventContext? source)
        {
            var normalized = Normalize(source);
            if (normalized == null)
            {
                return BuildProjection(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    SignalPayload.Empty);
            }

            return BuildProjection(
                normalized.EventKind,
                normalized.SignalTag,
                normalized.SubjectRefSerialized,
                normalized.SubjectSummary,
                normalized.InstigatorRefSerialized,
                normalized.InstigatorSummary,
                normalized.TargetRefSerialized,
                normalized.TargetSummary,
                normalized.PayloadSummary,
                normalized.Payload);
        }

        public static BlueprintEventContextSemanticProjection BuildProjection(
            string? eventKind,
            string? signalTag,
            string? subjectRefSerialized,
            string? subjectSummary,
            string? instigatorRefSerialized,
            string? instigatorSummary,
            string? targetRefSerialized,
            string? targetSummary,
            string? payloadSummary,
            SignalPayload? payload = null)
        {
            var normalizedPayloadSummary = string.IsNullOrWhiteSpace(payloadSummary)
                ? BuildPayloadSummary(payload)
                : payloadSummary.Trim();
            var semantics = BuildSemantics(
                eventKind,
                signalTag,
                subjectRefSerialized,
                subjectSummary,
                instigatorRefSerialized,
                instigatorSummary,
                targetRefSerialized,
                targetSummary,
                normalizedPayloadSummary);
            return BuildProjection(semantics, normalizedPayloadSummary);
        }

        public static string BuildSummary(
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
                BuildSemantics(
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

        public static BlueprintEventContext? Normalize(BlueprintEventContext? source)
        {
            if (source == null)
            {
                return null;
            }

            var normalizedPayload = source.Payload ?? SignalPayload.Empty;
            var semantics = BuildEventContextSemantics(
                semantics: null,
                fallbackEventKind: source.EventKind,
                fallbackSignalTag: source.SignalTag,
                payload: normalizedPayload,
                fallbackSubjectRefSerialized: source.SubjectRefSerialized,
                fallbackSubjectSummary: source.SubjectSummary,
                fallbackInstigatorRefSerialized: source.InstigatorRefSerialized,
                fallbackInstigatorSummary: source.InstigatorSummary,
                fallbackTargetRefSerialized: source.TargetRefSerialized,
                fallbackTargetSummary: source.TargetSummary);

            return CreateContextFromSemantics(
                actionId: source.ActionId,
                actionIndex: source.ActionIndex,
                tick: source.Tick,
                payload: normalizedPayload,
                semantics: semantics,
                fallbackEventKind: source.EventKind);
        }

        public static BlueprintEventContext CreateEmitContext(
            string? eventKind,
            string? actionId,
            int actionIndex,
            int tick,
            SignalTag signalTag,
            SignalPayload? payload,
            CompiledEntityRefInfo? compiledSubject,
            string? fallbackSubjectSerialized,
            CompiledEntityRefInfo? compiledInstigator,
            string? fallbackInstigatorSerialized,
            CompiledEntityRefInfo? compiledTarget,
            string? fallbackTargetSerialized)
        {
            var subject = CompiledEntityRefSemanticUtility.Resolve(compiledSubject, fallbackSubjectSerialized);
            var instigator = CompiledEntityRefSemanticUtility.Resolve(compiledInstigator, fallbackInstigatorSerialized);
            var target = CompiledEntityRefSemanticUtility.Resolve(compiledTarget, fallbackTargetSerialized);
            return CreateActionEventContext(
                eventKind,
                actionId,
                actionIndex,
                tick,
                payload,
                semantics: null,
                fallbackSignalTag: signalTag.Path,
                fallbackSubjectRefSerialized: subject.Serialized,
                fallbackSubjectSummary: subject.Summary,
                fallbackInstigatorRefSerialized: instigator.Serialized,
                fallbackInstigatorSummary: instigator.Summary,
                fallbackTargetRefSerialized: target.Serialized,
                fallbackTargetSummary: target.Summary);
        }

        public static BlueprintEventContext CreateInjectedContext(
            SignalTag signalTag,
            SignalPayload? payload,
            BlueprintEventContext? source,
            int tick)
        {
            var normalizedSource = Normalize(source);
            var effectivePayload = normalizedSource?.Payload ?? (payload ?? SignalPayload.Empty);
            return CreateActionEventContext(
                string.IsNullOrWhiteSpace(normalizedSource?.EventKind) ? "Signal.Inject" : normalizedSource!.EventKind,
                normalizedSource?.ActionId ?? string.Empty,
                normalizedSource?.ActionIndex ?? -1,
                tick,
                effectivePayload,
                normalizedSource != null ? BuildSemantics(normalizedSource) : null,
                fallbackSignalTag: string.IsNullOrWhiteSpace(normalizedSource?.SignalTag)
                    ? signalTag.Path ?? string.Empty
                    : normalizedSource!.SignalTag,
                fallbackSubjectRefSerialized: normalizedSource?.SubjectRefSerialized,
                fallbackSubjectSummary: normalizedSource?.SubjectSummary,
                fallbackInstigatorRefSerialized: normalizedSource?.InstigatorRefSerialized,
                fallbackInstigatorSummary: normalizedSource?.InstigatorSummary,
                fallbackTargetRefSerialized: normalizedSource?.TargetRefSerialized,
                fallbackTargetSummary: normalizedSource?.TargetSummary);
        }

        public static BlueprintEventContext CreateActionEventContext(
            string? eventKind,
            string? actionId,
            int actionIndex,
            int tick,
            SignalPayload? payload,
            SemanticDescriptorSet? semantics,
            string? fallbackSignalTag = null,
            string? fallbackSubjectRefSerialized = null,
            string? fallbackSubjectSummary = null,
            string? fallbackInstigatorRefSerialized = null,
            string? fallbackInstigatorSummary = null,
            string? fallbackTargetRefSerialized = null,
            string? fallbackTargetSummary = null)
        {
            var normalizedPayload = payload ?? SignalPayload.Empty;
            var eventSemantics = BuildEventContextSemantics(
                semantics,
                eventKind,
                fallbackSignalTag,
                normalizedPayload,
                fallbackSubjectRefSerialized,
                fallbackSubjectSummary,
                fallbackInstigatorRefSerialized,
                fallbackInstigatorSummary,
                fallbackTargetRefSerialized,
                fallbackTargetSummary);
            return CreateContextFromSemantics(
                actionId,
                actionIndex,
                tick,
                normalizedPayload,
                eventSemantics,
                eventKind);
        }

        public static BlueprintEventContext CreateActionEventContext(
            string? eventKind,
            string? actionId,
            int actionIndex,
            int tick,
            string? signalTag = null,
            SignalPayload? payload = null,
            string? subjectRefSerialized = null,
            string? subjectSummary = null,
            string? instigatorRefSerialized = null,
            string? instigatorSummary = null,
            string? targetRefSerialized = null,
            string? targetSummary = null)
        {
            return CreateActionEventContext(
                eventKind,
                actionId,
                actionIndex,
                tick,
                payload,
                semantics: null,
                fallbackSignalTag: signalTag,
                fallbackSubjectRefSerialized: subjectRefSerialized,
                fallbackSubjectSummary: subjectSummary,
                fallbackInstigatorRefSerialized: instigatorRefSerialized,
                fallbackInstigatorSummary: instigatorSummary,
                fallbackTargetRefSerialized: targetRefSerialized,
                fallbackTargetSummary: targetSummary);
        }

        public static string BuildPayloadSummary(SignalPayload? payload)
        {
            return (payload ?? SignalPayload.Empty).ToString();
        }

        private static BlueprintEventContextSemanticProjection BuildProjection(
            SemanticDescriptorSet semantics,
            string payloadSummary)
        {
            var subject = BuildSubjectProjection(semantics, "subject");
            var instigator = BuildSubjectProjection(semantics, "instigator");
            var target = BuildTargetProjection(semantics, "target");
            return new BlueprintEventContextSemanticProjection(
                semantics,
                SemanticDescriptorUtility.GetEventSummary(semantics, nameof(BlueprintEventContext)),
                SemanticDescriptorUtility.GetEventKind(semantics),
                SemanticDescriptorUtility.GetEventSignalTag(semantics),
                payloadSummary ?? string.Empty,
                subject,
                instigator,
                target);
        }

        private static BlueprintEventContext CreateContextFromSemantics(
            string? actionId,
            int actionIndex,
            int tick,
            SignalPayload? payload,
            SemanticDescriptorSet? semantics,
            string? fallbackEventKind)
        {
            var normalizedPayload = payload ?? SignalPayload.Empty;
            return new BlueprintEventContext
            {
                EventKind = SemanticDescriptorUtility.GetEventKind(semantics, fallbackEventKind),
                ActionId = actionId?.Trim() ?? string.Empty,
                ActionIndex = actionIndex,
                Tick = tick,
                SignalTag = SemanticDescriptorUtility.GetEventSignalTag(semantics),
                SubjectRefSerialized = SemanticDescriptorUtility.GetSubjectReference(semantics, "subject"),
                InstigatorRefSerialized = SemanticDescriptorUtility.GetSubjectReference(semantics, "instigator"),
                TargetRefSerialized = SemanticDescriptorUtility.GetTargetReference(semantics, "target"),
                SubjectRef = EntityRefCodec.Parse(SemanticDescriptorUtility.GetSubjectReference(semantics, "subject")),
                InstigatorRef = EntityRefCodec.Parse(SemanticDescriptorUtility.GetSubjectReference(semantics, "instigator")),
                TargetRef = EntityRefCodec.Parse(SemanticDescriptorUtility.GetTargetReference(semantics, "target")),
                SubjectSummary = SemanticDescriptorUtility.GetSubjectSummary(semantics, "subject"),
                InstigatorSummary = SemanticDescriptorUtility.GetSubjectSummary(semantics, "instigator"),
                TargetSummary = SemanticDescriptorUtility.GetTargetSummary(semantics, "target"),
                Payload = normalizedPayload,
                PayloadSummary = BuildPayloadSummary(normalizedPayload),
            };
        }

        private static BlueprintEventContextParticipantProjection BuildSubjectProjection(
            SemanticDescriptorSet semantics,
            string slot)
        {
            return new BlueprintEventContextParticipantProjection(
                SemanticDescriptorUtility.GetSubjectSummary(semantics, slot),
                SemanticDescriptorUtility.GetSubjectReference(semantics, slot),
                SemanticDescriptorUtility.GetSubjectIdentitySummary(semantics, slot),
                SemanticDescriptorUtility.GetSubjectPublicSubjectId(semantics, slot),
                SemanticDescriptorUtility.GetSubjectCompiledSubjectId(semantics, slot),
                SemanticDescriptorUtility.GetSubjectRuntimeEntityId(semantics, slot));
        }

        private static BlueprintEventContextParticipantProjection BuildTargetProjection(
            SemanticDescriptorSet semantics,
            string slot)
        {
            return new BlueprintEventContextParticipantProjection(
                SemanticDescriptorUtility.GetTargetSummary(semantics, slot),
                SemanticDescriptorUtility.GetTargetReference(semantics, slot),
                SemanticDescriptorUtility.GetTargetIdentitySummary(semantics, slot),
                SemanticDescriptorUtility.GetTargetPublicSubjectId(semantics, slot),
                SemanticDescriptorUtility.GetTargetCompiledSubjectId(semantics, slot),
                SemanticDescriptorUtility.GetTargetRuntimeEntityId(semantics, slot));
        }

        private static ResolvedEntityRefSemantic ResolveContextRef(
            string? serialized,
            EntityRef? entityRef,
            string? explicitSummary)
        {
            var normalizedSerialized = CompiledEntityRefSemanticUtility.NormalizeSerialized(serialized);
            if (string.IsNullOrEmpty(normalizedSerialized) && HasMeaningfulEntityRef(entityRef))
            {
                normalizedSerialized = EntityRefCodec.Serialize(entityRef!);
            }

            var normalizedEntityRef = string.IsNullOrEmpty(normalizedSerialized)
                ? (entityRef ?? new EntityRef())
                : EntityRefCodec.Parse(normalizedSerialized);
            var summary = string.IsNullOrWhiteSpace(explicitSummary)
                ? SemanticSummaryUtility.DescribeEntityRef(normalizedEntityRef)
                : explicitSummary.Trim();

            return new ResolvedEntityRefSemantic
            {
                Serialized = normalizedSerialized,
                Summary = summary,
                EntityRef = normalizedEntityRef,
            };
        }

        private static bool HasMeaningfulEntityRef(EntityRef? entityRef)
        {
            if (entityRef == null)
            {
                return false;
            }

            return entityRef.Mode switch
            {
                EntityRefMode.ByAlias => !string.IsNullOrWhiteSpace(entityRef.Alias),
                EntityRefMode.BySceneRef => !string.IsNullOrWhiteSpace(entityRef.SceneObjectId),
                EntityRefMode.ByRole => !string.IsNullOrWhiteSpace(entityRef.Role),
                EntityRefMode.ByTag => !string.IsNullOrWhiteSpace(entityRef.TagFilter),
                EntityRefMode.ByTags => entityRef.TagFilters != null && entityRef.TagFilters.Length > 0,
                EntityRefMode.All => true,
                EntityRefMode.Any => true,
                _ => false,
            };
        }
    }
}
