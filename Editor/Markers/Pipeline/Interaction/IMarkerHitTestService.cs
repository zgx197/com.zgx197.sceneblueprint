#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline.Interaction
{
    /// <summary>
    /// 标记命中测试服务。
    /// 负责在当前可见绘制上下文中，根据鼠标位置选出最接近的 Marker。
    /// </summary>
    internal interface IMarkerHitTestService
    {
        SceneMarker? FindClosestMarker(
            Vector2 mousePosition,
            IReadOnlyList<GizmoDrawContext> drawList,
            IReadOnlyDictionary<Type, IMarkerGizmoRenderer> renderers);
    }
}
