#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers
{
    /// <summary>
    /// 单点标记——表示一个位置 + 朝向。
    /// <para>
    /// 典型用途：刷怪点、摄像机位、VFX 播放点、路径点、伏击点。
    /// 空间数据完全由 Transform 提供，无需额外字段。
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Point Marker")]
    public class PointMarker : SceneMarker
    {
        public override string MarkerTypeId => "Point";

        [Header("显示")]

        [Tooltip("Gizmo 显示半径")]
        public float GizmoRadius = 0.5f;

        [Tooltip("是否显示方向箭头")]
        public bool ShowDirection = true;
    }
}
