#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// BlueprintRunner 的 ServiceLocator 扩展——极简服务容器。
    /// <para>
    /// 用于注入运行时服务（如 IEntityRegistry, ISpawnHandler 等），
    /// 替代直接在 System 上设置 Handler 属性的模式。
    /// 信号双向通信统一使用 <see cref="BlueprintRunner.Bridge"/>（ISignalBridge）。
    /// </para>
    /// <para>
    /// 使用示例：
    /// <code>
    /// runner.RegisterService&lt;IEntityRegistry&gt;(new DefaultEntityRegistry());
    /// // System 内部通过 frame.Runner.GetService&lt;IEntityRegistry&gt;() 获取
    /// // 信号通信通过 runner.Bridge.Listen/Send 操作
    /// </code>
    /// </para>
    /// </summary>
    public partial class BlueprintRunner
    {
        private readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// 注册服务实例。同一类型重复注册时覆盖旧实例。
        /// </summary>
        public void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 获取服务实例（可能为 null）。
        /// </summary>
        public T? GetService<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var service) ? (T)service : null;
        }

        /// <summary>
        /// 获取服务实例（不存在则抛出异常）。
        /// </summary>
        /// <exception cref="InvalidOperationException">服务未注册时抛出</exception>
        public T RequireService<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;
            throw new InvalidOperationException(
                $"[BlueprintRunner] 未注册服务: {typeof(T).Name}。请在 Load() 前调用 RegisterService<{typeof(T).Name}>()。");
        }

        /// <summary>
        /// 清除所有已注册的服务（Shutdown 时调用）。
        /// </summary>
        private void ClearServices()
        {
            foreach (var service in _services.Values)
            {
                if (service is IDisposable disposable)
                    disposable.Dispose();
            }
            _services.Clear();
        }
    }
}
