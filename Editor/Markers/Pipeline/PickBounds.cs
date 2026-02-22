#nullable enable
using UnityEngine;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// 标记的拾取区域信息——用于 Scene View 中点击检测。
    /// <para>
    /// 管线在 Pick Phase 中使用此数据与 <see cref="UnityEditor.HandleUtility.DistanceToCircle"/>
    /// 配合，判断鼠标是否命中标记的 Gizmo 区域。
    /// </para>
    /// </summary>
    public struct PickBounds
    {
        /// <summary>拾取区域中心点（世界坐标）</summary>
        public Vector3 Center;

        /// <summary>拾取区域半径（世界单位）</summary>
        public float Radius;
    }
}
