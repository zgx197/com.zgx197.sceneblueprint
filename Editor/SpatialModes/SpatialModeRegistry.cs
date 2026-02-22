#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SceneBlueprint.Adapters.Unity2D;
using SceneBlueprint.Adapters.Unity3D;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.SpatialAbstraction;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.SpatialModes
{
    /// <summary>
    /// 编辑器扩展的空间模式描述器。
    /// 在核心契约之上补充 SceneView 放置能力。
    /// </summary>
    public interface IEditorSpatialModeDescriptor : ISpatialModeDescriptor
    {
        bool TryGetSceneViewPlacement(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos);
    }

    /// <summary>
    /// 空间模式注册表。
    /// 通过反射自动发现并注册所有 ISpatialModeDescriptor 实现。
    /// </summary>
    public static class SpatialModeRegistry
    {
        private static readonly Dictionary<string, IEditorSpatialModeDescriptor> _descriptors =
            new Dictionary<string, IEditorSpatialModeDescriptor>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;

        public static IReadOnlyList<IEditorSpatialModeDescriptor> GetAll()
        {
            EnsureInitialized();
            return _descriptors.Values.ToList();
        }

        public static bool TryGet(string modeId, out IEditorSpatialModeDescriptor descriptor)
        {
            EnsureInitialized();
            return _descriptors.TryGetValue(modeId, out descriptor!);
        }

        public static IEditorSpatialModeDescriptor GetProjectModeDescriptor()
        {
            EnsureInitialized();

            string configuredModeId = SceneBlueprintProjectSettings.GetSpatialModeId();

            if (TryGet(configuredModeId, out var descriptor))
                return descriptor;

            if (_descriptors.Count > 0)
            {
                var fallback = _descriptors.Values.First();
                SBLog.Warn(SBLogTags.Registry,
                    $"未找到空间模式 '{configuredModeId}'，回退到 '{fallback.ModeId}'。");
                return fallback;
            }

            throw new InvalidOperationException("未发现任何 IEditorSpatialModeDescriptor 实现。请检查模式描述器注册。");
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            var descriptorType = typeof(IEditorSpatialModeDescriptor);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name ?? "";
                if (assemblyName.StartsWith("System", StringComparison.Ordinal)
                    || assemblyName.StartsWith("Microsoft", StringComparison.Ordinal)
                    || assemblyName.StartsWith("mscorlib", StringComparison.Ordinal)
                    || assemblyName.StartsWith("netstandard", StringComparison.Ordinal))
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
                    if (type == null || type.IsAbstract || type.IsInterface)
                        continue;
                    if (!descriptorType.IsAssignableFrom(type))
                        continue;

                    try
                    {
                        var descriptor = (IEditorSpatialModeDescriptor)Activator.CreateInstance(type)!;
                        if (string.IsNullOrWhiteSpace(descriptor.ModeId))
                            continue;

                        if (_descriptors.ContainsKey(descriptor.ModeId))
                        {
                            SBLog.Warn(SBLogTags.Registry,
                                $"空间模式 '{descriptor.ModeId}' 重复定义，已跳过: {type.FullName}");
                            continue;
                        }

                        _descriptors[descriptor.ModeId] = descriptor;
                    }
                    catch (Exception ex)
                    {
                        SBLog.Warn(SBLogTags.Registry,
                            $"空间模式描述器加载失败: {type.FullName}，{ex.Message}");
                    }
                }
            }

            SBLog.Info(SBLogTags.Registry, $"SpatialModeRegistry: 加载 {_descriptors.Count} 个空间模式");
        }
    }

    /// <summary>内置 Unity3D 空间模式描述器。</summary>
    public sealed class Unity3DSpatialModeDescriptor : IEditorSpatialModeDescriptor
    {
        public string ModeId => "Unity3D";
        public string DisplayName => "Unity 3D";
        public string AdapterType => Unity3DAdapterServices.AdapterType;
        public ISpatialBindingCodec BindingCodec => Unity3DAdapterServices.BindingCodec;

        public bool TryGetSceneViewPlacement(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            return Unity3DAdapterServices.TryGetSceneViewPlacement(mousePos, sceneView, out worldPos);
        }
    }

    /// <summary>内置 Unity2D 空间模式描述器。</summary>
    public sealed class Unity2DSpatialModeDescriptor : IEditorSpatialModeDescriptor
    {
        public string ModeId => "Unity2D";
        public string DisplayName => "Unity 2D";
        public string AdapterType => Unity2DAdapterServices.AdapterType;
        public ISpatialBindingCodec BindingCodec => Unity2DAdapterServices.BindingCodec;

        public bool TryGetSceneViewPlacement(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            return Unity2DAdapterServices.TryGetSceneViewPlacement(mousePos, sceneView, out worldPos);
        }
    }
}
