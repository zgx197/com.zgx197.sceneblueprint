#nullable enable
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// Gizmo 标签绘制工具——提供统一的标签样式和绘制方法。
    /// <para>
    /// 所有 Renderer 通过此工具类绘制标签，确保风格一致。
    /// </para>
    /// </summary>
    public static class GizmoLabelUtil
    {
        private static GUIStyle? _labelStyle;

        /// <summary>获取或创建标准标签样式（缓存复用）</summary>
        private static GUIStyle GetLabelStyle()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    richText = true,
                };
            }
            return _labelStyle;
        }

        /// <summary>
        /// 绘制标准标记标签（名称 + Tag）。
        /// </summary>
        /// <param name="marker">标记组件</param>
        /// <param name="position">标签世界坐标位置</param>
        /// <param name="color">文字颜色</param>
        public static void DrawStandardLabel(SceneMarker marker, Vector3 position, Color color)
        {
            string text = marker.GetDisplayLabel();
            if (!string.IsNullOrEmpty(marker.Tag))
                text += $"\n<size=8>[{marker.Tag}]</size>";

            var style = GetLabelStyle();
            style.normal.textColor = color;
            Handles.Label(position, text, style);
        }

        /// <summary>
        /// 绘制自定义文字标签。
        /// </summary>
        public static void DrawCustomLabel(string text, Vector3 position, Color color, int fontSize = 10)
        {
            var style = GetLabelStyle();
            style.fontSize = fontSize;
            style.normal.textColor = color;
            Handles.Label(position, text, style);
            style.fontSize = 10; // 恢复默认
        }
    }
}
