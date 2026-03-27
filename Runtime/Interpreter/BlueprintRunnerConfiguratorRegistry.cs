#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// BlueprintRunner 配置器全局注册表。
    /// 与 BlueprintSystemRegistry 类似，用于收集默认 runtime 的业务接线器。
    /// </summary>
    internal static class BlueprintRunnerConfiguratorRegistry
    {
        private static readonly List<RegisteredConfigurator> _configurators = new();
        private static readonly HashSet<Type> _configuratorTypes = new();

        public static void RegisterFrameworkDefault(IBlueprintRunnerConfigurator configurator)
        {
            Register(configurator, BlueprintRunnerConfiguratorScope.FrameworkDefault);
        }

        public static void RegisterProjectBaseline(IBlueprintRunnerConfigurator configurator)
        {
            Register(configurator, BlueprintRunnerConfiguratorScope.ProjectBaseline);
        }

        public static void ConfigureDefaultScopes(BlueprintRunner runner)
        {
            Configure(
                runner,
                BlueprintRunnerConfiguratorScope.FrameworkDefault,
                BlueprintRunnerConfiguratorScope.ProjectBaseline);
        }

        public static void ConfigureFrameworkDefault(BlueprintRunner runner)
        {
            Configure(runner, BlueprintRunnerConfiguratorScope.FrameworkDefault);
        }

        public static void ConfigureProjectBaseline(BlueprintRunner runner)
        {
            Configure(runner, BlueprintRunnerConfiguratorScope.ProjectBaseline);
        }

        public static IReadOnlyList<string> GetRegisteredFrameworkDefaultConfiguratorTypeNames()
        {
            return GetRegisteredConfiguratorTypeNames(BlueprintRunnerConfiguratorScope.FrameworkDefault);
        }

        public static IReadOnlyList<string> GetRegisteredProjectBaselineConfiguratorTypeNames()
        {
            return GetRegisteredConfiguratorTypeNames(BlueprintRunnerConfiguratorScope.ProjectBaseline);
        }

        public static void Register(
            IBlueprintRunnerConfigurator configurator,
            BlueprintRunnerConfiguratorScope scope)
        {
            if (configurator == null)
            {
                throw new ArgumentNullException(nameof(configurator));
            }

            var configuratorType = configurator.GetType();
            if (!_configuratorTypes.Add(configuratorType))
            {
                return;
            }

            _configurators.Add(new RegisteredConfigurator(configurator, scope));
        }

        public static void Configure(
            BlueprintRunner runner,
            params BlueprintRunnerConfiguratorScope[] scopes)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            if (scopes == null || scopes.Length == 0)
            {
                return;
            }

            for (var index = 0; index < _configurators.Count; index++)
            {
                var entry = _configurators[index];
                if (!ContainsScope(scopes, entry.Scope))
                {
                    continue;
                }

                entry.Configurator.Configure(runner);
            }
        }

        public static IReadOnlyList<string> GetRegisteredConfiguratorTypeNames(
            BlueprintRunnerConfiguratorScope? scope)
        {
            var names = new List<string>(_configurators.Count);
            for (var index = 0; index < _configurators.Count; index++)
            {
                var entry = _configurators[index];
                if (scope.HasValue && entry.Scope != scope.Value)
                {
                    continue;
                }

                var configuratorType = entry.Configurator.GetType();
                names.Add(configuratorType.FullName ?? configuratorType.Name);
            }

            return new ReadOnlyCollection<string>(names);
        }

        public static void Clear()
        {
            _configurators.Clear();
            _configuratorTypes.Clear();
        }

        private static bool ContainsScope(
            IReadOnlyList<BlueprintRunnerConfiguratorScope> scopes,
            BlueprintRunnerConfiguratorScope scope)
        {
            for (var index = 0; index < scopes.Count; index++)
            {
                if (scopes[index] == scope)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct RegisteredConfigurator
        {
            public RegisteredConfigurator(
                IBlueprintRunnerConfigurator configurator,
                BlueprintRunnerConfiguratorScope scope)
            {
                Configurator = configurator;
                Scope = scope;
            }

            public IBlueprintRunnerConfigurator Configurator { get; }

            public BlueprintRunnerConfiguratorScope Scope { get; }
        }
    }
}
