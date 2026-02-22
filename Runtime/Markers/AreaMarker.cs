#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers
{
    /// <summary>
    /// 区域形状类型
    /// </summary>
    public enum AreaShape
    {
        /// <summary>多边形——由顶点列表定义不规则区域</summary>
        Polygon,
        /// <summary>Box——由中心点 + 尺寸定义矩形区域</summary>
        Box
    }

    /// <summary>
    /// 区域标记——表示一个多边形或 Box 区域。
    /// <para>
    /// 典型用途：触发区域、刷怪区域、灯光区域、音频区域。
    /// Polygon 模式下顶点为相对坐标（相对于 Transform 位置）。
    /// Box 模式下使用 Transform 位置作为中心，BoxSize 作为尺寸。
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Area Marker")]
    public class AreaMarker : SceneMarker
    {
        public override string MarkerTypeId => "Area";

        [Header("区域形状")]

        [Tooltip("区域形状：多边形或 Box")]
        public AreaShape Shape = AreaShape.Box;

        [Tooltip("多边形顶点（相对于 Transform 的局部坐标）")]
        public List<Vector3> Vertices = new();

        [Tooltip("Box 模式的尺寸")]
        public Vector3 BoxSize = new Vector3(5f, 3f, 5f);

        [Tooltip("区域高度（用于体积判定和 Gizmo 绘制）")]
        public float Height = 3f;

        /// <summary>
        /// 返回区域中心作为代表位置。
        /// <para>Polygon 模式返回所有顶点的重心，Box 模式返回 Transform 位置。</para>
        /// </summary>
        public override Vector3 GetRepresentativePosition()
        {
            if (Shape == AreaShape.Box || Vertices.Count == 0)
                return transform.position;

            var center = Vector3.zero;
            foreach (var v in Vertices)
                center += v;
            return transform.position + center / Vertices.Count;
        }

        /// <summary>
        /// 获取世界坐标下的顶点列表（Polygon 模式）。
        /// </summary>
        public List<Vector3> GetWorldVertices()
        {
            var worldVerts = new List<Vector3>(Vertices.Count);
            foreach (var v in Vertices)
                worldVerts.Add(transform.TransformPoint(v));
            return worldVerts;
        }

        /// <summary>
        /// 获取世界坐标下的 Box 边界（Box 模式）。
        /// </summary>
        public Bounds GetWorldBounds()
        {
            return new Bounds(transform.position, BoxSize);
        }
    }
}
