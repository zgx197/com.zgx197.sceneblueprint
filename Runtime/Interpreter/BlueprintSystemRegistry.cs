#nullable enable
using System.Collections.Generic;

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

        /// <summary>注册一个 System 提供者（由用户层在 [RuntimeInitializeOnLoadMethod] 中调用）</summary>
        public static void Register(IBlueprintSystemProvider provider)
            => _providers.Add(provider);

        /// <summary>将所有已注册 Provider 的 System 批量注入到指定 Runner</summary>
        public static void RegisterSystemsTo(BlueprintRunner runner)
        {
            foreach (var provider in _providers)
                foreach (var system in provider.CreateSystems())
                    runner.RegisterSystem(system);
        }

        /// <summary>清除注册表（主要用于单元测试场景）</summary>
        public static void Clear() => _providers.Clear();
    }
}
