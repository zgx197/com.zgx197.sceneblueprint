#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SceneBlueprint.Editor.Logging;

namespace SceneBlueprint.Editor.Markers.Definitions
{
    /// <summary>
    /// 标记定义注册表——管理所有 <see cref="MarkerDefinition"/> 的注册和查找。
    /// <para>
    /// 通过 <see cref="AutoDiscover"/> 自动扫描所有标注了 <see cref="MarkerDefAttribute"/>
    /// 的 <see cref="IMarkerDefinitionProvider"/> 实现类，调用 Define() 获取定义并注册。
    /// </para>
    /// <para>
    /// 与 <see cref="Core.ActionRegistry"/> 对称的设计。
    /// </para>
    /// </summary>
    public static class MarkerDefinitionRegistry
    {
        private static readonly Dictionary<string, MarkerDefinition> _definitions = new();
        private static bool _discovered;

        /// <summary>已注册的定义数量</summary>
        public static int Count => _definitions.Count;

        /// <summary>
        /// 确保已执行自动发现（幂等）。
        /// </summary>
        public static void EnsureDiscovered()
        {
            if (!_discovered)
                AutoDiscover();
        }

        /// <summary>
        /// 通过 TypeId 获取标记定义。未找到时返回 null。
        /// </summary>
        public static MarkerDefinition? Get(string typeId)
        {
            EnsureDiscovered();
            return _definitions.TryGetValue(typeId, out var def) ? def : null;
        }

        /// <summary>获取所有已注册的标记定义</summary>
        public static IReadOnlyList<MarkerDefinition> GetAll()
        {
            EnsureDiscovered();
            return _definitions.Values.ToList();
        }

        /// <summary>手动注册一个标记定义（TypeId 重复时覆盖）</summary>
        public static void Register(MarkerDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.TypeId))
                throw new ArgumentException("MarkerDefinition.TypeId 不能为空");

            _definitions[definition.TypeId] = definition;
        }

        /// <summary>
        /// 通过反射自动发现并注册所有标记定义。
        /// <para>
        /// 扫描当前 AppDomain 中所有程序集，找到实现了 <see cref="IMarkerDefinitionProvider"/>
        /// 且标注了 <see cref="MarkerDefAttribute"/> 的类，实例化并调用 Define()。
        /// </para>
        /// </summary>
        public static void AutoDiscover()
        {
            _definitions.Clear();
            _discovered = true;

            var providerType = typeof(IMarkerDefinitionProvider);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name ?? "";
                if (assemblyName.StartsWith("System") || assemblyName.StartsWith("Microsoft")
                    || assemblyName.StartsWith("mscorlib") || assemblyName.StartsWith("netstandard")
                    || assemblyName.StartsWith("Unity.") || assemblyName.StartsWith("UnityEngine.")
                    || assemblyName.StartsWith("UnityEditor."))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface) continue;
                    if (!providerType.IsAssignableFrom(type)) continue;

                    var attr = type.GetCustomAttribute<MarkerDefAttribute>();
                    if (attr == null) continue;

                    if (_definitions.ContainsKey(attr.TypeId)) continue;

                    try
                    {
                        var provider = (IMarkerDefinitionProvider)Activator.CreateInstance(type);
                        var def = provider.Define();
                        Register(def);
                    }
                    catch (Exception ex)
                    {
                        SBLog.Warn(SBLogTags.Marker,
                            $"AutoDiscover 跳过 {type.Name}: {ex.Message}");
                    }
                }
            }

            if (_definitions.Count > 0)
            {
                var names = string.Join(", ", _definitions.Values.Select(d => d.TypeId));
                SBLog.Info(SBLogTags.Marker,
                    $"已注册 {_definitions.Count} 个标记定义: {names}");
            }
        }

        /// <summary>强制重新发现（编辑器热重载时调用）</summary>
        public static void Refresh()
        {
            _discovered = false;
            EnsureDiscovered();
        }
    }
}
