#nullable enable
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// 单个标记的绘制上下文——每帧为每个可见标记预计算一次，
    /// 传递给 <see cref="IMarkerGizmoRenderer"/> 的各 Phase 方法。
    /// <para>
    /// 设计为 struct 避免 GC 分配。所有样式值（颜色、脉冲）在管线主循环中
    /// 预计算完毕，Renderer 无需重复计算。
    /// </para>
    /// </summary>
    public struct GizmoDrawContext
    {
        // ─── 标记引用 ───

        /// <summary>标记组件引用</summary>
        public SceneMarker Marker;

        /// <summary>标记的 Transform（缓存，避免重复 .transform 访问）</summary>
        public Transform Transform;

        // ─── 状态 ───

        /// <summary>是否在 Unity Selection 中被选中</summary>
        public bool IsSelected;

        /// <summary>是否被蓝图联动高亮</summary>
        public bool IsHighlighted;

        // ─── 预计算样式 ───

        /// <summary>图层基础颜色（由 Tag 前缀决定）</summary>
        public Color BaseColor;

        /// <summary>最终颜色（含高亮加亮处理）</summary>
        public Color EffectiveColor;

        /// <summary>半透明填充色（BaseColor 降低 alpha）</summary>
        public Color FillColor;

        /// <summary>脉冲缩放因子（1.0 = 无脉冲，高亮时 > 1.0）</summary>
        public float PulseScale;

        /// <summary>脉冲透明度因子（0~1，用于高亮呼吸效果）</summary>
        public float PulseAlpha;

        // ─── 工具值 ───

        /// <summary>HandleUtility.GetHandleSize 的缓存值（基于标记位置和摄像机距离）</summary>
        public float HandleSize;
    }
}
