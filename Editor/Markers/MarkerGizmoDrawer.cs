#nullable enable
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;
using UnityEngine;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// [已废弃] 旧版 Gizmo 绘制器——保留颜色查询的向后兼容。
    /// <para>
    /// 所有绘制逻辑已迁移到 <see cref="GizmoRenderPipeline"/> + <see cref="IMarkerGizmoRenderer"/>。
    /// </para>
    /// </summary>
    public static class MarkerGizmoDrawer
    {
        /// <summary>
        /// 根据标记的 Tag 前缀获取 Gizmo 颜色。
        /// 委托到 <see cref="GizmoStyleConstants.GetLayerColor"/>。
        /// </summary>
        public static Color GetMarkerColor(SceneMarker marker)
            => GizmoStyleConstants.GetLayerColor(marker);
    }
}
