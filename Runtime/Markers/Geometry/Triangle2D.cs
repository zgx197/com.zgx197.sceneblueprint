#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Geometry
{
    /// <summary>
    /// XZ 平面三角形——三角剖分的基本单元。
    /// <para>
    /// 三个顶点均为世界坐标，Y 分量保留用于 3D 渲染（如 Gizmo 填充面）。
    /// 面积计算忽略 Y 分量，在 XZ 平面上进行。
    /// </para>
    /// </summary>
    public readonly struct Triangle2D
    {
        public readonly Vector3 A;
        public readonly Vector3 B;
        public readonly Vector3 C;

        public Triangle2D(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c;
        }

        /// <summary>XZ 平面上的有符号面积（正值 = CCW，负值 = CW）</summary>
        public float SignedAreaXZ =>
            0.5f * ((B.x - A.x) * (C.z - A.z) - (C.x - A.x) * (B.z - A.z));

        /// <summary>XZ 平面上的面积（绝对值）</summary>
        public float AreaXZ => System.Math.Abs(SignedAreaXZ);

        /// <summary>
        /// 在三角形内部均匀采样一个随机点（重心坐标法）。
        /// </summary>
        public Vector3 SampleRandom(System.Random rng)
        {
            float r1 = (float)rng.NextDouble();
            float r2 = (float)rng.NextDouble();
            if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
            float r3 = 1f - r1 - r2;
            return A * r3 + B * r1 + C * r2;
        }
    }
}
