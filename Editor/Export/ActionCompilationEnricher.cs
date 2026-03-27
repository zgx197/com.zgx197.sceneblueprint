#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor.Compilation;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// 框架级 action 编译接缝。
    /// 第一版统一在导出阶段执行已注册 compiler，并汇总 transport metadata 与 diagnostics。
    /// </summary>
    [ExportEnricher(Order = 120)]
    public sealed class ActionCompilationEnricher : IExportEnricher
    {
        public void Enrich(ExportContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var actions = context.Data.Actions;
            if (actions == null || actions.Length == 0)
            {
                context.ClearTransportMetadata();
                return;
            }

            var transportEntries = new List<PropertyValue>();
            var tickRate = ResolveTargetTickRate(context);
            var registry = ActionCompilerRegistry.Default;

            for (var index = 0; index < actions.Length; index++)
            {
                var action = actions[index];
                if (action == null)
                {
                    continue;
                }

                var compilationContext = ActionCompilationContext.ForExport(context, action, tickRate);
                if (!registry.TryCompile(compilationContext, out var artifact))
                {
                    var definitionDiagnostics = ActionDefinitionValidationSupport.BuildDiagnostics(compilationContext);
                    for (var diagnosticIndex = 0; diagnosticIndex < definitionDiagnostics.Length; diagnosticIndex++)
                    {
                        context.Messages.Add(definitionDiagnostics[diagnosticIndex].ToValidationMessage());
                    }

                    continue;
                }

                if (artifact.HasTransportEntries)
                {
                    transportEntries.AddRange(artifact.TransportEntries);
                }

                var diagnostics = artifact.Diagnostics;
                for (var diagnosticIndex = 0; diagnosticIndex < diagnostics.Length; diagnosticIndex++)
                {
                    context.Messages.Add(diagnostics[diagnosticIndex].ToValidationMessage());
                }
            }

            context.ReplaceTransportMetadata(transportEntries.ToArray());
        }

        private static float ResolveTargetTickRate(ExportContext context)
        {
            if (context.TryGetUserData<float>("sceneBlueprint.targetTickRate", out var targetTickRate)
                && targetTickRate > 0f)
            {
                return targetTickRate;
            }

            return BlueprintRuntimeSettings.Instance.TargetTickRate;
        }
    }
}
