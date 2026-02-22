#nullable enable
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Templates;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// Gizmo 样式常量——图层颜色、填充透明度、脉冲参数等。
    /// <para>
    /// 集中管理所有视觉常量，避免散落在各 Renderer 中。
    /// 修改颜色或动画参数只需改这一处。
    /// </para>
    /// <para>
    /// 覆盖优先级：GizmoStyleSO（如果存在）> C# 默认值。
    /// 无 SO 时行为与改造前完全一致。
    /// </para>
    /// </summary>
    public static class GizmoStyleConstants
    {
        static GizmoStyleConstants()
        {
            EditorApplication.projectChanged -= InvalidateCache;
            EditorApplication.projectChanged += InvalidateCache;
        }

        // ─── SO 覆盖层（延迟加载 + 缓存）───

        private static GizmoStyleSO? _override;
        private static bool _searched;

        /// <summary>查找项目中的 GizmoStyleSO（延迟加载，缓存结果）</summary>
        private static GizmoStyleSO? FindOverride()
        {
            if (!_searched)
            {
                _searched = true;
                var guids = AssetDatabase.FindAssets("t:GizmoStyleSO");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _override = AssetDatabase.LoadAssetAtPath<GizmoStyleSO>(path);
                }
            }
            return _override;
        }

        /// <summary>强制重新搜索 SO（SO 被创建/删除/修改后调用）</summary>
        public static void InvalidateCache()
        {
            _searched = false;
            _override = null;
        }

        /// <summary>当前是否有 SO 覆盖生效</summary>
        public static bool HasOverride => FindOverride() != null;

        /// <summary>获取当前加载的 SO 引用（可能为 null）</summary>
        public static GizmoStyleSO? Override => FindOverride();

        // ─── 图层颜色（SO 覆盖 > C# 默认值）───

        private static readonly Color _defaultCombatColor      = new(0.9f, 0.2f, 0.2f);
        private static readonly Color _defaultTriggerColor     = new(0.2f, 0.4f, 0.9f);
        private static readonly Color _defaultEnvironmentColor = new(0.9f, 0.8f, 0.2f);
        private static readonly Color _defaultCameraColor      = new(0.2f, 0.8f, 0.3f);
        private static readonly Color _defaultNarrativeColor   = new(0.7f, 0.3f, 0.9f);
        private static readonly Color _defaultColor            = new(0.7f, 0.7f, 0.7f);

        public static Color CombatColor      => FindOverride()?.CombatColor      ?? _defaultCombatColor;
        public static Color TriggerColor     => FindOverride()?.TriggerColor     ?? _defaultTriggerColor;
        public static Color EnvironmentColor => FindOverride()?.EnvironmentColor ?? _defaultEnvironmentColor;
        public static Color CameraColor      => FindOverride()?.CameraColor      ?? _defaultCameraColor;
        public static Color NarrativeColor   => FindOverride()?.NarrativeColor   ?? _defaultNarrativeColor;
        public static Color DefaultColor     => FindOverride()?.DefaultColor     ?? _defaultColor;

        // ─── 填充透明度 ───

        private const float _defaultFillAlpha = 0.15f;
        private const float _defaultSelectedFillAlpha = 0.25f;

        /// <summary>普通状态下填充面的 alpha</summary>
        public static float FillAlpha         => FindOverride()?.FillAlpha         ?? _defaultFillAlpha;

        /// <summary>选中状态下填充面的 alpha</summary>
        public static float SelectedFillAlpha => FindOverride()?.SelectedFillAlpha ?? _defaultSelectedFillAlpha;

        // ─── 脉冲动画参数 ───

        private const float _defaultPulseSpeed = 5f;
        private const float _defaultMaxPulseAmplitude = 0.3f;
        private const float _defaultPulseAlphaMin = 0.4f;
        private const float _defaultPulseAlphaMax = 1.0f;

        /// <summary>脉冲频率（越大越快）</summary>
        public static float PulseSpeed        => FindOverride()?.PulseSpeed        ?? _defaultPulseSpeed;

        /// <summary>脉冲缩放最大倍数（1.0 + MaxPulseAmplitude）</summary>
        public static float MaxPulseAmplitude => FindOverride()?.MaxPulseAmplitude ?? _defaultMaxPulseAmplitude;

        /// <summary>脉冲透明度最小值</summary>
        public static float PulseAlphaMin     => FindOverride()?.PulseAlphaMin     ?? _defaultPulseAlphaMin;

        /// <summary>脉冲透明度最大值</summary>
        public static float PulseAlphaMax     => FindOverride()?.PulseAlphaMax     ?? _defaultPulseAlphaMax;

        // ─── 拾取参数 ───

        private const float _defaultPickDistanceThreshold = 20f;

        /// <summary>鼠标距离阈值（像素），小于此值判定为命中</summary>
        public static float PickDistanceThreshold => FindOverride()?.PickDistanceThreshold ?? _defaultPickDistanceThreshold;

        // ─── 颜色工具方法 ───

        /// <summary>
        /// 根据标记获取 Gizmo 颜色。
        /// <para>
        /// 优先级：Marker 自定义颜色 > Annotation 覆盖色 > 默认颜色
        /// </para>
        /// </summary>
        public static Color GetLayerColor(SceneMarker marker)
        {
            // 1. Marker 自定义颜色（最高优先级）
            if (marker.UseCustomGizmoColor)
                return marker.CustomGizmoColor;

            // 2. Annotation 颜色覆盖（取第一个非 null 的）
            var annotations = MarkerCache.GetAnnotations(marker);
            for (int i = 0; i < annotations.Length; i++)
            {
                var colorOverride = annotations[i].GetGizmoColorOverride();
                if (colorOverride.HasValue)
                    return colorOverride.Value;
            }

            // 3. 默认颜色
            return DefaultColor;
        }

        /// <summary>将颜色加亮（用于高亮效果）</summary>
        public static Color GetHighlightColor(Color baseColor)
        {
            var bright = Color.Lerp(baseColor, Color.white, 0.5f);
            bright.a = 1f;
            return bright;
        }

        /// <summary>生成半透明填充色</summary>
        public static Color GetFillColor(Color baseColor, bool selected = false)
        {
            float alpha = selected ? SelectedFillAlpha : FillAlpha;
            return new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        /// <summary>计算当前帧的脉冲缩放值</summary>
        public static float CalcPulseScale(float time)
        {
            return 1f + MaxPulseAmplitude * (0.5f + 0.5f * Mathf.Sin(time * PulseSpeed));
        }

        /// <summary>计算当前帧的脉冲透明度值</summary>
        public static float CalcPulseAlpha(float time)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(time * PulseSpeed);
            return Mathf.Lerp(PulseAlphaMin, PulseAlphaMax, t);
        }
    }
}
