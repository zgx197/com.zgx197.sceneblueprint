#nullable enable
using System;
using System.Reflection;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Editor.Compilation
{
    /// <summary>
    /// 第一版正式编译结果包装。
    /// 先把 loose object 收成统一 result slot，同时兼容既有 payload 直传。
    /// </summary>
    public sealed class ActionCompilationSemanticResult
    {
        public ActionCompilationSemanticResult(object? payload, SemanticDescriptorSet? semantics = null)
        {
            Payload = payload;
            Semantics = semantics ?? ActionCompilationResultSemanticExtractor.TryExtract(payload) ?? new SemanticDescriptorSet();
        }

        public object? Payload { get; }

        public SemanticDescriptorSet Semantics { get; }
    }

    public sealed class ActionCompilationPlanResult
    {
        public ActionCompilationPlanResult(object? payload, SemanticDescriptorSet? semantics = null)
        {
            Payload = payload;
            Semantics = semantics ?? ActionCompilationResultSemanticExtractor.TryExtract(payload) ?? new SemanticDescriptorSet();
        }

        public object? Payload { get; }

        public SemanticDescriptorSet Semantics { get; }
    }

    public sealed class ActionCompilationDebugProjection
    {
        public ActionCompilationDebugProjection(
            object? payload,
            SemanticDescriptorSet? semantics = null,
            DebugProjectionModel? model = null)
        {
            Payload = payload;
            Semantics = semantics ?? ActionCompilationResultSemanticExtractor.TryExtract(payload) ?? new SemanticDescriptorSet();
            Model = model ?? DebugProjectionModelFactory.CreateDefault(payload, Semantics, diagnostics: null, metadataEntries: null);
        }

        public object? Payload { get; }

        public SemanticDescriptorSet Semantics { get; }

        public DebugProjectionModel Model { get; }
    }

    internal static class ActionCompilationResultSemanticExtractor
    {
        public static SemanticDescriptorSet? TryExtract(object? payload)
        {
            if (payload == null)
            {
                return null;
            }

            if (payload is SemanticDescriptorSet descriptorSet)
            {
                return descriptorSet;
            }

            var property = payload.GetType().GetProperty(
                "Semantics",
                BindingFlags.Instance | BindingFlags.Public);
            if (property == null
                || !typeof(SemanticDescriptorSet).IsAssignableFrom(property.PropertyType))
            {
                return null;
            }

            try
            {
                return property.GetValue(payload) as SemanticDescriptorSet;
            }
            catch
            {
                return null;
            }
        }
    }
}
