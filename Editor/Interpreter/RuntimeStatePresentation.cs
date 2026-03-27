#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor;
using SceneBlueprint.Runtime.Interpreter;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Editor.Interpreter
{
    public sealed class RuntimeStatePresentationResult
    {
        public RuntimeStatePresentationResult(
            IReadOnlyList<RuntimeStatePresentationViewModel> presentations,
            int totalEntryCount)
        {
            Presentations = presentations ?? Array.Empty<RuntimeStatePresentationViewModel>();
            TotalEntryCount = Math.Max(0, totalEntryCount);
        }

        public IReadOnlyList<RuntimeStatePresentationViewModel> Presentations { get; }

        public int TotalEntryCount { get; }

        public int SupportedEntryCount => Presentations.Count;
    }

    public sealed class RuntimeStatePresentationViewModel
    {
        public RuntimeStatePresentationViewModel(
            RuntimeStateSummaryViewModel summary,
            RuntimeStateDetailViewModel detail)
        {
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
        }

        public RuntimeStateSummaryViewModel Summary { get; }

        public RuntimeStateDetailViewModel Detail { get; }
    }

    public sealed class RuntimeStateSummaryViewModel
    {
        public RuntimeStateSummaryViewModel(
            string logicalEntryKey,
            string title,
            string subtitle,
            string summaryText,
            string actionId,
            string actionTypeId,
            int? actionIndex,
            ActionPhase? phase,
            RuntimeEntryRef? entryRef)
        {
            LogicalEntryKey = string.IsNullOrWhiteSpace(logicalEntryKey)
                ? throw new ArgumentException("Logical entry key cannot be null or whitespace.", nameof(logicalEntryKey))
                : logicalEntryKey;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            ActionTypeId = actionTypeId ?? string.Empty;
            ActionIndex = actionIndex;
            Phase = phase;
            EntryRef = entryRef;
        }

        public string LogicalEntryKey { get; }

        public string Title { get; }

        public string Subtitle { get; }

        public string SummaryText { get; }

        public string ActionId { get; }

        public string ActionTypeId { get; }

        public int? ActionIndex { get; }

        public ActionPhase? Phase { get; }

        public RuntimeEntryRef? EntryRef { get; }
    }

    public sealed class RuntimeStateDetailViewModel
    {
        public RuntimeStateDetailViewModel(
            string title,
            string subtitle,
            string summaryText,
            IReadOnlyList<RuntimeStateDetailFieldViewModel> fields)
        {
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            Fields = fields ?? Array.Empty<RuntimeStateDetailFieldViewModel>();
        }

        public string Title { get; }

        public string Subtitle { get; }

        public string SummaryText { get; }

        public IReadOnlyList<RuntimeStateDetailFieldViewModel> Fields { get; }
    }

    public readonly struct RuntimeStateDetailFieldViewModel
    {
        public RuntimeStateDetailFieldViewModel(string label, string value, string? sectionTitle = null)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            SectionTitle = sectionTitle ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }

        public string SectionTitle { get; }
    }

    public sealed class RuntimeStatePresenterRegistry
    {
        private readonly IReadOnlyList<IRuntimeStatePresenter> _presenters;

        public RuntimeStatePresenterRegistry()
            : this(null)
        {
        }

        internal RuntimeStatePresenterRegistry(IReadOnlyList<IRuntimeStatePresenter>? presenters)
        {
            _presenters = presenters ?? new IRuntimeStatePresenter[]
            {
                new EventHistoryRuntimeStatePresenter(),
                new BlueprintEventContextRuntimeStatePresenter(),
                new InstantEventRuntimeStatePresenter(),
                new BlackboardAccessRuntimeStatePresenter(),
                new FlowFilterRuntimeStatePresenter(),
                new FlowBranchRuntimeStatePresenter(),
                new TimedRuntimeStatePresenter(),
                new WaitSignalRuntimeStatePresenter(),
                new WatchConditionRuntimeStatePresenter(),
                new TriggerEnterAreaRuntimeStatePresenter(),
                new InteractionApproachTargetRuntimeStatePresenter(),
                new SpawnPresetRuntimeStatePresenter(),
                new SpawnWaveRuntimeStatePresenter(),
                new CompositeConditionRuntimeStatePresenter(),
                new JoinRuntimeStatePresenter(),
            };
        }

        public static RuntimeStatePresenterRegistry Default { get; } = new();

        public RuntimeStatePresentationResult BuildPresentations(ObservationResult observation, BlueprintFrame? frame)
        {
            if (observation == null)
            {
                throw new ArgumentNullException(nameof(observation));
            }

            var presentations = new List<RuntimeStatePresentationViewModel>();
            for (var index = 0; index < observation.Entries.Count; index++)
            {
                if (!RuntimeStateObservationContext.TryCreate(observation.Entries[index], frame, out var context))
                {
                    continue;
                }

                for (var presenterIndex = 0; presenterIndex < _presenters.Count; presenterIndex++)
                {
                    var presenter = _presenters[presenterIndex];
                    if (!presenter.CanPresent(context))
                    {
                        continue;
                    }

                    presentations.Add(presenter.CreatePresentation(context));
                    break;
                }
            }

            presentations.Sort(static (left, right) =>
            {
                var leftIndex = left.Summary.ActionIndex ?? int.MaxValue;
                var rightIndex = right.Summary.ActionIndex ?? int.MaxValue;
                var indexComparison = leftIndex.CompareTo(rightIndex);
                if (indexComparison != 0)
                {
                    return indexComparison;
                }

                return string.CompareOrdinal(left.Summary.LogicalEntryKey, right.Summary.LogicalEntryKey);
            });

            return new RuntimeStatePresentationResult(presentations, observation.Entries.Count);
        }
    }

    internal interface IRuntimeStatePresenter
    {
        bool CanPresent(RuntimeStateObservationContext context);

        RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context);
    }

    internal sealed class RuntimeStateObservationContext
    {
        private RuntimeStateObservationContext(
            ObservationEntry entry,
            ObservationFieldNode descriptorNode,
            ObservationFieldNode ownerNode,
            ObservationFieldNode stateNode,
            string descriptorId,
            string descriptorDebugName,
            string ownerLogicalKey,
            string stateTypeName,
            string actionId,
            string actionTypeId,
            int? actionIndex,
            ActionPhase? phase,
            int? currentTick)
        {
            Entry = entry;
            DescriptorNode = descriptorNode;
            OwnerNode = ownerNode;
            StateNode = stateNode;
            DescriptorId = descriptorId;
            DescriptorDebugName = descriptorDebugName;
            OwnerLogicalKey = ownerLogicalKey;
            StateTypeName = stateTypeName;
            ActionId = actionId;
            ActionTypeId = actionTypeId;
            ActionIndex = actionIndex;
            Phase = phase;
            CurrentTick = currentTick;
        }

        public ObservationEntry Entry { get; }

        public ObservationFieldNode DescriptorNode { get; }

        public ObservationFieldNode OwnerNode { get; }

        public ObservationFieldNode StateNode { get; }

        public string DescriptorId { get; }

        public string DescriptorDebugName { get; }

        public string OwnerLogicalKey { get; }

        public string StateTypeName { get; }

        public string ActionId { get; }

        public string ActionTypeId { get; }

        public int? ActionIndex { get; }

        public ActionPhase? Phase { get; }

        public int? CurrentTick { get; }

        public string PreferredTitle =>
            !string.IsNullOrWhiteSpace(ActionTypeId)
                ? ActionTypeId
                : !string.IsNullOrWhiteSpace(DescriptorDebugName)
                    ? DescriptorDebugName
                    : Entry.LogicalEntryKey;

        public string CommonSubtitle
        {
            get
            {
                var parts = new List<string>(3);
                if (!string.IsNullOrWhiteSpace(ActionId))
                {
                    parts.Add(ActionId);
                }

                if (ActionIndex.HasValue)
                {
                    parts.Add($"idx={ActionIndex.Value.ToString(CultureInfo.InvariantCulture)}");
                }

                if (Phase.HasValue)
                {
                    parts.Add(Phase.Value.ToString());
                }

                return parts.Count == 0
                    ? DescriptorDebugName
                    : string.Join(" | ", parts);
            }
        }

        public static bool TryCreate(ObservationEntry entry, BlueprintFrame? frame, out RuntimeStateObservationContext context)
        {
            context = null!;
            if (entry == null
                || !ObservationNodeLookup.TryGetChild(entry.RootField, "Descriptor", out var descriptorNode)
                || !ObservationNodeLookup.TryGetChild(entry.RootField, "Owner", out var ownerNode)
                || !ObservationNodeLookup.TryGetChild(entry.RootField, "State", out var stateNode))
            {
                return false;
            }

            var descriptorId = ObservationNodeLookup.GetChildValueSummary(descriptorNode, "Id");
            var descriptorDebugName = ObservationNodeLookup.GetChildValueSummary(descriptorNode, "DebugName");
            var ownerLogicalKey = ObservationNodeLookup.GetChildValueSummary(ownerNode, "LogicalKey");
            var stateTypeName = stateNode.TypeName ?? string.Empty;
            var actionId = ParseActionId(ownerLogicalKey);
            var actionTypeId = string.Empty;
            int? actionIndex = null;
            ActionPhase? phase = null;
            int? currentTick = frame?.TickCount;

            if (frame != null
                && !string.IsNullOrWhiteSpace(actionId)
                && frame.ActionIdToIndex.TryGetValue(actionId, out var resolvedIndex))
            {
                actionIndex = resolvedIndex;
                if (resolvedIndex >= 0 && resolvedIndex < frame.Actions.Length)
                {
                    actionTypeId = frame.Actions[resolvedIndex].TypeId ?? string.Empty;
                }

                if (resolvedIndex >= 0 && resolvedIndex < frame.States.Length)
                {
                    phase = frame.States[resolvedIndex].Phase;
                }
            }

            context = new RuntimeStateObservationContext(
                entry,
                descriptorNode,
                ownerNode,
                stateNode,
                descriptorId,
                descriptorDebugName,
                ownerLogicalKey,
                stateTypeName,
                actionId,
                actionTypeId,
                actionIndex,
                phase,
                currentTick);
            return true;
        }

        public bool HasStateType(string typeName)
        {
            return StateTypeName.EndsWith(typeName, StringComparison.Ordinal);
        }

        public string GetStateValueOrEmpty(string fieldName)
        {
            return ObservationNodeLookup.GetChildValueSummary(StateNode, fieldName);
        }

        public bool TryGetStateChild(string fieldName, out ObservationFieldNode child)
        {
            return ObservationNodeLookup.TryGetChild(StateNode, fieldName, out child);
        }

        public bool TryGetStateBool(string fieldName, out bool value)
        {
            return bool.TryParse(GetStateValueOrEmpty(fieldName), out value);
        }

        public bool TryGetStateInt(string fieldName, out int value)
        {
            return int.TryParse(
                GetStateValueOrEmpty(fieldName),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        public bool TryGetStateFloat(string fieldName, out float value)
        {
            return float.TryParse(
                GetStateValueOrEmpty(fieldName),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        public string GetActionTypeOrFallback()
        {
            return string.IsNullOrWhiteSpace(ActionTypeId)
                ? PreferredTitle
                : ActionTypeId;
        }

        private static string ParseActionId(string ownerLogicalKey)
        {
            const string prefix = "action:";
            return ownerLogicalKey.StartsWith(prefix, StringComparison.Ordinal)
                ? ownerLogicalKey.Substring(prefix.Length)
                : string.Empty;
        }
    }

    internal static class ObservationNodeLookup
    {
        public static bool TryGetChild(ObservationFieldNode node, string fieldName, out ObservationFieldNode child)
        {
            if (node.Children != null)
            {
                for (var index = 0; index < node.Children.Count; index++)
                {
                    var candidate = node.Children[index];
                    if (string.Equals(candidate.FieldName, fieldName, StringComparison.Ordinal))
                    {
                        child = candidate;
                        return true;
                    }
                }
            }

            child = null!;
            return false;
        }

        public static string GetChildValueSummary(ObservationFieldNode node, string fieldName)
        {
            return TryGetChild(node, fieldName, out var child)
                ? child.ValueSummary ?? string.Empty
                : string.Empty;
        }
    }

    internal sealed class InstantEventRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("InstantEventNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var eventKind = context.GetStateValueOrEmpty("EventKind");
            var eventValue = context.GetStateValueOrEmpty("EventValue");
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var projection = BlueprintEventContextSemanticUtility.BuildProjection(
                eventKind,
                null,
                context.GetStateValueOrEmpty("SubjectRefSerialized"),
                context.GetStateValueOrEmpty("SubjectSummary"),
                context.GetStateValueOrEmpty("InstigatorRefSerialized"),
                context.GetStateValueOrEmpty("InstigatorSummary"),
                context.GetStateValueOrEmpty("TargetRefSerialized"),
                context.GetStateValueOrEmpty("TargetSummary"),
                context.GetStateValueOrEmpty("PayloadSummary"));
            context.TryGetStateBool("IsTerminal", out var isTerminal);

            var summaryText = !string.IsNullOrWhiteSpace(executionSummary)
                ? RuntimeStatePresentationFactory.Shorten(executionSummary, 72)
                : string.IsNullOrWhiteSpace(eventValue)
                ? eventKind
                : $"{eventKind} = {RuntimeStatePresentationFactory.Shorten(eventValue)}";
            if (isTerminal)
            {
                summaryText = string.IsNullOrWhiteSpace(summaryText)
                    ? "终止事件"
                    : $"{summaryText} | 终止";
            }

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                subtitle: context.CommonSubtitle,
                summaryText: summaryText,
                specificRows: new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("事件类型", eventKind),
                    RuntimeStatePresentationFactory.CreateRow("事件值", eventValue),
                    RuntimeStatePresentationFactory.CreateRow("主体", projection.Subject.Summary),
                    RuntimeStatePresentationFactory.CreateRow("发起者", projection.Instigator.Summary),
                    RuntimeStatePresentationFactory.CreateRow("目标", projection.Target.Summary),
                    RuntimeStatePresentationFactory.CreateRow("载荷", projection.PayloadSummary),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                    RuntimeStatePresentationFactory.CreateRow("终止事件", RuntimeStatePresentationFactory.ToYesNo(isTerminal)),
                });
        }
    }

    internal sealed class BlueprintEventContextRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("BlueprintEventContext");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var projection = BlueprintEventContextSemanticUtility.BuildProjection(
                context.GetStateValueOrEmpty("EventKind"),
                context.GetStateValueOrEmpty("SignalTag"),
                context.GetStateValueOrEmpty("SubjectRefSerialized"),
                context.GetStateValueOrEmpty("SubjectSummary"),
                context.GetStateValueOrEmpty("InstigatorRefSerialized"),
                context.GetStateValueOrEmpty("InstigatorSummary"),
                context.GetStateValueOrEmpty("TargetRefSerialized"),
                context.GetStateValueOrEmpty("TargetSummary"),
                context.GetStateValueOrEmpty("PayloadSummary"),
                TryBuildStatePayload(context));
            var subtitle = string.IsNullOrWhiteSpace(projection.SignalTag)
                ? context.CommonSubtitle
                : $"{projection.SignalTag} | {context.CommonSubtitle}";
            var summaryText = RuntimeStatePresentationFactory.Shorten(projection.Summary, 96);

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                subtitle,
                summaryText,
                BuildDetailRows(
                    context,
                    projection,
                    TryBuildStatePayload(context)));
        }

        private static IReadOnlyList<RuntimeStateDetailFieldViewModel> BuildDetailRows(
            RuntimeStateObservationContext context,
            BlueprintEventContextSemanticProjection projection,
            SignalPayload payload)
        {
            var rows = new List<RuntimeStateDetailFieldViewModel>(24)
            {
                RuntimeStatePresentationFactory.CreateRow("事件类型", projection.EventKind),
                RuntimeStatePresentationFactory.CreateRow("信号标签", projection.SignalTag),
            };
            AddParticipantRows(rows, "主体", projection.Subject);
            AddParticipantRows(rows, "发起者", projection.Instigator);
            AddParticipantRows(rows, "目标", projection.Target);
            rows.Add(RuntimeStatePresentationFactory.CreateRow("载荷", projection.PayloadSummary));
            AddPayloadRows(rows, payload);
            rows.Add(RuntimeStatePresentationFactory.CreateRow("来源节点", context.GetStateValueOrEmpty("ActionId")));
            rows.Add(RuntimeStatePresentationFactory.CreateRow("节点索引", context.GetStateValueOrEmpty("ActionIndex")));
            rows.Add(RuntimeStatePresentationFactory.CreateRow("Tick", context.GetStateValueOrEmpty("Tick")));
            return rows;
        }

        private static void AddParticipantRows(
            List<RuntimeStateDetailFieldViewModel> rows,
            string labelPrefix,
            BlueprintEventContextParticipantProjection projection)
        {
            AddRow(rows, labelPrefix, projection.Summary);
            AddRow(rows, $"{labelPrefix}引用", projection.Reference);
            AddRow(
                rows,
                $"{labelPrefix}标识",
                ObservationNoiseReductionUtility.BuildParticipantIdentityValue(
                    projection.Summary,
                    projection.IdentitySummary,
                    projection.PublicSubjectId,
                    projection.CompiledSubjectId,
                    projection.RuntimeEntityId));
        }

        private static void AddRow(List<RuntimeStateDetailFieldViewModel> rows, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                rows.Add(RuntimeStatePresentationFactory.CreateRow(label, value));
            }
        }

        private static void AddPayloadRows(List<RuntimeStateDetailFieldViewModel> rows, SignalPayload payload)
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

                AddRow(rows, $"载荷.{key}", value);
                visibleCount++;
            }

            if (keys.Count > visibleCount)
            {
                AddRow(rows, "更多载荷", $"已折叠 {keys.Count - visibleCount} 项");
            }
        }

        private static SignalPayload TryBuildStatePayload(RuntimeStateObservationContext context)
        {
            if (!context.TryGetStateChild("Payload", out var payloadNode)
                || payloadNode.Children == null
                || payloadNode.Children.Count == 0)
            {
                return SignalPayload.Empty;
            }

            var payload = new SignalPayload();
            for (var index = 0; index < payloadNode.Children.Count; index++)
            {
                var child = payloadNode.Children[index];
                if (string.IsNullOrWhiteSpace(child.FieldName)
                    || string.IsNullOrWhiteSpace(child.ValueSummary))
                {
                    continue;
                }

                payload[child.FieldName] = child.ValueSummary;
            }

            return payload;
        }
    }

    internal sealed class EventHistoryRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("BlueprintEventHistoryState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var sequence = context.GetStateValueOrEmpty("Sequence");
            var tick = context.GetStateValueOrEmpty("Tick");
            var sourceActionId = context.GetStateValueOrEmpty("ActionId");
            var projection = EventHistoryProjectionUtility.Build(new BlueprintEventHistoryState
            {
                RecordKind = Enum.TryParse<EventHistoryRecordKind>(context.GetStateValueOrEmpty("RecordKind"), out var parsedRecordKind)
                    ? parsedRecordKind
                    : EventHistoryRecordKind.Unknown,
                EventKind = context.GetStateValueOrEmpty("EventKind"),
                ActionId = sourceActionId,
                ActionIndex = int.TryParse(context.GetStateValueOrEmpty("ActionIndex"), out var actionIndex) ? actionIndex : -1,
                Tick = int.TryParse(tick, out var parsedTick) ? parsedTick : 0,
                SignalTag = context.GetStateValueOrEmpty("SignalTag"),
                SubjectSummary = context.GetStateValueOrEmpty("SubjectSummary"),
                SubjectRefSerialized = context.GetStateValueOrEmpty("SubjectRefSerialized"),
                InstigatorSummary = context.GetStateValueOrEmpty("InstigatorSummary"),
                InstigatorRefSerialized = context.GetStateValueOrEmpty("InstigatorRefSerialized"),
                TargetSummary = context.GetStateValueOrEmpty("TargetSummary"),
                TargetRefSerialized = context.GetStateValueOrEmpty("TargetRefSerialized"),
                PayloadSummary = context.GetStateValueOrEmpty("PayloadSummary"),
                EventContext = TryBuildEventContext(context),
                Sequence = long.TryParse(sequence, out var parsedSequence) ? parsedSequence : 0L,
            });

            var subtitleParts = new List<string>(4);
            if (!string.IsNullOrWhiteSpace(sequence))
            {
                subtitleParts.Add($"#{sequence}");
            }

            if (!string.IsNullOrWhiteSpace(projection.Category))
            {
                subtitleParts.Add(projection.Category);
            }

            if (!string.IsNullOrWhiteSpace(tick))
            {
                subtitleParts.Add($"T={tick}");
            }

            if (!string.IsNullOrWhiteSpace(sourceActionId))
            {
                subtitleParts.Add(sourceActionId);
            }

            var subtitle = subtitleParts.Count == 0
                ? "事件历史"
                : string.Join(" | ", subtitleParts);
            var rows = new List<RuntimeStateDetailFieldViewModel>(projection.DetailFields.Count);
            for (var index = 0; index < projection.DetailFields.Count; index++)
            {
                var field = projection.DetailFields[index];
                if (string.IsNullOrWhiteSpace(field.Value))
                {
                    continue;
                }

                rows.Add(RuntimeStatePresentationFactory.CreateRow(field.Label, field.Value, field.SectionTitle));
            }

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                subtitle,
                projection.Summary,
                rows);
        }

        private static BlueprintEventContext TryBuildEventContext(RuntimeStateObservationContext context)
        {
            if (!context.TryGetStateChild("EventContext", out var eventContextNode))
            {
                return new BlueprintEventContext();
            }

            var payload = new SignalPayload();
            if (ObservationNodeLookup.TryGetChild(eventContextNode, "Payload", out var payloadNode)
                && payloadNode.Children != null)
            {
                for (var index = 0; index < payloadNode.Children.Count; index++)
                {
                    var child = payloadNode.Children[index];
                    if (string.IsNullOrWhiteSpace(child.FieldName)
                        || string.IsNullOrWhiteSpace(child.ValueSummary))
                    {
                        continue;
                    }

                    payload[child.FieldName] = child.ValueSummary;
                }
            }

            return new BlueprintEventContext
            {
                EventKind = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "EventKind"),
                ActionId = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "ActionId"),
                ActionIndex = int.TryParse(ObservationNodeLookup.GetChildValueSummary(eventContextNode, "ActionIndex"), out var actionIndex) ? actionIndex : -1,
                Tick = int.TryParse(ObservationNodeLookup.GetChildValueSummary(eventContextNode, "Tick"), out var tick) ? tick : 0,
                SignalTag = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "SignalTag"),
                SubjectRefSerialized = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "SubjectRefSerialized"),
                SubjectSummary = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "SubjectSummary"),
                InstigatorRefSerialized = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "InstigatorRefSerialized"),
                InstigatorSummary = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "InstigatorSummary"),
                TargetRefSerialized = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "TargetRefSerialized"),
                TargetSummary = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "TargetSummary"),
                PayloadSummary = ObservationNodeLookup.GetChildValueSummary(eventContextNode, "PayloadSummary"),
                Payload = payload,
            };
        }
    }

    internal sealed class BlackboardAccessRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("BlackboardAccessNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var accessKind = context.GetStateValueOrEmpty("AccessKind");
            var variableName = context.GetStateValueOrEmpty("VariableName");
            var variableSummary = context.GetStateValueOrEmpty("VariableSummary");
            var accessSummary = context.GetStateValueOrEmpty("AccessSummary");
            var valueText = context.GetStateValueOrEmpty("ValueText");
            context.TryGetStateBool("Succeeded", out var succeeded);
            var failureReason = context.GetStateValueOrEmpty("FailureReason");

            var subtitleSource = string.IsNullOrWhiteSpace(variableSummary)
                ? variableName
                : variableSummary;
            var subtitle = string.IsNullOrWhiteSpace(subtitleSource)
                ? context.CommonSubtitle
                : $"{subtitleSource} | {context.CommonSubtitle}";
            var summaryText = succeeded
                ? (string.IsNullOrWhiteSpace(accessSummary)
                    ? SemanticSummaryUtility.BuildBlackboardAccessSummary(accessKind, variableSummary, valueText)
                    : accessSummary)
                : string.IsNullOrWhiteSpace(failureReason)
                    ? $"{SemanticSummaryUtility.NormalizeBlackboardAccessKind(accessKind)} 失败"
                    : $"{SemanticSummaryUtility.NormalizeBlackboardAccessKind(accessKind)} 失败: {RuntimeStatePresentationFactory.Shorten(failureReason)}";

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                subtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("访问类型", accessKind),
                    RuntimeStatePresentationFactory.CreateRow("变量摘要", variableSummary),
                    RuntimeStatePresentationFactory.CreateRow("变量名", variableName),
                    RuntimeStatePresentationFactory.CreateRow("作用域", context.GetStateValueOrEmpty("Scope")),
                    RuntimeStatePresentationFactory.CreateRow("变量类型", context.GetStateValueOrEmpty("VariableType")),
                    RuntimeStatePresentationFactory.CreateRow("访问摘要", accessSummary),
                    RuntimeStatePresentationFactory.CreateRow("值", valueText),
                    RuntimeStatePresentationFactory.CreateRow("是否成功", RuntimeStatePresentationFactory.ToYesNo(succeeded)),
                    RuntimeStatePresentationFactory.CreateRow("失败原因", failureReason),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                });
        }
    }

    internal sealed class FlowFilterRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("FlowFilterNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var compareValue = context.GetStateValueOrEmpty("CompareValueText");
            var op = context.GetStateValueOrEmpty("Operator");
            var constValue = context.GetStateValueOrEmpty("ConstValueText");
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var semantics = RuntimeSemanticPresentationUtility.BuildFlowFilterSemantics(
                op,
                constValue,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var conditionSummary = SemanticDescriptorUtility.GetConditionSummary(
                semantics,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var routedPort = context.GetStateValueOrEmpty("RoutedPort");
            context.TryGetStateBool("ConditionMet", out var conditionMet);
            context.TryGetStateBool("WasUnconditionalPass", out var unconditionalPass);

            var expression = unconditionalPass
                ? "无条件通过"
                : string.IsNullOrWhiteSpace(conditionSummary)
                    ? $"{RuntimeStatePresentationFactory.Shorten(compareValue)} {op} {RuntimeStatePresentationFactory.Shorten(constValue)}"
                    : $"{RuntimeStatePresentationFactory.Shorten(compareValue)} | {RuntimeStatePresentationFactory.Shorten(conditionSummary)}";
            var summaryText = string.IsNullOrWhiteSpace(routedPort)
                ? expression
                : $"{expression} -> {routedPort}";
            var subtitlePrefix = conditionMet ? "通过" : "未通过";
            var subtitle = string.IsNullOrWhiteSpace(conditionSummary)
                ? $"{subtitlePrefix} | {context.CommonSubtitle}"
                : $"{subtitlePrefix} | {conditionSummary} | {context.CommonSubtitle}";

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                subtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("比较值", compareValue),
                    RuntimeStatePresentationFactory.CreateRow("运算符", op),
                    RuntimeStatePresentationFactory.CreateRow("常量值", constValue),
                    RuntimeStatePresentationFactory.CreateRow("命中条件", RuntimeStatePresentationFactory.ToYesNo(conditionMet)),
                    RuntimeStatePresentationFactory.CreateRow("无条件通过", RuntimeStatePresentationFactory.ToYesNo(unconditionalPass)),
                    RuntimeStatePresentationFactory.CreateRow("输出端口", routedPort),
                    RuntimeStatePresentationFactory.CreateRow("评估次数", context.GetStateValueOrEmpty("EvaluationCount")),
                    RuntimeStatePresentationFactory.CreateRow("最近评估 Tick", context.GetStateValueOrEmpty("LastEvaluationTick")),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                });
        }
    }

    internal sealed class FlowBranchRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("BranchNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var routedPort = context.GetStateValueOrEmpty("RoutedPort");
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            context.TryGetStateBool("ConditionResult", out var conditionResult);
            var graphSemantics = RuntimeSemanticPresentationUtility.BuildFlowBranchSemantics(
                conditionResult,
                routedPort,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var conditionSummary = SemanticDescriptorUtility.GetGraphSummary(
                graphSemantics,
                context.GetStateValueOrEmpty("ConditionSummary"));

            var routeSummary = string.IsNullOrWhiteSpace(routedPort)
                ? conditionSummary
                : string.IsNullOrWhiteSpace(conditionSummary)
                    ? routedPort
                    : $"{conditionSummary} -> {routedPort}";
            var summaryText = !string.IsNullOrWhiteSpace(executionSummary)
                ? executionSummary
                : string.IsNullOrWhiteSpace(routeSummary)
                    ? (conditionResult ? "条件为真" : "条件为假")
                    : routeSummary;
            var subtitlePrefix = conditionResult ? "True" : "False";
            string subtitle;
            if (!string.IsNullOrWhiteSpace(planSummary))
            {
                subtitle = $"{subtitlePrefix} | {planSummary} | {context.CommonSubtitle}";
            }
            else if (!string.IsNullOrWhiteSpace(conditionSummary))
            {
                subtitle = $"{subtitlePrefix} | {conditionSummary} | {context.CommonSubtitle}";
            }
            else
            {
                subtitle = $"{subtitlePrefix} | {context.CommonSubtitle}";
            }

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                subtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("条件结果", RuntimeStatePresentationFactory.ToYesNo(conditionResult)),
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("输出端口", routedPort),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                });
        }
    }

    internal sealed class TimedRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("TimedNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            context.TryGetStateInt("StartTick", out var startTick);
            context.TryGetStateInt("TargetTick", out var targetTick);
            var elapsedText = RuntimeStatePresentationFactory.FormatElapsedTicks(context.CurrentTick, startTick);
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var conditionSummary = context.GetStateValueOrEmpty("ConditionSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var summaryText = !string.IsNullOrWhiteSpace(executionSummary)
                ? RuntimeStatePresentationFactory.Shorten(executionSummary, 72)
                : targetTick > startTick
                ? $"开始 T={startTick} -> 截止 T={targetTick}"
                : $"开始 T={startTick}";
            if (string.IsNullOrWhiteSpace(executionSummary) && !string.IsNullOrWhiteSpace(elapsedText))
            {
                summaryText = $"{summaryText} | {elapsedText}";
            }

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                context.CommonSubtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                    RuntimeStatePresentationFactory.CreateRow("目标 Tick", context.GetStateValueOrEmpty("TargetTick")),
                    RuntimeStatePresentationFactory.CreateRow("已运行", elapsedText),
                });
        }
    }

    internal sealed class WaitSignalRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("WaitSignalNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var signalTag = context.GetStateValueOrEmpty("SignalTag");
            var subjectRefFilterSerialized = context.GetStateValueOrEmpty("SubjectRefFilterSerialized");
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            context.TryGetStateBool("IsWildcardPattern", out var isWildcard);
            context.TryGetStateFloat("TimeoutSeconds", out var timeoutSeconds);
            var semantics = RuntimeSemanticPresentationUtility.BuildWaitSignalSemantics(
                signalTag,
                subjectRefFilterSerialized,
                context.GetStateValueOrEmpty("SubjectRefFilterSummary"),
                isWildcard,
                timeoutSeconds,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var subjectRefFilterSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "subject-filter",
                context.GetStateValueOrEmpty("SubjectRefFilterSummary"));
            subjectRefFilterSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "subject-filter",
                subjectRefFilterSerialized);
            signalTag = SemanticDescriptorUtility.GetConditionSignalTag(semantics, signalTag);
            var conditionSummary = SemanticDescriptorUtility.GetConditionSummary(
                semantics,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var timeoutText = RuntimeStatePresentationFactory.FormatDeadlineText(
                context.GetStateValueOrEmpty("TimeoutTargetTick"));
            var summaryText = !string.IsNullOrWhiteSpace(executionSummary)
                ? RuntimeStatePresentationFactory.Shorten(executionSummary, 72)
                : string.IsNullOrWhiteSpace(conditionSummary)
                ? string.IsNullOrWhiteSpace(signalTag)
                    ? "等待未配置的信号"
                    : isWildcard
                        ? $"等待 {signalTag}（通配）"
                        : $"等待 {signalTag}"
                : RuntimeStatePresentationFactory.Shorten(conditionSummary, 72);
            if (string.IsNullOrWhiteSpace(conditionSummary) && !string.IsNullOrWhiteSpace(subjectRefFilterSummary))
            {
                summaryText = $"{summaryText} | 主体 {subjectRefFilterSummary}";
            }
            if (!string.IsNullOrWhiteSpace(timeoutText))
            {
                summaryText = $"{summaryText} | {timeoutText}";
            }

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                context.CommonSubtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("信号标签", signalTag),
                    RuntimeStatePresentationFactory.CreateRow("主体过滤", subjectRefFilterSummary),
                    RuntimeStatePresentationFactory.CreateRow("主体过滤引用", subjectRefFilterSerialized),
                    RuntimeStatePresentationFactory.CreateRow("通配匹配", RuntimeStatePresentationFactory.ToYesNo(isWildcard)),
                    RuntimeStatePresentationFactory.CreateRow("超时截止", context.GetStateValueOrEmpty("TimeoutTargetTick")),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                    RuntimeStatePresentationFactory.CreateRow("ReactiveWait", context.GetStateValueOrEmpty("ReactiveWaitEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("ReactiveSubscription", context.GetStateValueOrEmpty("ReactiveSubscriptionEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("Scheduling", context.GetStateValueOrEmpty("SchedulingEntryRef")),
                });
        }
    }

    internal sealed class WatchConditionRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("WatchConditionNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var conditionType = context.GetStateValueOrEmpty("ConditionType");
            var parametersRaw = context.GetStateValueOrEmpty("ParametersRaw");
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var timeoutSeconds = context.GetStateValueOrEmpty("TimeoutSeconds");
            context.TryGetStateBool("Repeat", out var repeat);
            context.TryGetStateFloat("TimeoutSeconds", out var timeoutSecondsValue);
            var semantics = RuntimeSemanticPresentationUtility.BuildWatchConditionSemantics(
                conditionType,
                context.GetStateValueOrEmpty("TargetRefSerialized"),
                context.GetStateValueOrEmpty("TargetSummary"),
                parametersRaw,
                timeoutSecondsValue,
                repeat,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var targetSummary = SemanticDescriptorUtility.GetTargetSummary(
                semantics,
                "target",
                context.GetStateValueOrEmpty("TargetSummary"));
            var targetRefSerialized = SemanticDescriptorUtility.GetTargetReference(
                semantics,
                "target",
                context.GetStateValueOrEmpty("TargetRefSerialized"));
            var conditionSummary = SemanticDescriptorUtility.GetConditionSummary(
                semantics,
                context.GetStateValueOrEmpty("ConditionSummary"));
            conditionType = SemanticDescriptorUtility.GetConditionType(semantics, conditionType);
            parametersRaw = SemanticDescriptorUtility.GetConditionParametersRaw(semantics, parametersRaw);
            var subtitle = string.IsNullOrWhiteSpace(targetSummary)
                ? context.CommonSubtitle
                : $"{targetSummary} | {context.CommonSubtitle}";
            var summaryText = !string.IsNullOrWhiteSpace(executionSummary)
                ? RuntimeStatePresentationFactory.Shorten(executionSummary, 72)
                : string.IsNullOrWhiteSpace(conditionSummary)
                ? string.IsNullOrWhiteSpace(parametersRaw)
                    ? $"{conditionType} 监听中"
                    : $"{conditionType} | {RuntimeStatePresentationFactory.Shorten(parametersRaw)}"
                : RuntimeStatePresentationFactory.Shorten(conditionSummary, 72);

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                subtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("条件类型", conditionType),
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("目标", targetSummary),
                    RuntimeStatePresentationFactory.CreateRow("目标引用", targetRefSerialized),
                    RuntimeStatePresentationFactory.CreateRow("条件参数", parametersRaw),
                    RuntimeStatePresentationFactory.CreateRow("重复触发", RuntimeStatePresentationFactory.ToYesNo(repeat)),
                    RuntimeStatePresentationFactory.CreateRow("超时秒数", timeoutSeconds),
                    RuntimeStatePresentationFactory.CreateRow("超时截止", context.GetStateValueOrEmpty("TimeoutTargetTick")),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                    RuntimeStatePresentationFactory.CreateRow("ReactiveWait", context.GetStateValueOrEmpty("ReactiveWaitEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("ReactiveSubscription", context.GetStateValueOrEmpty("ReactiveSubscriptionEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("Scheduling", context.GetStateValueOrEmpty("SchedulingEntryRef")),
                });
        }
    }

    internal sealed class TriggerEnterAreaRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("TriggerEnterAreaNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            context.TryGetStateBool("RequireFullyInside", out var requireFullyInside);
            var semantics = RuntimeSemanticPresentationUtility.BuildTriggerEnterAreaSemantics(
                context.GetStateValueOrEmpty("SubjectRefSerialized"),
                context.GetStateValueOrEmpty("SubjectSummary"),
                context.GetStateValueOrEmpty("TriggerAreaPayloadJson"),
                context.GetStateValueOrEmpty("TriggerAreaSummary"),
                requireFullyInside,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var subjectSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "subject",
                context.GetStateValueOrEmpty("SubjectSummary"));
            var subjectRefSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "subject",
                context.GetStateValueOrEmpty("SubjectRefSerialized"));
            var triggerAreaSummary = SemanticDescriptorUtility.GetTargetSummary(
                semantics,
                "area",
                context.GetStateValueOrEmpty("TriggerAreaSummary"));
            var conditionSummary = SemanticDescriptorUtility.GetConditionSummary(
                semantics,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var waitReason = context.GetStateValueOrEmpty("LastWaitReason");
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            context.TryGetStateBool("HasTriggerAreaBinding", out var hasBinding);
            context.TryGetStateBool("TreatAsAlwaysSatisfied", out var treatAsAlwaysSatisfied);

            var summaryText = !string.IsNullOrWhiteSpace(executionSummary)
                ? RuntimeStatePresentationFactory.Shorten(executionSummary, 72)
                : string.IsNullOrWhiteSpace(waitReason)
                ? RuntimeStatePresentationFactory.Shorten(conditionSummary, 72)
                : RuntimeStatePresentationFactory.Shorten(waitReason, 72);

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                string.IsNullOrWhiteSpace(subjectSummary) ? context.CommonSubtitle : $"{subjectSummary} | {context.CommonSubtitle}",
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("主体", subjectSummary),
                    RuntimeStatePresentationFactory.CreateRow("主体引用", subjectRefSerialized),
                    RuntimeStatePresentationFactory.CreateRow("区域", triggerAreaSummary),
                    RuntimeStatePresentationFactory.CreateRow("需要完全进入", RuntimeStatePresentationFactory.ToYesNo(requireFullyInside)),
                    RuntimeStatePresentationFactory.CreateRow("已绑定区域", RuntimeStatePresentationFactory.ToYesNo(hasBinding)),
                    RuntimeStatePresentationFactory.CreateRow("退化为默认满足", RuntimeStatePresentationFactory.ToYesNo(treatAsAlwaysSatisfied)),
                    RuntimeStatePresentationFactory.CreateRow("当前等待原因", waitReason),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                });
        }
    }

    internal sealed class InteractionApproachTargetRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("InteractionApproachTargetNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var waitReason = context.GetStateValueOrEmpty("LastWaitReason");
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var triggerRange = context.GetStateValueOrEmpty("TriggerRange");
            context.TryGetStateFloat("TriggerRange", out var triggerRangeValue);
            var semantics = RuntimeSemanticPresentationUtility.BuildInteractionApproachSemantics(
                context.GetStateValueOrEmpty("SubjectRefSerialized"),
                context.GetStateValueOrEmpty("SubjectSummary"),
                context.GetStateValueOrEmpty("TargetRefSerialized"),
                context.GetStateValueOrEmpty("TargetSummary"),
                triggerRangeValue,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var subjectSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "subject",
                context.GetStateValueOrEmpty("SubjectSummary"));
            var subjectRefSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "subject",
                context.GetStateValueOrEmpty("SubjectRefSerialized"));
            var targetSummary = SemanticDescriptorUtility.GetTargetSummary(
                semantics,
                "target",
                context.GetStateValueOrEmpty("TargetSummary"));
            var targetRefSerialized = SemanticDescriptorUtility.GetTargetReference(
                semantics,
                "target",
                context.GetStateValueOrEmpty("TargetRefSerialized"));
            var conditionSummary = SemanticDescriptorUtility.GetConditionSummary(
                semantics,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var summaryText = !string.IsNullOrWhiteSpace(executionSummary)
                ? RuntimeStatePresentationFactory.Shorten(executionSummary, 72)
                : string.IsNullOrWhiteSpace(waitReason)
                ? RuntimeStatePresentationFactory.Shorten(conditionSummary, 72)
                : RuntimeStatePresentationFactory.Shorten(waitReason, 72);

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                string.IsNullOrWhiteSpace(targetSummary) ? context.CommonSubtitle : $"{targetSummary} | {context.CommonSubtitle}",
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("主体", subjectSummary),
                    RuntimeStatePresentationFactory.CreateRow("主体引用", subjectRefSerialized),
                    RuntimeStatePresentationFactory.CreateRow("目标", targetSummary),
                    RuntimeStatePresentationFactory.CreateRow("目标引用", targetRefSerialized),
                    RuntimeStatePresentationFactory.CreateRow("触发距离", triggerRange),
                    RuntimeStatePresentationFactory.CreateRow("当前等待原因", waitReason),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                });
        }
    }

    internal sealed class CompositeConditionRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("CompositeConditionNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var mode = context.GetStateValueOrEmpty("Mode");
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var connectedPortSummary = context.GetStateValueOrEmpty("ConnectedPortSummary");
            context.TryGetStateInt("ConnectedMask", out var connectedMask);
            context.TryGetStateInt("TriggeredMask", out var triggeredMask);
            var connectedCount = RuntimeStatePresentationFactory.CountBits(connectedMask);
            var triggeredCount = RuntimeStatePresentationFactory.CountBits(triggeredMask);
            context.TryGetStateFloat("TimeoutSeconds", out var timeoutSeconds);
            var semantics = RuntimeSemanticPresentationUtility.BuildCompositeConditionSemantics(
                mode,
                connectedCount,
                connectedMask,
                connectedPortSummary,
                timeoutSeconds,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var modeSummary = SemanticSummaryUtility.DescribeCompositeConditionMode(mode);
            var conditionSummary = SemanticDescriptorUtility.GetConditionSummary(
                semantics,
                context.GetStateValueOrEmpty("ConditionSummary"));
            connectedPortSummary = SemanticDescriptorUtility.GetGraphSummary(
                semantics,
                connectedPortSummary);
            var progressSummary = string.IsNullOrWhiteSpace(executionSummary)
                ? $"已触发 {triggeredCount}/{connectedCount}"
                : executionSummary;
            var summaryText = string.IsNullOrWhiteSpace(conditionSummary)
                ? progressSummary
                : $"{RuntimeStatePresentationFactory.Shorten(conditionSummary, 48)} | {progressSummary}";

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                context.CommonSubtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("语义模式", modeSummary),
                    RuntimeStatePresentationFactory.CreateRow("语义连接条件", connectedPortSummary),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", progressSummary),
                    RuntimeStatePresentationFactory.CreateRow("已连接掩码", RuntimeStatePresentationFactory.FormatMask(connectedMask)),
                    RuntimeStatePresentationFactory.CreateRow("已触发掩码", RuntimeStatePresentationFactory.FormatMask(triggeredMask)),
                    RuntimeStatePresentationFactory.CreateRow("已连接条件数", connectedCount.ToString(CultureInfo.InvariantCulture)),
                    RuntimeStatePresentationFactory.CreateRow("已触发条件数", triggeredCount.ToString(CultureInfo.InvariantCulture)),
                    RuntimeStatePresentationFactory.CreateRow("超时截止", context.GetStateValueOrEmpty("TimeoutTargetTick")),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                    RuntimeStatePresentationFactory.CreateRow("ReactiveWait", context.GetStateValueOrEmpty("ReactiveWaitEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("Cond0 Subscription", context.GetStateValueOrEmpty("ReactiveCond0SubscriptionEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("Cond1 Subscription", context.GetStateValueOrEmpty("ReactiveCond1SubscriptionEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("Cond2 Subscription", context.GetStateValueOrEmpty("ReactiveCond2SubscriptionEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("Cond3 Subscription", context.GetStateValueOrEmpty("ReactiveCond3SubscriptionEntryRef")),
                    RuntimeStatePresentationFactory.CreateRow("Scheduling", context.GetStateValueOrEmpty("SchedulingEntryRef")),
                });
        }
    }

    internal sealed class SpawnWaveRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("SpawnWaveStockState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planPosMode = context.GetStateValueOrEmpty("PlanPosMode");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var planWaveCount = context.GetStateValueOrEmpty("PlanWaveCount");
            context.TryGetStateInt("CurrentWaveIndex", out var currentWaveIndex);
            context.TryGetStateInt("CurrentWaveRequestedSpawnCount", out var currentWaveRequestedSpawnCount);
            context.TryGetStateInt("CurrentWaveSubjectSlotCount", out var currentWaveSubjectSlotCount);
            context.TryGetStateInt("CurrentWavePublicSubjectCount", out var currentWavePublicSubjectCount);
            context.TryGetStateInt("TotalInitialCount", out var totalInitialCount);
            context.TryGetStateInt("RemainingTotal", out var remainingTotal);
            var inventorySummary = remainingTotal > 0 || totalInitialCount > 0
                ? $"{remainingTotal}/{totalInitialCount}"
                : string.Empty;
            var planDiagnostic = BuildSpawnPlanDiagnostic(planSource);

            var currentWaveId = context.GetStateValueOrEmpty("CurrentWaveId");
            var currentWaveLabel = string.IsNullOrWhiteSpace(currentWaveId)
                ? $"wave#{Math.Max(0, currentWaveIndex) + 1}"
                : currentWaveId;
            var defaultExecutionText =
                $"当前波 {currentWaveLabel} | 主体位 {currentWaveSubjectSlotCount}/{currentWaveRequestedSpawnCount} | 公共主体 {currentWavePublicSubjectCount}";
            var summaryText = string.IsNullOrWhiteSpace(executionSummary)
                ? defaultExecutionText
                : executionSummary;
            if (!string.IsNullOrWhiteSpace(inventorySummary))
            {
                summaryText = $"{summaryText} | 供给库存 {inventorySummary}";
            }

            var nextWaveId = context.GetStateValueOrEmpty("NextWaveId");
            context.TryGetStateInt("NextWaveRequestedSpawnCount", out var nextWaveRequestedSpawnCount);
            context.TryGetStateInt("NextWaveSubjectSlotCount", out var nextWaveSubjectSlotCount);
            context.TryGetStateInt("NextWavePublicSubjectCount", out var nextWavePublicSubjectCount);

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                context.CommonSubtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow(
                        "当前波主体身份",
                        RuntimeStatePresentationFactory.FirstNonEmpty(
                            context.GetStateValueOrEmpty("CurrentWaveSubjectIdentitySummary"),
                            context.GetStateValueOrEmpty("CurrentWaveSubjectSlotsSummary"))),
                    RuntimeStatePresentationFactory.CreateRow("当前波主体位", context.GetStateValueOrEmpty("CurrentWaveSubjectSlotsSummary")),
                    RuntimeStatePresentationFactory.CreateRow(
                        "下一波主体身份",
                        RuntimeStatePresentationFactory.FirstNonEmpty(
                            context.GetStateValueOrEmpty("NextWaveSubjectIdentitySummary"),
                            context.GetStateValueOrEmpty("NextWaveSubjectSlotsSummary"))),
                    RuntimeStatePresentationFactory.CreateRow("下一波主体位", context.GetStateValueOrEmpty("NextWaveSubjectSlotsSummary")),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划诊断", planDiagnostic),
                    RuntimeStatePresentationFactory.CreateRow("计划位置模式", planPosMode),
                    RuntimeStatePresentationFactory.CreateRow("计划波次数", planWaveCount),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", string.IsNullOrWhiteSpace(executionSummary) ? defaultExecutionText : executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("当前消费波", currentWaveLabel),
                    RuntimeStatePresentationFactory.CreateRow("当前延迟 Tick", context.GetStateValueOrEmpty("CurrentWaveDelayTicks")),
                    RuntimeStatePresentationFactory.CreateRow("当前请求数", context.GetStateValueOrEmpty("CurrentWaveRequestedSpawnCount")),
                    RuntimeStatePresentationFactory.CreateRow("当前主体位", context.GetStateValueOrEmpty("CurrentWaveSubjectSlotCount")),
                    RuntimeStatePresentationFactory.CreateRow("当前公共主体", context.GetStateValueOrEmpty("CurrentWavePublicSubjectCount")),
                    RuntimeStatePresentationFactory.CreateRow("下一消费波", string.IsNullOrWhiteSpace(nextWaveId) ? "无" : nextWaveId),
                    RuntimeStatePresentationFactory.CreateRow("下一波请求数", nextWaveRequestedSpawnCount > 0 ? nextWaveRequestedSpawnCount.ToString(CultureInfo.InvariantCulture) : string.Empty),
                    RuntimeStatePresentationFactory.CreateRow("下一波主体位", nextWaveSubjectSlotCount > 0 ? nextWaveSubjectSlotCount.ToString(CultureInfo.InvariantCulture) : string.Empty),
                    RuntimeStatePresentationFactory.CreateRow("下一波公共主体", nextWavePublicSubjectCount > 0 ? nextWavePublicSubjectCount.ToString(CultureInfo.InvariantCulture) : string.Empty),
                    RuntimeStatePresentationFactory.CreateRow("供给库存", inventorySummary),
                    RuntimeStatePresentationFactory.CreateRow("上次刷怪 Tick", context.GetStateValueOrEmpty("LastSpawnTick")),
                });
        }

        private static string BuildSpawnPlanDiagnostic(string planSource)
        {
            return string.Equals(planSource, "compiled-missing", StringComparison.Ordinal)
                ? "未找到 compiled plan；当前节点仅保留可收敛的空计划。"
                : string.Empty;
        }
    }

    internal sealed class SpawnPresetRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("SpawnPresetNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var conditionSummary = context.GetStateValueOrEmpty("ConditionSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var subjectIdentitySummary = context.GetStateValueOrEmpty("SubjectIdentitySummary");
            var planDiagnostic = BuildSpawnPlanDiagnostic(planSource);
            var summaryText = RuntimeStatePresentationFactory.FirstNonEmpty(
                executionSummary,
                subjectIdentitySummary,
                planSummary);

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                context.CommonSubtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("请求主体身份", subjectIdentitySummary),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划诊断", planDiagnostic),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", executionSummary),
                    RuntimeStatePresentationFactory.CreateRow("请求数量", context.GetStateValueOrEmpty("RequestedSpawnCount")),
                    RuntimeStatePresentationFactory.CreateRow("公共主体数量", context.GetStateValueOrEmpty("PublicSubjectCount")),
                    RuntimeStatePresentationFactory.CreateRow("最近刷怪数量", context.GetStateValueOrEmpty("LastSpawnCount")),
                    RuntimeStatePresentationFactory.CreateRow("最近错误", context.GetStateValueOrEmpty("LastErrorMessage")),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                });
        }

        private static string BuildSpawnPlanDiagnostic(string planSource)
        {
            return string.Equals(planSource, "compiled-missing", StringComparison.Ordinal)
                ? "未找到 compiled plan；当前节点不会继续解析点位刷怪请求。"
                : string.Empty;
        }
    }

    internal sealed class JoinRuntimeStatePresenter : IRuntimeStatePresenter
    {
        public bool CanPresent(RuntimeStateObservationContext context)
        {
            return context.HasStateType("JoinNodeState");
        }

        public RuntimeStatePresentationViewModel CreatePresentation(RuntimeStateObservationContext context)
        {
            var planSource = context.GetStateValueOrEmpty("PlanSource");
            var planSummary = context.GetStateValueOrEmpty("PlanSummary");
            var executionSummary = context.GetStateValueOrEmpty("ExecutionSummary");
            var requiredCount = context.GetStateValueOrEmpty("RequiredCount");
            var receivedCount = context.GetStateValueOrEmpty("ReceivedCount");
            context.TryGetStateInt("RequiredCount", out var requiredCountValue);
            var graphSemantics = RuntimeSemanticPresentationUtility.BuildFlowJoinSemantics(
                requiredCountValue,
                context.GetStateValueOrEmpty("IncomingActionSummary"),
                context.GetStateValueOrEmpty("ConditionSummary"));
            var conditionSummary = SemanticDescriptorUtility.GetGraphSummary(
                graphSemantics,
                context.GetStateValueOrEmpty("ConditionSummary"));
            var incomingActionSummary = context.GetStateValueOrEmpty("IncomingActionSummary");
            var progressSummary = string.IsNullOrWhiteSpace(executionSummary)
                ? $"已到达 {receivedCount}/{requiredCount} 输入"
                : executionSummary;
            var summaryText = string.IsNullOrWhiteSpace(conditionSummary)
                ? progressSummary
                : $"{RuntimeStatePresentationFactory.Shorten(conditionSummary, 48)} | {progressSummary}";

            return RuntimeStatePresentationFactory.CreatePresentation(
                context,
                context.CommonSubtitle,
                summaryText,
                new[]
                {
                    RuntimeStatePresentationFactory.CreateRow("语义摘要", conditionSummary),
                    RuntimeStatePresentationFactory.CreateRow("语义上游输入", incomingActionSummary),
                    RuntimeStatePresentationFactory.CreateRow("计划来源", planSource),
                    RuntimeStatePresentationFactory.CreateRow("计划摘要", planSummary),
                    RuntimeStatePresentationFactory.CreateRow("执行摘要", progressSummary),
                    RuntimeStatePresentationFactory.CreateRow("所需输入数", requiredCount),
                    RuntimeStatePresentationFactory.CreateRow("已到达输入数", receivedCount),
                    RuntimeStatePresentationFactory.CreateRow("起始 Tick", context.GetStateValueOrEmpty("StartTick")),
                });
        }
    }

    internal static class RuntimeStatePresentationFactory
    {
        public static RuntimeStatePresentationViewModel CreatePresentation(
            RuntimeStateObservationContext context,
            string subtitle,
            string summaryText,
            IReadOnlyList<RuntimeStateDetailFieldViewModel> specificRows)
        {
            var rows = new List<RuntimeStateDetailFieldViewModel>(4 + specificRows.Count);
            AppendCommonRows(rows, context);
            for (var index = 0; index < specificRows.Count; index++)
            {
                rows.Add(specificRows[index]);
            }

            var summary = new RuntimeStateSummaryViewModel(
                context.Entry.LogicalEntryKey,
                context.PreferredTitle,
                subtitle,
                summaryText,
                context.ActionId,
                context.ActionTypeId,
                context.ActionIndex,
                context.Phase,
                context.Entry.EntryRef);

            var detail = new RuntimeStateDetailViewModel(
                context.PreferredTitle,
                subtitle,
                summaryText,
                rows);

            return new RuntimeStatePresentationViewModel(summary, detail);
        }

        private static void AppendCommonRows(
            List<RuntimeStateDetailFieldViewModel> rows,
            RuntimeStateObservationContext context)
        {
            AddCommonRow(rows, "节点类型", context.GetActionTypeOrFallback());
            AddCommonRow(rows, "节点 Id", context.ActionId);
            AddCommonRow(rows, "当前 Phase", context.Phase?.ToString() ?? string.Empty);

            if (!ObservationNoiseReductionUtility.AreEquivalentObservationValues(
                    context.PreferredTitle,
                    context.DescriptorDebugName))
            {
                AddCommonRow(rows, "状态描述符", context.DescriptorDebugName);
            }

            if (string.IsNullOrWhiteSpace(context.ActionId) && context.ActionIndex.HasValue)
            {
                AddCommonRow(rows, "节点索引", context.ActionIndex.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (string.IsNullOrWhiteSpace(context.ActionId) && context.Entry.EntryRef.HasValue)
            {
                AddCommonRow(rows, "EntryRef", context.Entry.EntryRef.Value.ToString());
            }
        }

        private static void AddCommonRow(
            List<RuntimeStateDetailFieldViewModel> rows,
            string label,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                rows.Add(CreateRow(label, value, ObservationStage.RuntimeState));
            }
        }

        public static RuntimeStateDetailFieldViewModel CreateRow(string label, string value)
        {
            return CreateRow(label, value, ObservationStageUtility.InferFromLabel(label));
        }

        public static RuntimeStateDetailFieldViewModel CreateRow(string label, string value, ObservationStage stage)
        {
            return new RuntimeStateDetailFieldViewModel(
                label,
                value,
                ObservationStageUtility.GetTitle(stage));
        }

        public static RuntimeStateDetailFieldViewModel CreateRow(string label, string value, string? sectionTitle)
        {
            return new RuntimeStateDetailFieldViewModel(
                label,
                value,
                sectionTitle ?? string.Empty);
        }

        public static string ToYesNo(bool value)
        {
            return value ? "是" : "否";
        }

        public static string Shorten(string value, int maxLength = 48)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength - 3) + "...";
        }

        public static string FormatElapsedTicks(int? currentTick, int startTick)
        {
            if (!currentTick.HasValue || currentTick.Value < startTick)
            {
                return string.Empty;
            }

            return $"已运行 {currentTick.Value - startTick} Tick";
        }

        public static string FormatDeadlineText(string targetTickText)
        {
            if (!int.TryParse(targetTickText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetTick)
                || targetTick < 0)
            {
                return string.Empty;
            }

            return $"超时 T={targetTick}";
        }

        public static int CountBits(int value)
        {
            var count = 0;
            var mask = value;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }

            return count;
        }

        public static string FormatMask(int mask)
        {
            return $"0x{mask:X}";
        }

        public static string FirstNonEmpty(params string?[] values)
        {
            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index]!.Trim();
                }
            }

            return string.Empty;
        }
    }
}
