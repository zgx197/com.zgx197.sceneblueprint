#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Editor;
using UnityEditor;

namespace SceneBlueprint.Editor.Compilation
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ActionCompilationDebugRendererAttribute : Attribute
    {
        public ActionCompilationDebugRendererAttribute(int order = 0)
        {
            Order = order;
        }

        public int Order { get; }
    }

    public readonly struct ActionCompilationDebugRenderContext
    {
        public ActionCompilationDebugRenderContext(
            ActionInspectorSectionContext inspectorContext,
            ActionCompilationArtifact artifact)
        {
            InspectorContext = inspectorContext;
            Artifact = artifact;
        }

        public ActionInspectorSectionContext InspectorContext { get; }

        public ActionCompilationArtifact Artifact { get; }
    }

    public interface IActionCompilationDebugRenderer
    {
        /// <summary>
        /// 只用于补充 formal debug projection 之外的节点专用视图。
        /// 主协议解释应优先落在统一的 diagnostics / projection / observation stage 上。
        /// </summary>
        bool Supports(ActionCompilationDebugRenderContext context);

        void Draw(ActionCompilationDebugRenderContext context);
    }

    internal static class ActionCompilationDebugRendererRegistry
    {
        private static List<IActionCompilationDebugRenderer>? _renderers;

        public static bool TryResolve(
            ActionCompilationDebugRenderContext context,
            out IActionCompilationDebugRenderer renderer)
        {
            var renderers = _renderers ??= Discover();
            for (var index = 0; index < renderers.Count; index++)
            {
                var candidate = renderers[index];
                try
                {
                    if (candidate.Supports(context))
                    {
                        renderer = candidate;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ActionCompilationDebugRendererRegistry] Renderer Supports 失败: {candidate.GetType().FullName} - {ex.Message}");
                }
            }

            renderer = default!;
            return false;
        }

        private static List<IActionCompilationDebugRenderer> Discover()
        {
            var discovered = new List<(IActionCompilationDebugRenderer renderer, int order)>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IActionCompilationDebugRenderer>())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                try
                {
                    var renderer = (IActionCompilationDebugRenderer)Activator.CreateInstance(type)!;
                    var attribute = (ActionCompilationDebugRendererAttribute?)Attribute.GetCustomAttribute(
                        type,
                        typeof(ActionCompilationDebugRendererAttribute));
                    discovered.Add((renderer, attribute?.Order ?? 0));
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ActionCompilationDebugRendererRegistry] Renderer 实例化失败: {type.FullName} - {ex.Message}");
                }
            }

            discovered.Sort(static (left, right) =>
            {
                var orderCompare = left.order.CompareTo(right.order);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                return string.CompareOrdinal(
                    left.renderer.GetType().FullName,
                    right.renderer.GetType().FullName);
            });

            var result = new List<IActionCompilationDebugRenderer>(discovered.Count);
            for (var index = 0; index < discovered.Count; index++)
            {
                result.Add(discovered[index].renderer);
            }

            return result;
        }
    }
}
