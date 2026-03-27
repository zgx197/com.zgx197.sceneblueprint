#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Geometry
{
    /// <summary>
    /// Circle 区域形状几何实现。
    /// <para>圆形由圆心世界坐标 + 半径定义，采样使用均匀极坐标法。</para>
    /// </summary>
    public sealed class CircleAreaShape : IAreaShape
    {
        private readonly Vector3 _center;
        private readonly float _radius;
        private readonly float _height;
        private Triangle2D[]? _triangles;

        private const int DiscSegments = 32;

        public AreaShape ShapeType => AreaShape.Circle;

        public CircleAreaShape(Vector3 center, float radius, float height)
        {
            _center = center;
            _radius = radius;
            _height = height;
        }

        public Bounds GetBounds()
        {
            var size = new Vector3(_radius * 2f, _height, _radius * 2f);
            var boundsCenter = _center + Vector3.up * (_height * 0.5f);
            return new Bounds(boundsCenter, size);
        }

        public bool ContainsPointXZ(Vector3 worldPoint)
        {
            float dx = worldPoint.x - _center.x;
            float dz = worldPoint.z - _center.z;
            return dx * dx + dz * dz <= _radius * _radius;
        }

        public Vector3 SampleRandomPoint(System.Random rng)
        {
            // 均匀极坐标采样：r = sqrt(U) * Radius，θ = U * 2π
            float r = Mathf.Sqrt((float)rng.NextDouble()) * _radius;
            float theta = (float)(rng.NextDouble() * System.Math.PI * 2.0);
            return new Vector3(
                _center.x + r * Mathf.Cos(theta),
                _center.y,
                _center.z + r * Mathf.Sin(theta));
        }

        public Triangle2D[] GetTriangles()
        {
            if (_triangles != null) return _triangles;

            _triangles = new Triangle2D[DiscSegments];
            float angleStep = Mathf.PI * 2f / DiscSegments;
            for (int i = 0; i < DiscSegments; i++)
            {
                float a0 = i * angleStep;
                float a1 = (i + 1) * angleStep;
                var p0 = new Vector3(_center.x + Mathf.Cos(a0) * _radius, _center.y, _center.z + Mathf.Sin(a0) * _radius);
                var p1 = new Vector3(_center.x + Mathf.Cos(a1) * _radius, _center.y, _center.z + Mathf.Sin(a1) * _radius);
                _triangles[i] = new Triangle2D(_center, p0, p1);
            }
            return _triangles;
        }

        public float ComputeArea() => Mathf.PI * _radius * _radius;
    }
}
