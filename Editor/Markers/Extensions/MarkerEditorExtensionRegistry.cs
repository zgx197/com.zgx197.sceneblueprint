#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace SceneBlueprint.Editor.Markers.Extensions
{
    /// <summary>
    /// Marker 编辑器扩展注册表 — 自动发现并管理所有标注了 [MarkerEditorExtension] 的工具类。
    /// <para>
    /// 职责：
    /// - 反射扫描所有程序集，查找 [MarkerEditorExtension] 特性标注的类
    /// - 实例化并缓存这些扩展工具
    /// - 根据 Marker 类型 ID 查询适用的扩展列表
    /// </para>
    /// <para>
    /// 使用方式：
    /// - InitializeOnLoad 时自动调用 AutoDiscover()
    /// - SceneMarkerEditor 通过 GetExtensions(markerTypeId) 获取扩展列表
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class MarkerEditorExtensionRegistry
    {
        // 扩展工具缓存：MarkerTypeId → List<IMarkerEditorExtension>
        private static readonly Dictionary<string, List<IMarkerEditorExtension>> _extensionsByTypeId = new();

        // 所有扩展工具的实例（用于遍历）
        private static readonly List<IMarkerEditorExtension> _allExtensions = new();

        // 是否已完成自动发现
        private static bool _isDiscovered;

        static MarkerEditorExtensionRegistry()
        {
            // Unity 编辑器启动时自动发现所有扩展
            AutoDiscover();
        }

        /// <summary>
        /// 自动发现所有标注了 [MarkerEditorExtension] 的扩展工具。
        /// <para>
        /// 反射扫描所有程序集，查找实现 IMarkerEditorExtension 接口且标注了特性的类，
        /// 实例化并注册到内部缓存。
        /// </para>
        /// <para>
        /// 此方法在 InitializeOnLoad 时自动调用，通常无需手动调用。
        /// 如果业务层动态加载了新程序集，可手动调用刷新注册表。
        /// </para>
        /// </summary>
        public static void AutoDiscover()
        {
            if (_isDiscovered)
            {
                // 已发现过，清空重新扫描（支持热重载）
                _extensionsByTypeId.Clear();
                _allExtensions.Clear();
            }

            int discoveredCount = 0;

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    // 跳过系统程序集，提升性能
                    var assemblyName = assembly.GetName().Name ?? "";
                    if (assemblyName.StartsWith("System") ||
                        assemblyName.StartsWith("Unity") ||
                        assemblyName.StartsWith("mscorlib"))
                    {
                        continue;
                    }

                    try
                    {
                        var types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            // 检查是否标注了 [MarkerEditorExtension] 特性
                            var attr = type.GetCustomAttribute<MarkerEditorExtensionAttribute>(false);
                            if (attr == null)
                                continue;

                            // 检查是否实现了 IMarkerEditorExtension 接口
                            if (!typeof(IMarkerEditorExtension).IsAssignableFrom(type))
                            {
                                UnityEngine.Debug.LogWarning(
                                    $"[MarkerEditorExtensionRegistry] 类型 {type.FullName} 标注了 [MarkerEditorExtension]，" +
                                    $"但未实现 IMarkerEditorExtension 接口，已跳过。");
                                continue;
                            }

                            // 检查是否为抽象类
                            if (type.IsAbstract)
                            {
                                UnityEngine.Debug.LogWarning(
                                    $"[MarkerEditorExtensionRegistry] 类型 {type.FullName} 是抽象类，无法实例化，已跳过。");
                                continue;
                            }

                            // 尝试实例化（要求有无参构造函数）
                            IMarkerEditorExtension? extension = null;
                            try
                            {
                                extension = (IMarkerEditorExtension)Activator.CreateInstance(type);
                            }
                            catch (Exception e)
                            {
                                UnityEngine.Debug.LogError(
                                    $"[MarkerEditorExtensionRegistry] 无法实例化扩展类 {type.FullName}：{e.Message}\n" +
                                    $"请确保该类有公共无参构造函数。");
                                continue;
                            }

                            if (extension == null)
                                continue;

                            // 注册到缓存
                            var targetTypeId = attr.TargetMarkerTypeId;
                            if (!_extensionsByTypeId.ContainsKey(targetTypeId))
                                _extensionsByTypeId[targetTypeId] = new List<IMarkerEditorExtension>();

                            _extensionsByTypeId[targetTypeId].Add(extension);
                            _allExtensions.Add(extension);
                            discoveredCount++;
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // 某些程序集可能加载失败（如编辑器相关 DLL），跳过即可
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[MarkerEditorExtensionRegistry] 扫描程序集 {assemblyName} 时发生异常：{e.Message}");
                    }
                }

                _isDiscovered = true;

                if (discoveredCount > 0)
                {
                    UnityEngine.Debug.Log(
                        $"[MarkerEditorExtensionRegistry] AutoDiscover 完成，共发现 {discoveredCount} 个扩展工具。");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(
                    $"[MarkerEditorExtensionRegistry] AutoDiscover 失败：{e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 获取适用于指定 Marker 类型的所有扩展工具。
        /// </summary>
        /// <param name="markerTypeId">Marker 类型 ID（如 "PresetSpawnArea"）</param>
        /// <returns>扩展工具列表（如果没有匹配的扩展，返回空列表）</returns>
        public static List<IMarkerEditorExtension> GetExtensions(string markerTypeId)
        {
            if (string.IsNullOrEmpty(markerTypeId))
                return new List<IMarkerEditorExtension>();

            if (!_isDiscovered)
                AutoDiscover();

            return _extensionsByTypeId.TryGetValue(markerTypeId, out var list)
                ? list
                : new List<IMarkerEditorExtension>();
        }

        /// <summary>
        /// 获取所有已注册的扩展工具（用于调试）。
        /// </summary>
        public static IReadOnlyList<IMarkerEditorExtension> GetAllExtensions()
        {
            if (!_isDiscovered)
                AutoDiscover();

            return _allExtensions;
        }

        /// <summary>
        /// 获取所有已注册的 Marker 类型 ID（用于调试）。
        /// </summary>
        public static IReadOnlyCollection<string> GetRegisteredMarkerTypeIds()
        {
            if (!_isDiscovered)
                AutoDiscover();

            return _extensionsByTypeId.Keys;
        }

        /// <summary>
        /// 清空注册表并重新发现（用于测试或热重载）。
        /// </summary>
        [MenuItem("SceneBlueprint/调试/重新发现 Marker 扩展工具")]
        public static void Refresh()
        {
            _isDiscovered = false;
            AutoDiscover();
        }
    }
}
