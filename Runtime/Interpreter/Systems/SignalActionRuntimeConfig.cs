#nullable enable
using System;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal readonly struct SignalEmitRuntimeConfig
    {
        public SignalEmitRuntimeConfig(
            string planSource,
            string signalTag,
            SemanticDescriptorSet semantics,
            string subjectRefSerialized,
            string subjectSummary,
            string instigatorRefSerialized,
            string instigatorSummary,
            string targetRefSerialized,
            string targetSummary)
        {
            PlanSource = NormalizePlanSource(planSource);
            SignalTag = WaitSignalSemanticUtility.NormalizeSignalTag(signalTag);
            Semantics = semantics ?? new SemanticDescriptorSet();
            SubjectRefSerialized = subjectRefSerialized ?? string.Empty;
            SubjectSummary = subjectSummary ?? string.Empty;
            InstigatorRefSerialized = instigatorRefSerialized ?? string.Empty;
            InstigatorSummary = instigatorSummary ?? string.Empty;
            TargetRefSerialized = targetRefSerialized ?? string.Empty;
            TargetSummary = targetSummary ?? string.Empty;
        }

        public string PlanSource { get; }

        public string SignalTag { get; }

        public SemanticDescriptorSet Semantics { get; }

        public string SubjectRefSerialized { get; }

        public string SubjectSummary { get; }

        public string InstigatorRefSerialized { get; }

        public string InstigatorSummary { get; }

        public string TargetRefSerialized { get; }

        public string TargetSummary { get; }

        private static string NormalizePlanSource(string? planSource)
        {
            return string.IsNullOrWhiteSpace(planSource) ? "unresolved" : planSource.Trim();
        }
    }

    internal readonly struct WaitSignalRuntimeConfig
    {
        public WaitSignalRuntimeConfig(
            string planSource,
            string signalTag,
            SemanticDescriptorSet semantics,
            string subjectRefFilterSerialized,
            string subjectRefFilterSummary,
            bool isWildcardPattern,
            float timeoutSeconds,
            string conditionSummary)
        {
            PlanSource = NormalizePlanSource(planSource);
            SignalTag = WaitSignalSemanticUtility.NormalizeSignalTag(signalTag);
            Semantics = semantics ?? new SemanticDescriptorSet();
            SubjectRefFilterSerialized = subjectRefFilterSerialized ?? string.Empty;
            SubjectRefFilterSummary = subjectRefFilterSummary ?? string.Empty;
            IsWildcardPattern = isWildcardPattern;
            TimeoutSeconds = Math.Max(0f, timeoutSeconds);
            ConditionSummary = conditionSummary ?? string.Empty;
        }

        public string PlanSource { get; }

        public string SignalTag { get; }

        public SemanticDescriptorSet Semantics { get; }

        public string SubjectRefFilterSerialized { get; }

        public string SubjectRefFilterSummary { get; }

        public bool IsWildcardPattern { get; }

        public float TimeoutSeconds { get; }

        public string ConditionSummary { get; }

        public string PlanSummary =>
            string.IsNullOrWhiteSpace(ConditionSummary)
                ? PlanSource
                : $"{PlanSource} | {ConditionSummary}";

        private static string NormalizePlanSource(string? planSource)
        {
            return string.IsNullOrWhiteSpace(planSource) ? "unresolved" : planSource.Trim();
        }
    }

    internal readonly struct WatchConditionRuntimeConfig
    {
        public WatchConditionRuntimeConfig(
            string planSource,
            string conditionType,
            SemanticDescriptorSet semantics,
            string targetRefSerialized,
            string targetSummary,
            string parametersRaw,
            float timeoutSeconds,
            bool repeat,
            ConditionWatchDescriptor descriptor,
            string conditionSummary)
        {
            PlanSource = NormalizePlanSource(planSource);
            ConditionType = ConditionWatchSemanticUtility.NormalizeConditionType(conditionType);
            Semantics = semantics ?? new SemanticDescriptorSet();
            TargetRefSerialized = targetRefSerialized ?? string.Empty;
            TargetSummary = targetSummary ?? string.Empty;
            ParametersRaw = ConditionWatchSemanticUtility.SerializeParameters(
                ConditionType,
                parametersRaw);
            TimeoutSeconds = Math.Max(0f, timeoutSeconds);
            Repeat = repeat;
            Descriptor = descriptor ?? new ConditionWatchDescriptor();
            ConditionSummary = conditionSummary ?? string.Empty;
        }

        public string PlanSource { get; }

        public string ConditionType { get; }

        public SemanticDescriptorSet Semantics { get; }

        public string TargetRefSerialized { get; }

        public string TargetSummary { get; }

        public string ParametersRaw { get; }

        public float TimeoutSeconds { get; }

        public bool Repeat { get; }

        public ConditionWatchDescriptor Descriptor { get; }

        public string ConditionSummary { get; }

        public string PlanSummary =>
            string.IsNullOrWhiteSpace(ConditionSummary)
                ? PlanSource
                : $"{PlanSource} | {ConditionSummary}";

        private static string NormalizePlanSource(string? planSource)
        {
            return string.IsNullOrWhiteSpace(planSource) ? "unresolved" : planSource.Trim();
        }
    }

    internal static class SignalActionRuntimeConfigResolver
    {
        private const string MissingCompiledPlanSource = "compiled-missing";

        public static SignalEmitRuntimeConfig ResolveEmit(BlueprintFrame frame, int actionIndex)
        {
            if (TryResolveEmitFromCompiled(frame, actionIndex, out var runtimeConfig))
            {
                return runtimeConfig;
            }

            return CreateMissingEmitRuntimeConfig();
        }

        public static WaitSignalRuntimeConfig ResolveWaitSignal(BlueprintFrame frame, int actionIndex)
        {
            if (TryResolveWaitSignalFromCompiled(frame, actionIndex, out var runtimeConfig))
            {
                return runtimeConfig;
            }

            return CreateMissingWaitSignalRuntimeConfig();
        }

        public static WatchConditionRuntimeConfig ResolveWatchCondition(BlueprintFrame frame, int actionIndex)
        {
            if (TryResolveWatchConditionFromCompiled(frame, actionIndex, out var runtimeConfig))
            {
                return runtimeConfig;
            }

            return CreateMissingWatchConditionRuntimeConfig();
        }

        private static bool TryResolveEmitFromCompiled(
            BlueprintFrame frame,
            int actionIndex,
            out SignalEmitRuntimeConfig runtimeConfig)
        {
            runtimeConfig = default;
            var compiled = CompiledActionResolver.TryGetSignalEmit(frame, actionIndex);
            if (compiled == null)
            {
                return false;
            }

            runtimeConfig = ResolveEmitFromCompiled(compiled);
            return true;
        }

        private static bool TryResolveWaitSignalFromCompiled(
            BlueprintFrame frame,
            int actionIndex,
            out WaitSignalRuntimeConfig runtimeConfig)
        {
            runtimeConfig = default;
            var compiled = CompiledActionResolver.TryGetSignalWaitSignal(frame, actionIndex);
            if (compiled == null)
            {
                return false;
            }

            runtimeConfig = ResolveWaitSignalFromCompiled(compiled);
            return true;
        }

        private static bool TryResolveWatchConditionFromCompiled(
            BlueprintFrame frame,
            int actionIndex,
            out WatchConditionRuntimeConfig runtimeConfig)
        {
            runtimeConfig = default;
            var compiled = CompiledActionResolver.TryGetSignalWatchCondition(frame, actionIndex);
            if (compiled == null)
            {
                return false;
            }

            runtimeConfig = ResolveWatchConditionFromCompiled(compiled);
            return true;
        }

        private static SignalEmitRuntimeConfig ResolveEmitFromCompiled(SignalEmitCompiledData compiled)
        {
            var semantics = HasMeaningfulSemantics(compiled.Semantics)
                ? compiled.Semantics
                : BuildEmitSemantics(
                    WaitSignalSemanticUtility.NormalizeSignalTag(compiled.SignalTag),
                    CompiledEntityRefSemanticUtility.Resolve(compiled.Subject, string.Empty),
                    CompiledEntityRefSemanticUtility.Resolve(compiled.Instigator, string.Empty),
                    CompiledEntityRefSemanticUtility.Resolve(compiled.Target, string.Empty));
            var signalTag = WaitSignalSemanticUtility.NormalizeSignalTag(
                !string.IsNullOrWhiteSpace(compiled.SignalTag)
                    ? compiled.SignalTag
                    : SemanticDescriptorUtility.GetEventSignalTag(semantics));

            return new SignalEmitRuntimeConfig(
                "compiled",
                signalTag,
                semantics,
                SemanticDescriptorUtility.GetSubjectReference(semantics, "subject", compiled.Subject.Serialized),
                SemanticDescriptorUtility.GetSubjectSummary(semantics, "subject", compiled.Subject.Summary),
                SemanticDescriptorUtility.GetSubjectReference(semantics, "instigator", compiled.Instigator.Serialized),
                SemanticDescriptorUtility.GetSubjectSummary(semantics, "instigator", compiled.Instigator.Summary),
                SemanticDescriptorUtility.GetTargetReference(semantics, "target", compiled.Target.Serialized),
                SemanticDescriptorUtility.GetTargetSummary(semantics, "target", compiled.Target.Summary));
        }

        private static WaitSignalRuntimeConfig ResolveWaitSignalFromCompiled(SignalWaitSignalCompiledData compiled)
        {
            var semantics = HasMeaningfulSemantics(compiled.Semantics)
                ? compiled.Semantics
                : BuildWaitSignalSemantics(
                    WaitSignalSemanticUtility.NormalizeSignalTag(compiled.SignalTag),
                    compiled.SubjectFilter.Serialized,
                    compiled.SubjectFilter.Summary,
                    compiled.IsWildcardPattern,
                    compiled.TimeoutSeconds);
            var signalTag = WaitSignalSemanticUtility.NormalizeSignalTag(
                !string.IsNullOrWhiteSpace(compiled.SignalTag)
                    ? compiled.SignalTag
                    : SemanticDescriptorUtility.GetConditionSignalTag(semantics));
            var timeoutSeconds = compiled.TimeoutSeconds > 0f
                ? compiled.TimeoutSeconds
                : SemanticDescriptorUtility.GetConditionTimeoutSeconds(semantics, 0f);
            var isWildcardPattern = compiled.IsWildcardPattern
                || SemanticDescriptorUtility.GetConditionIsWildcardPattern(
                    semantics,
                    WaitSignalSemanticUtility.IsWildcardPattern(signalTag));
            var subjectRefFilterSerialized = SemanticDescriptorUtility.GetSubjectReference(
                semantics,
                "subject-filter",
                compiled.SubjectFilter.Serialized);
            var subjectRefFilterSummary = SemanticDescriptorUtility.GetSubjectSummary(
                semantics,
                "subject-filter",
                compiled.SubjectFilter.Summary);
            var conditionSummary = !string.IsNullOrWhiteSpace(compiled.ConditionSummary)
                ? compiled.ConditionSummary
                : SemanticDescriptorUtility.GetConditionSummary(
                    semantics,
                    WaitSignalSemanticUtility.BuildConditionSummary(
                        signalTag,
                        subjectRefFilterSummary));

            return new WaitSignalRuntimeConfig(
                "compiled",
                signalTag,
                semantics,
                subjectRefFilterSerialized,
                subjectRefFilterSummary,
                isWildcardPattern,
                timeoutSeconds,
                conditionSummary);
        }

        private static WatchConditionRuntimeConfig ResolveWatchConditionFromCompiled(SignalWatchConditionCompiledData compiled)
        {
            var compiledSemantics = HasMeaningfulSemantics(compiled.Semantics)
                ? compiled.Semantics
                : null;
            var descriptorTargetSerialized = EntityRefCodec.Serialize(compiled.Descriptor?.Target ?? new EntityRef());
            var conditionType = !string.IsNullOrWhiteSpace(compiled.ConditionType)
                ? ConditionWatchSemanticUtility.NormalizeConditionType(compiled.ConditionType)
                : ConditionWatchSemanticUtility.NormalizeConditionType(
                    SemanticDescriptorUtility.GetConditionType(
                        compiledSemantics,
                        compiled.Descriptor?.ConditionType ?? string.Empty));
            var targetRefSerialized = SemanticDescriptorUtility.GetTargetReference(
                compiledSemantics,
                "target",
                !string.IsNullOrWhiteSpace(compiled.Target.Serialized)
                    ? compiled.Target.Serialized
                    : descriptorTargetSerialized);
            var descriptorTargetSummary = string.IsNullOrWhiteSpace(descriptorTargetSerialized)
                ? string.Empty
                : SemanticSummaryUtility.DescribeEntityRef(descriptorTargetSerialized);
            var targetSummary = SemanticDescriptorUtility.GetTargetSummary(
                compiledSemantics,
                "target",
                !string.IsNullOrWhiteSpace(compiled.Target.Summary)
                    ? compiled.Target.Summary
                    : descriptorTargetSummary);
            var parametersRaw = ResolveParametersRawFromCompiled(compiled, compiledSemantics, conditionType);
            var timeoutSeconds = compiled.TimeoutSeconds > 0f
                ? compiled.TimeoutSeconds
                : SemanticDescriptorUtility.GetConditionTimeoutSeconds(
                    compiledSemantics,
                    Math.Max(0f, compiled.Descriptor?.Timeout ?? 0f));
            var repeat = compiled.Repeat
                || SemanticDescriptorUtility.GetConditionRepeat(compiledSemantics, compiled.Repeat);
            var effectiveSemantics = compiledSemantics ?? BuildWatchConditionSemantics(
                conditionType,
                targetRefSerialized,
                targetSummary,
                parametersRaw,
                timeoutSeconds,
                repeat);
            var conditionSummary = !string.IsNullOrWhiteSpace(compiled.ConditionSummary)
                ? compiled.ConditionSummary
                : SemanticDescriptorUtility.GetConditionSummary(
                    effectiveSemantics,
                    ConditionWatchSemanticUtility.BuildConditionSummary(
                        conditionType,
                        targetSummary,
                        parametersRaw));

            return new WatchConditionRuntimeConfig(
                "compiled",
                conditionType,
                effectiveSemantics,
                targetRefSerialized,
                targetSummary,
                parametersRaw,
                timeoutSeconds,
                repeat,
                BuildDescriptor(compiled, conditionType, targetRefSerialized, parametersRaw, timeoutSeconds),
                conditionSummary);
        }

        private static SignalEmitRuntimeConfig CreateMissingEmitRuntimeConfig()
        {
            return new SignalEmitRuntimeConfig(
                MissingCompiledPlanSource,
                string.Empty,
                new SemanticDescriptorSet(),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static WaitSignalRuntimeConfig CreateMissingWaitSignalRuntimeConfig()
        {
            return new WaitSignalRuntimeConfig(
                MissingCompiledPlanSource,
                string.Empty,
                new SemanticDescriptorSet(),
                string.Empty,
                string.Empty,
                false,
                1f,
                "缺少 WaitSignal compiled plan，按默认超时 1s 收口");
        }

        private static WatchConditionRuntimeConfig CreateMissingWatchConditionRuntimeConfig()
        {
            return new WatchConditionRuntimeConfig(
                MissingCompiledPlanSource,
                string.Empty,
                new SemanticDescriptorSet(),
                string.Empty,
                string.Empty,
                string.Empty,
                1f,
                false,
                new ConditionWatchDescriptor
                {
                    ConditionType = string.Empty,
                    Parameters = Array.Empty<ConditionParameter>(),
                    Timeout = 1f,
                },
                "缺少 WatchCondition compiled plan，按默认超时 1s 收口");
        }

        private static string ResolveParametersRawFromCompiled(
            SignalWatchConditionCompiledData compiled,
            SemanticDescriptorSet? compiledSemantics,
            string conditionType)
        {
            if (!string.IsNullOrWhiteSpace(compiled.ParametersRaw))
            {
                return ConditionWatchSemanticUtility.SerializeParameters(conditionType, compiled.ParametersRaw);
            }

            if (compiled.Parameters != null && compiled.Parameters.Length > 0)
            {
                return ConditionWatchSemanticUtility.SerializeParameters(conditionType, compiled.Parameters);
            }

            if (compiled.Descriptor?.Parameters != null && compiled.Descriptor.Parameters.Length > 0)
            {
                return ConditionWatchSemanticUtility.SerializeParameters(conditionType, compiled.Descriptor.Parameters);
            }

            var semanticParametersRaw = SemanticDescriptorUtility.GetConditionParametersRaw(compiledSemantics);
            if (!string.IsNullOrWhiteSpace(semanticParametersRaw))
            {
                return ConditionWatchSemanticUtility.SerializeParameters(conditionType, semanticParametersRaw);
            }

            return ConditionWatchSemanticUtility.SerializeParameters(conditionType, string.Empty);
        }

        private static ConditionWatchDescriptor BuildDescriptor(
            SignalWatchConditionCompiledData? compiled,
            string conditionType,
            string targetRefSerialized,
            string parametersRaw,
            float timeoutSeconds)
        {
            if (compiled?.Descriptor != null && !string.IsNullOrWhiteSpace(compiled.Descriptor.ConditionType))
            {
                var descriptorConditionType = ConditionWatchSemanticUtility.NormalizeConditionType(
                    compiled.Descriptor.ConditionType);
                var descriptorParameters = compiled.Descriptor.Parameters != null && compiled.Descriptor.Parameters.Length > 0
                    ? ConditionWatchSemanticUtility.NormalizeParameters(
                        descriptorConditionType,
                        compiled.Descriptor.Parameters)
                    : ConditionWatchSemanticUtility.NormalizeParameters(
                        descriptorConditionType,
                        parametersRaw);
                return new ConditionWatchDescriptor
                {
                    ConditionType = descriptorConditionType,
                    Target = EntityRefCodec.Parse(targetRefSerialized),
                    Parameters = CloneParameters(descriptorParameters),
                    Timeout = Math.Max(0f, timeoutSeconds),
                };
            }

            return ConditionWatchSemanticUtility.BuildDescriptor(
                conditionType,
                targetRefSerialized,
                parametersRaw,
                timeoutSeconds);
        }

        private static ConditionParameter[] CloneParameters(ConditionParameter[]? parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return Array.Empty<ConditionParameter>();
            }

            var cloned = new ConditionParameter[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                var source = parameters[index];
                cloned[index] = new ConditionParameter
                {
                    Key = source?.Key ?? string.Empty,
                    Value = source?.Value ?? string.Empty,
                };
            }

            return cloned;
        }

        private static bool HasMeaningfulSemantics(SemanticDescriptorSet? semantics)
        {
            return semantics != null
                   && ((semantics.Subjects?.Length ?? 0) > 0
                       || (semantics.Targets?.Length ?? 0) > 0
                       || (semantics.Conditions?.Length ?? 0) > 0
                       || (semantics.EventContexts?.Length ?? 0) > 0
                       || (semantics.Graphs?.Length ?? 0) > 0
                       || (semantics.Values?.Length ?? 0) > 0);
        }

        private static SemanticDescriptorSet BuildEmitSemantics(
            string signalTag,
            ResolvedEntityRefSemantic subject,
            ResolvedEntityRefSemantic instigator,
            ResolvedEntityRefSemantic target)
        {
            return new SemanticDescriptorSet
            {
                Subjects = new[]
                {
                    SemanticDescriptorUtility.BuildSubjectDescriptor("subject", subject.Serialized, subject.Summary),
                    SemanticDescriptorUtility.BuildSubjectDescriptor("instigator", instigator.Serialized, instigator.Summary),
                },
                Targets = new[]
                {
                    SemanticDescriptorUtility.BuildTargetDescriptor("target", "entity-ref", target.Serialized, target.Summary),
                },
                EventContexts = new[]
                {
                    SemanticDescriptorUtility.BuildEventContextDescriptor(
                        AT.Signal.Emit,
                        signalTag,
                        subject.Summary,
                        BlueprintEventContextSemanticUtility.BuildPayloadSummary(SignalPayload.Empty),
                        instigator.Summary,
                        target.Summary),
                },
            };
        }

        private static SemanticDescriptorSet BuildWaitSignalSemantics(
            string signalTag,
            string subjectFilterSerialized,
            string subjectFilterSummary,
            bool isWildcardPattern,
            float timeoutSeconds)
        {
            return new SemanticDescriptorSet
            {
                Subjects = new[]
                {
                    SemanticDescriptorUtility.BuildSubjectDescriptor(
                        "subject-filter",
                        subjectFilterSerialized,
                        subjectFilterSummary),
                },
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildWaitSignalConditionDescriptor(
                        signalTag,
                        subjectFilterSummary,
                        isWildcardPattern,
                        timeoutSeconds),
                },
            };
        }

        private static SemanticDescriptorSet BuildWatchConditionSemantics(
            string conditionType,
            string targetRefSerialized,
            string targetSummary,
            string parametersRaw,
            float timeoutSeconds,
            bool repeat)
        {
            return new SemanticDescriptorSet
            {
                Targets = new[]
                {
                    SemanticDescriptorUtility.BuildTargetDescriptor(
                        "target",
                        "entity-ref",
                        targetRefSerialized,
                        targetSummary),
                },
                Conditions = new[]
                {
                    SemanticDescriptorUtility.BuildWatchConditionDescriptor(
                        conditionType,
                        targetSummary,
                        ConditionWatchSemanticUtility.BuildParameterSummary(conditionType, parametersRaw),
                        parametersRaw,
                        timeoutSeconds,
                        repeat),
                },
            };
        }
    }
}
