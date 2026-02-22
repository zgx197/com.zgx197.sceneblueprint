#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;
using Object = UnityEngine.Object;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// 场景标记缓存——避免每帧 FindObjectsOfType。
    /// <para>
    /// 监听 <see cref="EditorApplication.hierarchyChanged"/> 自动刷新。
    /// 按 Component 类型分桶，支持快速按类型查询。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class MarkerCache
    {
        private static readonly List<SceneMarker> _all = new();
        private static readonly Dictionary<Type, List<SceneMarker>> _byType = new();
        private static readonly Dictionary<SceneMarker, MarkerAnnotation[]> _annotationCache = new();
        private static bool _dirty = true;

        static MarkerCache()
        {
            EditorApplication.hierarchyChanged += MarkDirty;
        }

        /// <summary>获取所有场景标记（自动刷新缓存）</summary>
        public static IReadOnlyList<SceneMarker> GetAll()
        {
            EnsureFresh();
            return _all;
        }

        /// <summary>获取指定类型的标记列表</summary>
        public static IReadOnlyList<SceneMarker> GetByType<T>() where T : SceneMarker
        {
            EnsureFresh();
            if (_byType.TryGetValue(typeof(T), out var list))
                return list;
            return Array.Empty<SceneMarker>();
        }

        /// <summary>
        /// 获取 Marker 上的所有 MarkerAnnotation（从缓存读取，零 GC）。
        /// <para>缓存随 hierarchyChanged 自动失效，与标记缓存同步刷新。</para>
        /// </summary>
        public static MarkerAnnotation[] GetAnnotations(SceneMarker marker)
        {
            EnsureFresh();
            return _annotationCache.TryGetValue(marker, out var annotations)
                ? annotations
                : Array.Empty<MarkerAnnotation>();
        }

        /// <summary>当前缓存的标记总数</summary>
        public static int Count
        {
            get
            {
                EnsureFresh();
                return _all.Count;
            }
        }

        /// <summary>手动标记缓存过期（标记属性变化时调用）</summary>
        public static void SetDirty() => _dirty = true;

        private static void MarkDirty() => _dirty = true;

        private static void EnsureFresh()
        {
            if (!_dirty) return;
            _dirty = false;

            _all.Clear();
            _byType.Clear();
            _annotationCache.Clear();

            var markers = Object.FindObjectsOfType<SceneMarker>();
            foreach (var m in markers)
            {
                if (m == null) continue;
                _all.Add(m);

                var type = m.GetType();
                if (!_byType.TryGetValue(type, out var list))
                {
                    list = new List<SceneMarker>();
                    _byType[type] = list;
                }
                list.Add(m);

                // 缓存 Annotation 组件
                var annotations = m.GetComponents<MarkerAnnotation>();
                if (annotations.Length > 0)
                    _annotationCache[m] = annotations;
            }
        }
    }
}
