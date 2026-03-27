#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Geometry
{
    /// <summary>
    /// Polygon 区域形状几何实现——支持凹多边形和带洞多边形。
    /// <para>
    /// 三角剖分（Ear Clipping）采用 Lazy 缓存策略：
    /// 首次调用 <see cref="GetTriangles"/> 时计算并缓存，之后直接返回缓存。
    /// 构造时传入世界坐标顶点，实例为不可变对象，几何变化时由 AreaMarker 创建新实例。
    /// </para>
    /// </summary>
    public sealed class PolygonAreaShape : IAreaShape
    {
        private readonly Vector3[] _outerVertices;
        private readonly IReadOnlyList<Vector3[]>? _holes;
        private readonly float _height;
        private readonly Bounds _bounds;

        // Lazy 缓存
        private Triangle2D[]? _cachedTriangles;
        private float _cachedArea = -1f;
        private float[]? _triangleAreaCdf;   // 用于面积加权采样的 CDF

        public AreaShape ShapeType => AreaShape.Polygon;

        /// <summary>
        /// 构造无洞多边形形状。
        /// </summary>
        /// <param name="worldVertices">世界坐标顶点列表（≥3 个）</param>
        /// <param name="height">拉伸高度</param>
        public PolygonAreaShape(IReadOnlyList<Vector3> worldVertices, float height)
        {
            _outerVertices = new Vector3[worldVertices.Count];
            for (int i = 0; i < worldVertices.Count; i++)
                _outerVertices[i] = worldVertices[i];
            _holes  = null;
            _height = height;
            _bounds = ComputeBounds(_outerVertices, height);
        }

        /// <summary>
        /// 构造带洞多边形形状。
        /// </summary>
        /// <param name="worldVertices">外轮廓世界坐标顶点列表（≥3 个）</param>
        /// <param name="holes">洞轮廓世界坐标顶点列表（每个洞 ≥3 个顶点）</param>
        /// <param name="height">拉伸高度</param>
        public PolygonAreaShape(IReadOnlyList<Vector3> worldVertices, IReadOnlyList<Vector3[]>? holes, float height)
        {
            _outerVertices = new Vector3[worldVertices.Count];
            for (int i = 0; i < worldVertices.Count; i++)
                _outerVertices[i] = worldVertices[i];
            _holes  = holes;
            _height = height;
            _bounds = ComputeBounds(_outerVertices, height);
        }

        public Bounds GetBounds() => _bounds;

        public bool ContainsPointXZ(Vector3 worldPoint)
        {
            // 1. 点必须在外轮廓内
            if (!IsPointInPolygonXZ(worldPoint, _outerVertices)) return false;
            // 2. 点不能在任何洞内
            if (_holes != null)
                foreach (var hole in _holes)
                    if (IsPointInPolygonXZ(worldPoint, hole)) return false;
            return true;
        }

        public Vector3 SampleRandomPoint(System.Random rng)
        {
            var triangles = GetTriangles();
            if (triangles.Length == 0) return _bounds.center;

            // 初始化面积 CDF（Lazy）
            if (_triangleAreaCdf == null)
                _triangleAreaCdf = BuildAreaCdf(triangles);

            // 面积加权选三角形
            float u = (float)rng.NextDouble() * _triangleAreaCdf[_triangleAreaCdf.Length - 1];
            int triIdx = BinarySearchCdf(_triangleAreaCdf, u);

            // 三角形内均匀采样（重心坐标法）
            var sample = triangles[triIdx].SampleRandom(rng);

            // 对于带洞多边形，采样点可能落在洞内，需要拒绝采样
            if (_holes != null)
            {
                const int maxRetries = 20;
                for (int i = 0; i < maxRetries; i++)
                {
                    if (ContainsPointXZ(sample)) return sample;
                    u = (float)rng.NextDouble() * _triangleAreaCdf[_triangleAreaCdf.Length - 1];
                    triIdx = BinarySearchCdf(_triangleAreaCdf, u);
                    sample = triangles[triIdx].SampleRandom(rng);
                }
            }

            return sample;
        }

        public Triangle2D[] GetTriangles()
        {
            if (_cachedTriangles != null) return _cachedTriangles;

            if (_holes == null || _holes.Count == 0)
                _cachedTriangles = EarClippingTriangulator.Triangulate(_outerVertices);
            else
            {
                var holeLists = new IReadOnlyList<Vector3>[_holes.Count];
                for (int i = 0; i < _holes.Count; i++) holeLists[i] = _holes[i];
                _cachedTriangles = EarClippingTriangulator.Triangulate(_outerVertices, holeLists);
            }

            return _cachedTriangles;
        }

        public float ComputeArea()
        {
            if (_cachedArea >= 0f) return _cachedArea;
            _cachedArea = 0f;
            foreach (var t in GetTriangles())
                _cachedArea += t.AreaXZ;
            return _cachedArea;
        }

        // ═══════════════════════════════════════════════════════
        // 内部工具
        // ═══════════════════════════════════════════════════════

        /// <summary>XZ 平面射线法判断点是否在多边形内</summary>
        private static bool IsPointInPolygonXZ(Vector3 point, IReadOnlyList<Vector3> verts)
        {
            int intersections = 0;
            int n = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var v1 = verts[i];
                var v2 = verts[(i + 1) % n];
                if ((v1.z > point.z) != (v2.z > point.z))
                {
                    float x = v1.x + (point.z - v1.z) / (v2.z - v1.z) * (v2.x - v1.x);
                    if (point.x < x) intersections++;
                }
            }
            return (intersections & 1) == 1;
        }

        private static Bounds ComputeBounds(Vector3[] verts, float height)
        {
            if (verts.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var min = verts[0]; var max = verts[0];
            foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            min.y = verts[0].y;
            max.y = verts[0].y + height;
            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static float[] BuildAreaCdf(Triangle2D[] triangles)
        {
            var cdf = new float[triangles.Length];
            float cumulative = 0f;
            for (int i = 0; i < triangles.Length; i++)
            {
                cumulative += triangles[i].AreaXZ;
                cdf[i] = cumulative;
            }
            return cdf;
        }

        private static int BinarySearchCdf(float[] cdf, float value)
        {
            int lo = 0, hi = cdf.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (cdf[mid] < value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }
}
