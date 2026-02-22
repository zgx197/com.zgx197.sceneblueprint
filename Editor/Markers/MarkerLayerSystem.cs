#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// 标记图层定义——描述一个图层的名称、颜色、Tag 前缀。
    /// </summary>
    public readonly struct MarkerLayer
    {
        public readonly string Id;           // 图层标识（= Tag 前缀，如 "Combat"）
        public readonly string DisplayName;  // 中文显示名
        public readonly Color Color;         // 代表色
        public readonly string Emoji;        // 面板图标（Unicode）

        public MarkerLayer(string id, string displayName, Color color, string emoji)
        {
            Id = id;
            DisplayName = displayName;
            Color = color;
            Emoji = emoji;
        }
    }

    /// <summary>
    /// 标记图层系统——管理图层定义与可见性状态。
    /// <para>
    /// 静态单例，由 <see cref="MarkerGizmoDrawer"/> 在绘制时查询可见性，
    /// 由 <see cref="MarkerLayerOverlay"/> 提供 UI 控制。
    /// </para>
    /// <para>
    /// 图层由 Tag 前缀自动映射。未分类的标记默认可见。
    /// </para>
    /// </summary>
    public static class MarkerLayerSystem
    {
        // ─── 预定义图层 ───

        public static readonly MarkerLayer[] Layers = new[]
        {
            new MarkerLayer("Preview", "预览", new Color(0.3f, 1.0f, 0.3f), "●"),
        };

        // ─── 可见性状态（按图层 Id 索引） ───

        private static readonly Dictionary<string, bool> _visibility = new();
        private static string _tagFilterExpression = "";
        private static bool _initialized;

        /// <summary>当前 Tag 过滤表达式（为空表示不过滤）。</summary>
        public static string TagFilterExpression => _tagFilterExpression;

        /// <summary>是否启用了 Tag 表达式过滤。</summary>
        public static bool HasTagFilter => !string.IsNullOrWhiteSpace(_tagFilterExpression);

        /// <summary>确保状态已初始化（所有图层默认可见）</summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            foreach (var layer in Layers)
            {
                _visibility[layer.Id] = true;
            }
            // 未归类标记默认可见
            _visibility[""] = true;
            _initialized = true;
        }

        /// <summary>查询某个图层是否可见</summary>
        /// <param name="layerId">图层 Id（= Tag 前缀），空字符串表示未归类</param>
        public static bool IsLayerVisible(string layerId)
        {
            EnsureInitialized();
            if (_visibility.TryGetValue(layerId, out bool v)) return v;
            return true; // 未注册的图层默认可见
        }

        /// <summary>
        /// 设置 Tag 过滤表达式。
        /// 支持逗号/分号/竖线分隔多个模式，任一命中即可见。
        /// </summary>
        public static void SetTagFilterExpression(string expression)
        {
            var normalized = string.IsNullOrWhiteSpace(expression)
                ? ""
                : expression.Trim();

            if (string.Equals(_tagFilterExpression, normalized, StringComparison.Ordinal))
                return;

            _tagFilterExpression = normalized;
            UnityEditor.SceneView.RepaintAll();
        }

        /// <summary>清空 Tag 过滤表达式。</summary>
        public static void ClearTagFilterExpression()
        {
            SetTagFilterExpression("");
        }

        /// <summary>设置某个图层的可见性</summary>
        public static void SetLayerVisible(string layerId, bool visible)
        {
            EnsureInitialized();
            _visibility[layerId] = visible;

            // 通知 Scene View 重绘
            UnityEditor.SceneView.RepaintAll();
        }

        /// <summary>
        /// 根据图层开关 + Tag 条件判断标记是否可见。
        /// 供 <see cref="MarkerGizmoDrawer"/> 调用。
        /// </summary>
        public static bool IsMarkerVisible(string tagPrefix, string fullTag)
        {
            if (!IsLayerVisible(tagPrefix))
                return false;

            return !HasTagFilter || TagExpressionMatcher.Evaluate(_tagFilterExpression, fullTag);
        }

        /// <summary>设置所有图层为可见</summary>
        public static void ShowAll()
        {
            EnsureInitialized();
            var keys = new List<string>(_visibility.Keys);
            foreach (var key in keys)
                _visibility[key] = true;
            UnityEditor.SceneView.RepaintAll();
        }

        /// <summary>设置所有图层为隐藏</summary>
        public static void HideAll()
        {
            EnsureInitialized();
            var keys = new List<string>(_visibility.Keys);
            foreach (var key in keys)
                _visibility[key] = false;
            UnityEditor.SceneView.RepaintAll();
        }

        /// <summary>查询预览图层是否可见</summary>
        public static bool IsPreviewVisible() => IsLayerVisible("Preview");
    }
}
