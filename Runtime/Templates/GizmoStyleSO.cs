#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Templates
{
    /// <summary>
    /// Gizmo 视觉样式配置——策划可通过此 SO 调整 Scene View 中所有标记的视觉参数。
    /// <para>
    /// 项目中最多存在一个 GizmoStyleSO 资产。不存在时使用 C# 硬编码默认值，行为不变。
    /// </para>
    /// <para>
    /// 覆盖优先级：GizmoStyleSO（如果存在）> C# 默认值
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "GizmoStyle",
        menuName = "SceneBlueprint/Gizmo Style",
        order = 110)]
    public class GizmoStyleSO : ScriptableObject
    {
        // ─── 图层颜色覆盖 ───

        [Header("── 图层颜色 ──")]

        [Tooltip("战斗图层颜色")]
        public Color CombatColor = new(0.9f, 0.2f, 0.2f);

        [Tooltip("触发图层颜色")]
        public Color TriggerColor = new(0.2f, 0.4f, 0.9f);

        [Tooltip("环境图层颜色")]
        public Color EnvironmentColor = new(0.9f, 0.8f, 0.2f);

        [Tooltip("摄像机图层颜色")]
        public Color CameraColor = new(0.2f, 0.8f, 0.3f);

        [Tooltip("叙事图层颜色")]
        public Color NarrativeColor = new(0.7f, 0.3f, 0.9f);

        [Tooltip("默认颜色（无匹配图层时）")]
        public Color DefaultColor = new(0.7f, 0.7f, 0.7f);

        // ─── 填充透明度 ───

        [Header("── 填充透明度 ──")]

        [Tooltip("普通状态下填充面的 alpha")]
        [Range(0f, 1f)]
        public float FillAlpha = 0.15f;

        [Tooltip("选中状态下填充面的 alpha")]
        [Range(0f, 1f)]
        public float SelectedFillAlpha = 0.25f;

        // ─── 脉冲动画 ───

        [Header("── 脉冲动画 ──")]

        [Tooltip("脉冲频率（越大越快）")]
        [Range(0.5f, 20f)]
        public float PulseSpeed = 5f;

        [Tooltip("脉冲缩放最大倍数（1.0 + 此值）")]
        [Range(0f, 1f)]
        public float MaxPulseAmplitude = 0.3f;

        [Tooltip("脉冲透明度最小值")]
        [Range(0f, 1f)]
        public float PulseAlphaMin = 0.4f;

        [Tooltip("脉冲透明度最大值")]
        [Range(0f, 1f)]
        public float PulseAlphaMax = 1.0f;

        // ─── 拾取参数 ───

        [Header("── 拾取参数 ──")]

        [Tooltip("鼠标距离阈值（像素），小于此值判定为命中")]
        [Range(5f, 50f)]
        public float PickDistanceThreshold = 20f;

        // ─── 标签 ───

        [Header("── 标签显示 ──")]

        [Tooltip("是否显示标记标签")]
        public bool ShowLabels = true;

        [Tooltip("标签字体大小")]
        [Range(8f, 24f)]
        public float LabelFontSize = 12f;

        [Tooltip("标签背景透明度")]
        [Range(0f, 1f)]
        public float LabelBackgroundAlpha = 0.7f;

        // ─── Point 标记 ───

        [Header("── Point 标记 ──")]

        [Tooltip("点位标记颜色（无图层匹配时使用）")]
        public Color PointColor = new(0.2f, 0.8f, 1f);

        [Tooltip("点位球体半径")]
        [Range(0.1f, 5f)]
        public float PointRadius = 0.5f;

        [Tooltip("方向箭头长度")]
        [Range(0.5f, 5f)]
        public float PointArrowLength = 1.2f;

        [Tooltip("是否显示方向箭头")]
        public bool PointShowDirection = true;

        // ─── Area 标记 ───

        [Header("── Area 标记 ──")]

        [Tooltip("区域标记颜色（无图层匹配时使用）")]
        public Color AreaColor = new(1f, 0.85f, 0.2f);

        [Tooltip("区域填充透明度")]
        [Range(0f, 1f)]
        public float AreaFillAlpha = 0.15f;

        [Tooltip("区域线框透明度")]
        [Range(0f, 1f)]
        public float AreaWireframeAlpha = 0.8f;

        [Tooltip("区域高度线透明度")]
        [Range(0f, 1f)]
        public float AreaHeightLineAlpha = 0.3f;

        // ─── Entity 标记 ───

        [Header("── Entity 标记 ──")]

        [Tooltip("实体标记颜色（无图层匹配时使用）")]
        public Color EntityColor = new(0.4f, 1f, 0.4f);

        [Tooltip("实体图标大小")]
        [Range(0.2f, 3f)]
        public float EntityIconSize = 0.8f;

        // ─── 高亮效果 ───

        [Header("── 高亮效果 ──")]

        [Tooltip("高亮混合色")]
        public Color HighlightColor = new(1f, 0.95f, 0.3f);

        [Tooltip("高亮脉冲速度")]
        [Range(0.5f, 10f)]
        public float HighlightPulseSpeed = 3f;

        [Tooltip("高亮脉冲最小亮度")]
        [Range(0f, 1f)]
        public float HighlightPulseMin = 0.5f;

        [Tooltip("高亮脉冲最大亮度")]
        [Range(0f, 1f)]
        public float HighlightPulseMax = 1f;

        // ─── 选中效果 ───

        [Header("── 选中效果 ──")]

        [Tooltip("选中轮廓颜色")]
        public Color SelectionOutlineColor = Color.white;

        [Tooltip("选中轮廓宽度")]
        [Range(1f, 5f)]
        public float SelectionOutlineWidth = 2f;
    }
}
