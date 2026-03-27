#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Geometry
{
    /// <summary>
    /// Capsule 区域形状几何实现。
    /// <para>胶囊由两端圆心世界坐标 + 半径定义。采样使用矩形+半圆分区法。</para>
    /// </summary>
    public sealed class CapsuleAreaShape : IAreaShape
    {
        private readonly Vector3 _pointA;
        private readonly Vector3 _pointB;
        private readonly float _radius;
        private readonly float _height;
        private Triangle2D[]? _triangles;

        private const int SemiCircleSegments = 16;

        public AreaShape ShapeType => AreaShape.Capsule;

        public CapsuleAreaShape(Vector3 pointA, Vector3 pointB, float radius, float height)
        {
            _pointA = pointA;
            _pointB = pointB;
            _radius = radius;
            _height = height;
        }

        public Bounds GetBounds()
        {
            var min = Vector3.Min(_pointA, _pointB) - new Vector3(_radius, 0, _radius);
            var max = Vector3.Max(_pointA, _pointB) + new Vector3(_radius, _height, _radius);
            return new Bounds((min + max) * 0.5f, max - min);
        }

        public bool ContainsPointXZ(Vector3 worldPoint)
        {
            float dist = DistanceToSegmentXZ(worldPoint, _pointA, _pointB);
            return dist <= _radius;
        }

        public Vector3 SampleRandomPoint(System.Random rng)
        {
            var axis = _pointB - _pointA;
            float len = new Vector2(axis.x, axis.z).magnitude;
            var axisDir = len > 1e-6f ? new Vector2(axis.x, axis.z) / len : Vector2.right;
            var perpDir = new Vector2(-axisDir.y, axisDir.x);

            // 面积分区采样：矩形部分 + 两个半圆部分
            float rectArea = len * _radius * 2f;
            float semiArea = Mathf.PI * _radius * _radius;  // 两个半圆合计
            float totalArea = rectArea + semiArea;

            float u = (float)rng.NextDouble() * totalArea;

            Vector2 sample2D;
            if (u < rectArea)
            {
                // 矩形内采样
                float t = (float)rng.NextDouble() * len;
                float s = ((float)rng.NextDouble() * 2f - 1f) * _radius;
                sample2D = new Vector2(_pointA.x, _pointA.z)
                    + axisDir * t + perpDir * s;
            }
            else
            {
                // 半圆内采样（均匀极坐标）
                bool nearA = rng.NextDouble() < 0.5;
                var center2D = nearA
                    ? new Vector2(_pointA.x, _pointA.z)
                    : new Vector2(_pointB.x, _pointB.z);
                float r = Mathf.Sqrt((float)rng.NextDouble()) * _radius;
                float theta = (float)(rng.NextDouble() * System.Math.PI);
                // 半圆方向：A 端朝 -axis，B 端朝 +axis
                var halfDir = nearA ? -axisDir : axisDir;
                float angle = Mathf.Atan2(halfDir.y, halfDir.x) - Mathf.PI * 0.5f + theta;
                sample2D = center2D + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }

            return new Vector3(sample2D.x, _pointA.y, sample2D.y);
        }

        public Triangle2D[] GetTriangles()
        {
            if (_triangles != null) return _triangles;

            var result = new System.Collections.Generic.List<Triangle2D>();
            var axisXZ = new Vector2(_pointB.x - _pointA.x, _pointB.z - _pointA.z);
            float len = axisXZ.magnitude;
            var axisDir = len > 1e-6f ? axisXZ / len : Vector2.right;
            var perpDir = new Vector2(-axisDir.y, axisDir.x) * _radius;
            var axisDirScaled = axisDir * _radius;

            Vector3 ToWorld(Vector2 v) => new Vector3(v.x, _pointA.y, v.y);

            var cA2D = new Vector2(_pointA.x, _pointA.z);
            var cB2D = new Vector2(_pointB.x, _pointB.z);

            // 矩形中轴两侧各 2 个三角形
            var r0 = ToWorld(cA2D + perpDir);
            var r1 = ToWorld(cA2D - perpDir);
            var r2 = ToWorld(cB2D - perpDir);
            var r3 = ToWorld(cB2D + perpDir);
            result.Add(new Triangle2D(r0, r1, r2));
            result.Add(new Triangle2D(r0, r2, r3));

            // A 端半圆
            float baseAngleA = Mathf.Atan2(-axisDir.y, -axisDir.x);
            for (int i = 0; i < SemiCircleSegments; i++)
            {
                float a0 = baseAngleA + Mathf.PI * i / SemiCircleSegments;
                float a1 = baseAngleA + Mathf.PI * (i + 1) / SemiCircleSegments;
                var p0 = ToWorld(cA2D + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * _radius);
                var p1 = ToWorld(cA2D + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * _radius);
                result.Add(new Triangle2D(_pointA, p0, p1));
            }

            // B 端半圆
            float baseAngleB = Mathf.Atan2(axisDir.y, axisDir.x);
            for (int i = 0; i < SemiCircleSegments; i++)
            {
                float a0 = baseAngleB + Mathf.PI * i / SemiCircleSegments;
                float a1 = baseAngleB + Mathf.PI * (i + 1) / SemiCircleSegments;
                var p0 = ToWorld(cB2D + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * _radius);
                var p1 = ToWorld(cB2D + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * _radius);
                result.Add(new Triangle2D(_pointB, p0, p1));
            }

            _triangles = result.ToArray();
            return _triangles;
        }

        public float ComputeArea()
        {
            var axisXZ = new Vector2(_pointB.x - _pointA.x, _pointB.z - _pointA.z);
            float len = axisXZ.magnitude;
            return len * _radius * 2f + Mathf.PI * _radius * _radius;
        }

        private static float DistanceToSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            float ax = a.x, az = a.z, bx = b.x, bz = b.z, px = p.x, pz = p.z;
            float dx = bx - ax, dz = bz - az;
            float lenSq = dx * dx + dz * dz;
            if (lenSq < 1e-8f) return Mathf.Sqrt((px - ax) * (px - ax) + (pz - az) * (pz - az));
            float t = Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / lenSq);
            float cx = ax + t * dx, cz = az + t * dz;
            return Mathf.Sqrt((px - cx) * (px - cx) + (pz - cz) * (pz - cz));
        }
    }
}
