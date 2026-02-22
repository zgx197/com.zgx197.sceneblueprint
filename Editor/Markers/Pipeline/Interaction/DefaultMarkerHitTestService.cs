#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline.Interaction
{
    /// <summary>
    /// 默认标记命中测试实现。
    /// 使用各 Renderer 提供的 PickBounds，并基于与鼠标的屏幕距离选择最近命中目标。
    /// </summary>
    internal sealed class DefaultMarkerHitTestService : IMarkerHitTestService
    {
        public SceneMarker? FindClosestMarker(
            Vector2 mousePosition,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers)
        {
            SceneMarker? best = null;
            float bestDist = float.MaxValue;

            foreach (var ctx in drawList)
            {
                if (!renderers.TryGetValue(ctx.Marker.GetType(), out var renderer))
                    continue;

                var pickBounds = renderer.GetPickBounds(in ctx);
                float dist = HandleUtility.DistanceToCircle(pickBounds.Center, pickBounds.Radius);

                if (dist < GizmoStyleConstants.PickDistanceThreshold && dist < bestDist)
                {
                    bestDist = dist;
                    best = ctx.Marker;
                }
            }

            return best;
        }
    }
}
