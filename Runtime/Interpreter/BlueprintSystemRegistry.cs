#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图 System 全局注册表——收集所有 IBlueprintSystemProvider 的注册。
    /// <para>
    /// 用户层在 [RuntimeInitializeOnLoadMethod] 中调用 Register()，
    /// BlueprintRunnerFactory 在创建 Runner 时调用 RegisterSystemsTo() 批量注入。
    /// </para>
    /// </summary>
    public static class BlueprintSystemRegistry
    {
        private static readonly List<IBlueprintSystemProvider> _providers = new();
        private static readonly HashSet<Type> _providerTypes = new();

        /// <summary>注册一个 System 提供者（由用户层在 [RuntimeInitializeOnLoadMethod] 中调用）</summary>
        public static void Register(IBlueprintSystemProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var providerType = provider.GetType();
            if (!_providerTypes.Add(providerType))
            {
                return;
            }

            _providers.Add(provider);
        }

        /// <summary>将所有已注册 Provider 的 System 批量注入到指定 Runner</summary>
        public static void RegisterSystemsTo(BlueprintRunner runner)
        {
            foreach (var provider in _providers)
                foreach (var system in provider.CreateSystems())
                    runner.RegisterSystem(system);
        }

        /// <summary>返回当前已注册 Provider 的类型名快照，供 Editor 诊断使用。</summary>
        public static IReadOnlyList<string> GetRegisteredProviderTypeNames()
        {
            var names = new List<string>(_providers.Count);
            for (var index = 0; index < _providers.Count; index++)
            {
                names.Add(_providers[index].GetType().FullName ?? _providers[index].GetType().Name);
            }

            return new ReadOnlyCollection<string>(names);
        }

        /// <summary>
        /// 预览当前 Provider 将注册哪些 System 名称。
        /// <para>
        /// 该方法仅用于 Editor 诊断，会重新枚举一次 Provider.CreateSystems()。
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> PreviewRegisteredSystemNames()
        {
            var names = new List<string>();
            foreach (var provider in _providers)
            {
                foreach (var system in provider.CreateSystems())
                {
                    if (system == null)
                    {
                        continue;
                    }

                    names.Add(system.Name);
                }
            }

            return new ReadOnlyCollection<string>(names);
        }

        /// <summary>清除注册表（主要用于单元测试场景）</summary>
        public static void Clear()
        {
            _providers.Clear();
            _providerTypes.Clear();
        }
    }
}
