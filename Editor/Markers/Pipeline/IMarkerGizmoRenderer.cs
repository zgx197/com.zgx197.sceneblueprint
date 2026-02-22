#nullable enable
using System;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// 标记 Gizmo 渲染器接口。
    /// <para>
    /// 每种标记类型（Point/Area/Entity/自定义）实现一个 Renderer，
    /// 由 <see cref="GizmoRenderPipeline"/> 按 <see cref="DrawPhase"/> 顺序调用。
    /// </para>
    /// <para>
    /// 所有 Phase 方法使用 C# 默认接口方法（空实现），
    /// Renderer 只需覆写自己需要的 Phase。
    /// </para>
    /// </summary>
    public interface IMarkerGizmoRenderer
    {
        /// <summary>支持的标记 Component 类型（如 typeof(PointMarker)）</summary>
        Type TargetType { get; }

        /// <summary>Phase 0: 绘制半透明填充面</summary>
        void DrawFill(in GizmoDrawContext ctx) { }

        /// <summary>Phase 1: 绘制线框、边框、方向箭头</summary>
        void DrawWireframe(in GizmoDrawContext ctx) { }

        /// <summary>Phase 2: 绘制图标图形（球体、菱形等）</summary>
        void DrawIcon(in GizmoDrawContext ctx) { }

        /// <summary>
        /// Phase 3: 选中时绘制交互编辑 Handle（拖拽顶点、Box Handle 等）。
        /// <para>仅在 <c>ctx.IsSelected == true</c> 时由管线调用。</para>
        /// <para>此方法只负责绘制编辑 Handle，不影响其他 Phase 的执行。
        /// Fill/Wireframe 始终由管线独立执行。</para>
        /// </summary>
        void DrawInteractive(in GizmoDrawContext ctx) { }

        /// <summary>Phase 4: 绘制高亮效果（脉冲光晕等，仅 IsHighlighted 时调用）</summary>
        void DrawHighlight(in GizmoDrawContext ctx) { }

        /// <summary>Phase 5: 绘制文字标签</summary>
        void DrawLabel(in GizmoDrawContext ctx) { }

        /// <summary>Phase 6: 返回拾取区域信息，用于 Scene View 点击检测</summary>
        PickBounds GetPickBounds(in GizmoDrawContext ctx);
    }
}
