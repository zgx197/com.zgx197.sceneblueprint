#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Geometry
{
    /// <summary>
    /// 区域形状几何接口——封装所有形状的几何运算。
    /// <para>
    /// 设计原则：每种形状独立实现，新增形状只需新增一个实现类（OCP）。
    /// 所有运算在 XZ 平面进行，忽略 Y 分量（高度由 AreaMarker.Height 单独处理）。
    /// </para>
    /// <para>
    /// 实现类为不可变值对象——构造时传入世界坐标参数，
    /// 几何数据变化时由 <see cref="Runtime.Markers.AreaMarker.GetShape"/> 创建新实例。
    /// </para>
    /// </summary>
    public interface IAreaShape
    {
        /// <summary>形状类型</summary>
        AreaShape ShapeType { get; }

        /// <summary>
        /// XZ 平面 AABB 包围盒（世界坐标）。
        /// Y 范围覆盖 [底面, 底面+Height]。
        /// </summary>
        Bounds GetBounds();

        /// <summary>
        /// 判断世界坐标点是否在区域内（XZ 平面，忽略 Y）。
        /// </summary>
        bool ContainsPointXZ(Vector3 worldPoint);

        /// <summary>
        /// 在区域内均匀采样一个随机点（XZ 平面，Y=底面高度）。
        /// <para>
        /// Polygon 使用面积加权三角形采样（均匀分布）。
        /// Box/Circle/Capsule 使用各自最优采样策略。
        /// </para>
        /// </summary>
        Vector3 SampleRandomPoint(System.Random rng);

        /// <summary>
        /// 底面三角剖分结果（世界坐标，XZ 平面）。
        /// <para>
        /// 用于 Gizmo 填充渲染——对每个三角形调用 Handles.DrawAAConvexPolygon。
        /// Polygon 实现内部 Lazy 缓存，首次调用时进行 Ear Clipping 三角剖分。
        /// </para>
        /// </summary>
        Triangle2D[] GetTriangles();

        /// <summary>XZ 平面面积（平方单位）</summary>
        float ComputeArea();
    }
}
