#nullable enable
using System;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;

namespace SceneBlueprint.Editor.Markers.Renderers
{
    /// <summary>
    /// PointMarker 的 Gizmo 渲染器。
    /// <para>
    /// 绘制：实心球 + 线框球 + 方向箭头 + 标签。
    /// 高亮时：脉冲缩放 + 外圈光晕。
    /// </para>
    /// </summary>
    public class PointMarkerRenderer : IMarkerGizmoRenderer
    {
        public Type TargetType => typeof(PointMarker);

        public void DrawIcon(in GizmoDrawContext ctx)
        {
            var pm = (PointMarker)ctx.Marker;
            var pos = ctx.Transform.position;
            float radius = pm.GizmoRadius * ctx.PulseScale;

            // 实心球
            Handles.color = (ctx.IsSelected || ctx.IsHighlighted)
                ? ctx.EffectiveColor
                : new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b, 0.5f);
            Handles.SphereHandleCap(0, pos, Quaternion.identity, radius * 2f, EventType.Repaint);

            // 线框球
            Handles.color = ctx.EffectiveColor;
            Handles.DrawWireDisc(pos, Vector3.up, radius);
            Handles.DrawWireDisc(pos, Vector3.forward, radius);
            Handles.DrawWireDisc(pos, Vector3.right, radius);
        }

        public void DrawWireframe(in GizmoDrawContext ctx)
        {
            var pm = (PointMarker)ctx.Marker;
            if (!pm.ShowDirection) return;

            var pos = ctx.Transform.position;
            float radius = pm.GizmoRadius;
            float arrowLen = radius * 3f;
            var forward = ctx.Transform.forward * arrowLen;

            Handles.color = ctx.EffectiveColor;
            Handles.DrawLine(pos, pos + forward);

            // 箭头头部
            float headSize = radius * 0.6f;
            var right = ctx.Transform.right * headSize;
            var tip = pos + forward;
            Handles.DrawLine(tip, tip - forward.normalized * headSize + right);
            Handles.DrawLine(tip, tip - forward.normalized * headSize - right);
        }

        public void DrawHighlight(in GizmoDrawContext ctx)
        {
            var pm = (PointMarker)ctx.Marker;
            var pos = ctx.Transform.position;
            float radius = pm.GizmoRadius;

            // 外圈脉冲光晕
            float glowRadius = radius * 1.8f;
            Handles.color = new Color(
                ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                ctx.PulseAlpha * 0.3f);
            Handles.SphereHandleCap(0, pos, Quaternion.identity, glowRadius * 2f, EventType.Repaint);

            Handles.color = new Color(
                ctx.EffectiveColor.r, ctx.EffectiveColor.g, ctx.EffectiveColor.b,
                ctx.PulseAlpha * 0.7f);
            Handles.DrawWireDisc(pos, Vector3.up, glowRadius);
        }

        public void DrawLabel(in GizmoDrawContext ctx)
        {
            var pm = (PointMarker)ctx.Marker;
            var pos = ctx.Transform.position + Vector3.up * (pm.GizmoRadius + 0.5f);
            GizmoLabelUtil.DrawStandardLabel(ctx.Marker, pos, ctx.EffectiveColor);
        }

        public PickBounds GetPickBounds(in GizmoDrawContext ctx)
        {
            var pm = (PointMarker)ctx.Marker;
            return new PickBounds
            {
                Center = ctx.Transform.position,
                Radius = pm.GizmoRadius * 2f
            };
        }
    }
}
