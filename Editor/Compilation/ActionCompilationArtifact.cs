#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Editor.Compilation
{
    /// <summary>
    /// Action compiler 的统一输出结构。
    /// 第一版先承接语义模型、编译计划、调试载荷、诊断和导出 metadata。
    /// </summary>
    public sealed class ActionCompilationArtifact
    {
        public ActionCompilationArtifact(
            string actionId,
            string actionTypeId,
            string compilerId,
            object? semanticModel = null,
            object? compiledPlan = null,
            object? debugPayload = null,
            ActionCompilationDiagnostic[]? diagnostics = null,
            PropertyValue[]? metadataEntries = null,
            ActionCompilationSemanticResult? semanticResult = null,
            ActionCompilationPlanResult? planResult = null,
            ActionCompilationDebugProjection? debugProjection = null)
        {
            ActionId = actionId ?? string.Empty;
            ActionTypeId = actionTypeId ?? string.Empty;
            CompilerId = compilerId ?? string.Empty;
            Semantic = semanticResult ?? new ActionCompilationSemanticResult(semanticModel);
            Plan = planResult ?? new ActionCompilationPlanResult(compiledPlan);
            Diagnostics = diagnostics ?? Array.Empty<ActionCompilationDiagnostic>();
            MetadataEntries = metadataEntries ?? Array.Empty<PropertyValue>();
            var debugSemantics = ResolvePreferredSemantics(
                Semantic.Semantics,
                Plan.Semantics,
                debugProjection?.Semantics);
            Debug = debugProjection ?? new ActionCompilationDebugProjection(
                debugPayload,
                semantics: debugSemantics,
                model: DebugProjectionModelFactory.CreateDefault(
                    Semantic.Payload,
                    Plan.Payload,
                    debugPayload,
                    debugSemantics,
                    Diagnostics,
                    TransportEntries));
        }

        public string ActionId { get; }

        public string ActionTypeId { get; }

        public string CompilerId { get; }

        public ActionCompilationSemanticResult Semantic { get; }

        public ActionCompilationPlanResult Plan { get; }

        public ActionCompilationDebugProjection Debug { get; }

        public SemanticDescriptorSet SemanticDescriptors => Semantic.Semantics;

        public SemanticDescriptorSet PlanDescriptors => Plan.Semantics;

        public SemanticDescriptorSet DebugDescriptors => Debug.Semantics;

        public object? SemanticModel => Semantic.Payload;

        public object? CompiledPlan => Plan.Payload;

        public object? DebugPayload => Debug.Payload;

        public ActionCompilationDiagnostic[] Diagnostics { get; }

        public PropertyValue[] MetadataEntries { get; }

        public PropertyValue[] TransportEntries => MetadataEntries;

        internal ActionCompilationTransportMetadataView TransportMetadata =>
            _transportMetadata ??= ActionCompilationTransportMetadataUtility.CreateView(TransportEntries);

        public int TransportMetadataCount => TransportMetadata.TotalCount;

        public int CompiledTransportEntryCount => TransportMetadata.CompiledPayloadCount;

        public int AuxiliaryTransportEntryCount => TransportMetadata.AuxiliaryEntryCount;

        public IReadOnlyList<string> TransportMetadataFamilies => TransportMetadata.Families;

        public string TransportMetadataSummary => TransportMetadata.Summary;

        public DebugProjectionModel DebugModel => Debug.Model;

        public bool HasDiagnostics => Diagnostics.Length > 0;

        public bool HasMetadataEntries => MetadataEntries.Length > 0;

        public bool HasTransportEntries => TransportEntries.Length > 0;

        public bool HasOutput =>
            Semantic.Payload != null
            || Plan.Payload != null
            || Debug.Payload != null
            || Diagnostics.Length > 0
            || TransportEntries.Length > 0;

        public bool TryGetSemanticModel<TModel>(out TModel? model)
            where TModel : class
        {
            model = Semantic.Payload as TModel;
            return model != null;
        }

        public bool TryGetCompiledPlan<TPlan>(out TPlan? plan)
            where TPlan : class
        {
            plan = Plan.Payload as TPlan;
            return plan != null;
        }

        public bool TryGetDebugPayload<TPayload>(out TPayload? payload)
            where TPayload : class
        {
            payload = Debug.Payload as TPayload;
            return payload != null;
        }

        public bool TryGetSemanticDescriptors(out SemanticDescriptorSet? semantics)
        {
            semantics = SemanticDescriptors;
            if (HasDescriptors(semantics))
            {
                return true;
            }

            semantics = PlanDescriptors;
            if (HasDescriptors(semantics))
            {
                return true;
            }

            semantics = DebugDescriptors;
            return HasDescriptors(semantics);
        }

        public SemanticDescriptorSet GetPreferredSemantics()
        {
            TryGetSemanticDescriptors(out var semantics);
            return semantics ?? new SemanticDescriptorSet();
        }

        public ActionCompilationArtifact WithAdditionalDiagnostics(
            ActionCompilationDiagnostic[]? additionalDiagnostics)
        {
            if (additionalDiagnostics == null || additionalDiagnostics.Length == 0)
            {
                return this;
            }

            var mergedDiagnostics = new ActionCompilationDiagnostic[Diagnostics.Length + additionalDiagnostics.Length];
            Array.Copy(additionalDiagnostics, 0, mergedDiagnostics, 0, additionalDiagnostics.Length);
            Array.Copy(Diagnostics, 0, mergedDiagnostics, additionalDiagnostics.Length, Diagnostics.Length);
            return new ActionCompilationArtifact(
                ActionId,
                ActionTypeId,
                CompilerId,
                Semantic.Payload,
                Plan.Payload,
                Debug.Payload,
                mergedDiagnostics,
                TransportEntries,
                Semantic,
                Plan,
                Debug);
        }

        public static ActionCompilationArtifact Empty(
            string actionId,
            string actionTypeId,
            string compilerId)
        {
            return new ActionCompilationArtifact(actionId, actionTypeId, compilerId);
        }

        public static ActionCompilationArtifact FromException(
            ActionCompilationContext context,
            string compilerId,
            Exception exception)
        {
            return new ActionCompilationArtifact(
                context.ActionId,
                context.ActionTypeId,
                compilerId,
                diagnostics: new[]
                {
                    ActionCompilationDiagnostic.Error(
                        compilerId,
                        context.ActionId,
                        context.ActionTypeId,
                        "compiler.exception",
                        exception.Message,
                        ActionCompilationDiagnosticStage.CompilerException)
                });
        }

        private static bool HasDescriptors(SemanticDescriptorSet? semantics)
        {
            return semantics != null
                   && ((semantics.Subjects?.Length ?? 0) > 0
                       || (semantics.Targets?.Length ?? 0) > 0
                       || (semantics.Conditions?.Length ?? 0) > 0
                       || (semantics.Values?.Length ?? 0) > 0
                       || (semantics.Graphs?.Length ?? 0) > 0
                       || (semantics.EventContexts?.Length ?? 0) > 0);
        }

        private ActionCompilationTransportMetadataView? _transportMetadata;

        private static SemanticDescriptorSet ResolvePreferredSemantics(
            SemanticDescriptorSet? semanticDescriptors,
            SemanticDescriptorSet? planDescriptors,
            SemanticDescriptorSet? debugDescriptors)
        {
            if (HasDescriptors(semanticDescriptors))
            {
                return semanticDescriptors!;
            }

            if (HasDescriptors(planDescriptors))
            {
                return planDescriptors!;
            }

            if (HasDescriptors(debugDescriptors))
            {
                return debugDescriptors!;
            }

            return new SemanticDescriptorSet();
        }
    }
}
