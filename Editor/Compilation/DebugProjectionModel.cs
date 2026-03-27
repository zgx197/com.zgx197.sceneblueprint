#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor;

namespace SceneBlueprint.Editor.Compilation
{
    public sealed class DebugProjectionModel
    {
        public DebugProjectionModel(
            string title,
            string summary,
            IReadOnlyList<DebugProjectionSection>? sections = null,
            SemanticDescriptorSet? semantics = null,
            DebugProjectionReadbackSummary? readback = null)
        {
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            Sections = sections ?? Array.Empty<DebugProjectionSection>();
            Semantics = semantics ?? new SemanticDescriptorSet();
            Readback = readback ?? DebugProjectionReadbackSummary.Empty;
        }

        public string Title { get; }

        public string Summary { get; }

        public IReadOnlyList<DebugProjectionSection> Sections { get; }

        public SemanticDescriptorSet Semantics { get; }

        public DebugProjectionReadbackSummary Readback { get; }
    }

    public sealed class DebugProjectionReadbackSummary
    {
        public static DebugProjectionReadbackSummary Empty { get; } = new(
            definitionSummary: string.Empty,
            semanticSummary: string.Empty,
            planSummary: string.Empty);

        public DebugProjectionReadbackSummary(
            string definitionSummary,
            string semanticSummary,
            string planSummary)
        {
            DefinitionSummary = definitionSummary ?? string.Empty;
            SemanticSummary = semanticSummary ?? string.Empty;
            PlanSummary = planSummary ?? string.Empty;
        }

        public string DefinitionSummary { get; }

        public string SemanticSummary { get; }

        public string PlanSummary { get; }

        public bool HasContent =>
            !string.IsNullOrWhiteSpace(DefinitionSummary)
            || !string.IsNullOrWhiteSpace(SemanticSummary)
            || !string.IsNullOrWhiteSpace(PlanSummary);
    }

    public sealed class DebugProjectionSection
    {
        public DebugProjectionSection(
            string title,
            IReadOnlyList<DebugProjectionField>? fields = null,
            ObservationStage stage = ObservationStage.None)
        {
            Title = title ?? string.Empty;
            Fields = fields ?? Array.Empty<DebugProjectionField>();
            Stage = stage;
        }

        public string Title { get; }

        public IReadOnlyList<DebugProjectionField> Fields { get; }

        public ObservationStage Stage { get; }
    }

    public readonly struct DebugProjectionField
    {
        public DebugProjectionField(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; }

        public string Value { get; }
    }

    internal static class DebugProjectionModelFactory
    {
        public static DebugProjectionModel CreateDefault(
            object? payload,
            SemanticDescriptorSet? semantics,
            ActionCompilationDiagnostic[]? diagnostics,
            PropertyValue[]? metadataEntries,
            DebugProjectionReadbackSummary? readback = null)
        {
            return CreateDefault(
                semanticPayload: null,
                planPayload: null,
                debugPayload: payload,
                semantics,
                diagnostics,
                metadataEntries,
                readback);
        }

        public static DebugProjectionModel CreateDefault(
            object? semanticPayload,
            object? planPayload,
            object? debugPayload,
            SemanticDescriptorSet? semantics,
            ActionCompilationDiagnostic[]? diagnostics,
            PropertyValue[]? metadataEntries,
            DebugProjectionReadbackSummary? readback = null)
        {
            var normalizedSemantics = semantics ?? new SemanticDescriptorSet();
            var transportMetadata = ActionCompilationTransportMetadataUtility.CreateView(metadataEntries);
            var sections = new List<DebugProjectionSection>(4);
            var semanticSummary = BuildSemanticSummary(normalizedSemantics);

            var semanticFields = BuildSemanticFields(normalizedSemantics);
            AddPayloadTypeField(semanticFields, "Semantic Payload", semanticPayload);
            if (semanticFields.Count > 0)
            {
                sections.Add(new DebugProjectionSection(
                    ObservationStageUtility.GetTitle(ObservationStage.SemanticAnalysis),
                    semanticFields,
                    ObservationStage.SemanticAnalysis));
            }

            var planFields = new List<DebugProjectionField>(2);
            AddPayloadTypeField(planFields, "Plan Payload", planPayload);
            if (planFields.Count > 0)
            {
                sections.Add(new DebugProjectionSection(
                    ObservationStageUtility.GetTitle(ObservationStage.PlanCompilation),
                    planFields,
                    ObservationStage.PlanCompilation));
            }

            var debugFields = new List<DebugProjectionField>(3);
            AddPayloadTypeField(debugFields, "Debug Payload", debugPayload);
            if (diagnostics != null)
            {
                debugFields.Add(new DebugProjectionField("诊断数", diagnostics.Length.ToString()));
            }

            sections.Add(new DebugProjectionSection(
                ObservationStageUtility.GetTitle(ObservationStage.DebugProjection),
                debugFields,
                ObservationStage.DebugProjection));

            if (transportMetadata.TotalCount > 0)
            {
                var metadataFields = new List<DebugProjectionField>(4)
                {
                    new("总条目", transportMetadata.TotalCount.ToString()),
                    new("Compiled 条目", transportMetadata.CompiledPayloadCount.ToString()),
                };
                if (transportMetadata.AuxiliaryEntryCount > 0)
                {
                    metadataFields.Add(new DebugProjectionField("Auxiliary 条目", transportMetadata.AuxiliaryEntryCount.ToString()));
                }

                if (transportMetadata.Families.Count > 0)
                {
                    metadataFields.Add(new DebugProjectionField("Family", string.Join(", ", transportMetadata.Families)));
                }

                sections.Add(new DebugProjectionSection(
                    ObservationStageUtility.GetTitle(ObservationStage.Metadata),
                    metadataFields,
                    ObservationStage.Metadata));
            }

            return new DebugProjectionModel(
                title: debugPayload == null ? "编译调试投影" : debugPayload.GetType().Name,
                summary: BuildSummary(normalizedSemantics, diagnostics, planPayload ?? debugPayload ?? semanticPayload),
                sections: sections,
                semantics: normalizedSemantics,
                readback: readback ?? new DebugProjectionReadbackSummary(
                    definitionSummary: string.Empty,
                    semanticSummary: semanticSummary,
                    planSummary: BuildPlanSummary(planPayload, semanticSummary, debugPayload, semanticPayload)));
        }

        private static string BuildSummary(
            SemanticDescriptorSet semantics,
            ActionCompilationDiagnostic[]? diagnostics,
            object? payload)
        {
            var parts = new List<string>(4);
            var semanticSummary = BuildSemanticSummary(semantics);
            if (!string.IsNullOrWhiteSpace(semanticSummary))
            {
                parts.Add(semanticSummary);
            }

            if (diagnostics != null && diagnostics.Length > 0)
            {
                parts.Add($"诊断 {diagnostics.Length}");
            }

            if (parts.Count == 0 && payload != null)
            {
                parts.Add(payload.GetType().Name);
            }

            return parts.Count == 0 ? "无调试投影" : string.Join(" | ", parts);
        }

        private static string BuildSemanticSummary(SemanticDescriptorSet semantics)
        {
            return SemanticDescriptorUtility.GetConditionSummary(
                semantics,
                SemanticDescriptorUtility.GetGraphSummary(
                    semantics,
                    SemanticDescriptorUtility.GetEventSummary(
                        semantics,
                        SemanticDescriptorUtility.GetValueSummary(
                            semantics,
                            SemanticDescriptorUtility.GetSubjectSummary(
                                semantics,
                                fallback: SemanticDescriptorUtility.GetTargetSummary(semantics))))));
        }

        private static string BuildPlanSummary(
            object? planPayload,
            string semanticSummary,
            object? debugPayload,
            object? semanticPayload)
        {
            if (!string.IsNullOrWhiteSpace(semanticSummary))
            {
                return semanticSummary;
            }

            var payload = planPayload ?? debugPayload ?? semanticPayload;
            return payload?.GetType().Name ?? string.Empty;
        }

        private static List<DebugProjectionField> BuildSemanticFields(SemanticDescriptorSet semantics)
        {
            var fields = new List<DebugProjectionField>(14);
            AddIfNotEmpty(fields, "主体", SemanticDescriptorUtility.GetSubjectSummary(semantics));
            AddIfNotEmpty(fields, "主体标识", SemanticDescriptorIdentityUtility.BuildSubjectIdentitySummary(semantics.Subjects));
            AddIfNotEmpty(fields, "主体公共ID", SemanticDescriptorUtility.GetSubjectPublicSubjectId(semantics));
            AddIfNotEmpty(fields, "主体编译ID", SemanticDescriptorUtility.GetSubjectCompiledSubjectId(semantics));
            AddIfNotEmpty(fields, "主体实体ID", SemanticDescriptorUtility.GetSubjectRuntimeEntityId(semantics));
            AddIfNotEmpty(fields, "目标", SemanticDescriptorUtility.GetTargetSummary(semantics));
            AddIfNotEmpty(fields, "目标标识", SemanticDescriptorIdentityUtility.BuildTargetIdentitySummary(semantics.Targets));
            AddIfNotEmpty(fields, "目标公共ID", SemanticDescriptorUtility.GetTargetPublicSubjectId(semantics));
            AddIfNotEmpty(fields, "目标编译ID", SemanticDescriptorUtility.GetTargetCompiledSubjectId(semantics));
            AddIfNotEmpty(fields, "目标实体ID", SemanticDescriptorUtility.GetTargetRuntimeEntityId(semantics));
            AddIfNotEmpty(fields, "条件", SemanticDescriptorUtility.GetConditionSummary(semantics));
            AddIfNotEmpty(fields, "值", SemanticDescriptorUtility.GetValueSummary(semantics));
            AddIfNotEmpty(fields, "图结构", SemanticDescriptorUtility.GetGraphSummary(semantics));
            AddIfNotEmpty(fields, "事件上下文", SemanticDescriptorUtility.GetEventSummary(semantics));
            return fields;
        }

        private static void AddPayloadTypeField(List<DebugProjectionField> fields, string label, object? payload)
        {
            if (payload == null)
            {
                return;
            }

            fields.Add(new DebugProjectionField(label, payload.GetType().Name));
        }

        private static void AddIfNotEmpty(List<DebugProjectionField> fields, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields.Add(new DebugProjectionField(label, value));
            }
        }
    }
}
