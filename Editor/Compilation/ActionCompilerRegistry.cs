#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Compilation
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ActionCompilerAttribute : Attribute
    {
        public ActionCompilerAttribute(int order = 0)
        {
            Order = order;
        }

        public int Order { get; }
    }

    public interface IActionCompiler
    {
        bool Supports(ActionCompilationContext context);

        ActionCompilationArtifact Compile(ActionCompilationContext context);
    }

    public interface IActionCompilerRegistry
    {
        void Register(IActionCompiler compiler, int order = 0);

        bool TryResolve(ActionCompilationContext context, out IActionCompiler compiler);

        bool TryCompile(ActionCompilationContext context, out ActionCompilationArtifact artifact);
    }

    /// <summary>
    /// 框架级 action compiler 注册表。
    /// 第一版先提供自动发现、按 actionType 选择 compiler 和统一异常兜底。
    /// </summary>
    public sealed class ActionCompilerRegistry : IActionCompilerRegistry
    {
        private readonly List<CompilerRegistration> _registrations = new();
        private static ActionCompilerRegistry? _default;

        public static ActionCompilerRegistry Default => _default ??= Discover();

        public void Register(IActionCompiler compiler, int order = 0)
        {
            if (compiler == null)
            {
                throw new ArgumentNullException(nameof(compiler));
            }

            _registrations.Add(new CompilerRegistration(compiler, order, _registrations.Count));
            _registrations.Sort(CompilerRegistrationComparer.Instance);
        }

        public bool TryResolve(ActionCompilationContext context, out IActionCompiler compiler)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            for (var index = 0; index < _registrations.Count; index++)
            {
                var registration = _registrations[index];
                try
                {
                    if (registration.Compiler.Supports(context))
                    {
                        compiler = registration.Compiler;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ActionCompilerRegistry] Compiler Supports 失败: {registration.Compiler.GetType().FullName} - {ex.Message}");
                }
            }

            compiler = default!;
            return false;
        }

        public bool TryCompile(ActionCompilationContext context, out ActionCompilationArtifact artifact)
        {
            var definitionDiagnostics = ActionDefinitionValidationSupport.BuildDiagnostics(context);
            if (!TryResolve(context, out var compiler))
            {
                artifact = ActionCompilationArtifact.Empty(
                    context.ActionId,
                    context.ActionTypeId,
                    compilerId: string.Empty)
                    .WithAdditionalDiagnostics(definitionDiagnostics);
                return false;
            }

            var compilerId = compiler.GetType().FullName ?? compiler.GetType().Name;
            try
            {
                artifact = compiler.Compile(context)
                    ?? ActionCompilationArtifact.Empty(context.ActionId, context.ActionTypeId, compilerId);
                artifact = artifact.WithAdditionalDiagnostics(definitionDiagnostics);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ActionCompilerRegistry] Compiler 执行失败: {compilerId} - {ex.Message}");
                artifact = ActionCompilationArtifact.FromException(context, compilerId, ex)
                    .WithAdditionalDiagnostics(definitionDiagnostics);
                return true;
            }
        }

        private static ActionCompilerRegistry Discover()
        {
            var registry = new ActionCompilerRegistry();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IActionCompiler>())
            {
                if (type == null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                var attribute = (ActionCompilerAttribute?)Attribute.GetCustomAttribute(
                    type,
                    typeof(ActionCompilerAttribute));
                if (attribute == null)
                {
                    continue;
                }

                try
                {
                    var instance = (IActionCompiler)Activator.CreateInstance(type)!;
                    registry.Register(instance, attribute.Order);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ActionCompilerRegistry] Compiler 实例化失败: {type.FullName} - {ex.Message}");
                }
            }

            return registry;
        }

        private readonly struct CompilerRegistration
        {
            public CompilerRegistration(IActionCompiler compiler, int order, int registrationIndex)
            {
                Compiler = compiler;
                Order = order;
                RegistrationIndex = registrationIndex;
            }

            public IActionCompiler Compiler { get; }

            public int Order { get; }

            public int RegistrationIndex { get; }
        }

        private sealed class CompilerRegistrationComparer : IComparer<CompilerRegistration>
        {
            public static readonly CompilerRegistrationComparer Instance = new();

            public int Compare(CompilerRegistration x, CompilerRegistration y)
            {
                var orderCompare = x.Order.CompareTo(y.Order);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                return x.RegistrationIndex.CompareTo(y.RegistrationIndex);
            }
        }
    }
}
