#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Geometry
{
    /// <summary>
    /// Box 区域形状几何实现。
    /// <para>底面矩形由 BoxSize（XZ 尺寸）定义，支持任意旋转（通过 Transform 传入）。</para>
    /// </summary>
    public sealed class BoxAreaShape : IAreaShape
    {
        private readonly Vector3 _center;
        private readonly Vector2 _boxSize;
        private readonly float _height;
        private readonly Quaternion _rotation;
        private readonly Bounds _bounds;
        private Triangle2D[]? _triangles;

        public AreaShape ShapeType => AreaShape.Box;

        public BoxAreaShape(Transform transform, Vector2 boxSize, float height)
        {
            _center   = transform.position;
            _boxSize  = boxSize;
            _height   = height;
            _rotation = transform.rotation;

            // 保守 AABB（包含旋转后的四个顶点）
            float hw = boxSize.x * 0.5f;
            float hd = boxSize.y * 0.5f;
            var corners = new[]
            {
                transform.TransformPoint(new Vector3( hw, 0,  hd)),
                transform.TransformPoint(new Vector3(-hw, 0,  hd)),
                transform.TransformPoint(new Vector3( hw, 0, -hd)),
                transform.TransformPoint(new Vector3(-hw, 0, -hd)),
            };
            var min = corners[0]; var max = corners[0];
            foreach (var c in corners) { min = Vector3.Min(min, c); max = Vector3.Max(max, c); }
            min.y = _center.y;
            max.y = _center.y + _height;
            _bounds = new Bounds((min + max) * 0.5f, max - min);
        }

        public Bounds GetBounds() => _bounds;

        public bool ContainsPointXZ(Vector3 worldPoint)
        {
            // 转换到局部空间，再做 AABB 检查
            var local = Quaternion.Inverse(_rotation) * (worldPoint - _center);
            float hw = _boxSize.x * 0.5f;
            float hd = _boxSize.y * 0.5f;
            return local.x >= -hw && local.x <= hw && local.z >= -hd && local.z <= hd;
        }

        public Vector3 SampleRandomPoint(System.Random rng)
        {
            float hw = _boxSize.x * 0.5f;
            float hd = _boxSize.y * 0.5f;
            var localPos = new Vector3(
                (float)(rng.NextDouble() * 2 - 1) * hw,
                0f,
                (float)(rng.NextDouble() * 2 - 1) * hd);
            return _center + _rotation * localPos;
        }

        public Triangle2D[] GetTriangles()
        {
            if (_triangles != null) return _triangles;

            float hw = _boxSize.x * 0.5f;
            float hd = _boxSize.y * 0.5f;
            // 底面矩形 → 2 个三角形（世界坐标）
            var v0 = _center + _rotation * new Vector3(-hw, 0, -hd);
            var v1 = _center + _rotation * new Vector3( hw, 0, -hd);
            var v2 = _center + _rotation * new Vector3( hw, 0,  hd);
            var v3 = _center + _rotation * new Vector3(-hw, 0,  hd);

            _triangles = new[]
            {
                new Triangle2D(v0, v1, v2),
                new Triangle2D(v0, v2, v3),
            };
            return _triangles;
        }

        public float ComputeArea() => _boxSize.x * _boxSize.y;
    }
}
