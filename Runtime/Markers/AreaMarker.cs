#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Runtime.Markers.Geometry;

namespace SceneBlueprint.Runtime.Markers
{
    /// <summary>
    /// 区域形状类型
    /// </summary>
    public enum AreaShape
    {
        /// <summary>Box——由底面尺寸 (width × depth) + 高度定义矩形区域</summary>
        Box,
        /// <summary>Circle——由半径 + 高度定义圆形区域</summary>
        Circle,
        /// <summary>Capsule——由两端点距离 + 半径 + 高度定义胶囊区域</summary>
        Capsule,
        /// <summary>Polygon——由顶点列表 + 高度定义不规则区域</summary>
        Polygon,
    }

    /// <summary>
    /// 区域标记——表示一个 Box / Circle / Capsule / Polygon 区域。
    /// <para>
    /// 典型用途：触发区域、刷怪区域、灯光区域、音频区域。
    /// </para>
    /// <para>
    /// <b>锚点约定</b>：Transform.position 为底面中心的世界坐标，
    /// 区域从底面向上拉伸 <see cref="Height"/>。
    /// 局部空间中底面在 y=0，顶面在 y=Height。
    /// </para>
    /// <para>
    /// <b>导出约定</b>：所有形状统一导出为三种类型之一——
    /// Polygon（含 Box 展开的 4 顶点）/ Circle / Capsule，
    /// 导出坐标均为世界坐标，多边形顶点为俯视顺时针绕序。
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Area Marker")]
    public class AreaMarker : SceneMarker
    {
        public override string MarkerTypeId => "Area";

        [Header("区域形状")]

        [Tooltip("区域形状")]
        public AreaShape Shape = AreaShape.Box;

        // ── Box 参数 ──────────────────────────────────────────

        [Tooltip("底面尺寸：x=宽(X轴), y=深(Z轴)")]
        public Vector2 BoxSize = new Vector2(5f, 5f);

        // ── Circle 参数 ───────────────────────────────────────

        [Tooltip("圆形半径")]
        public float Radius = 3f;

        // ── Capsule 参数 ──────────────────────────────────────

        [Tooltip("胶囊两端圆心之间的距离（沿局部 Z 轴）")]
        public float CapsuleLength = 4f;

        [Tooltip("胶囊半径")]
        public float CapsuleRadius = 2f;

        // ── Polygon 参数 ──────────────────────────────────────

        [Tooltip("多边形顶点（相对于 Transform 的局部坐标）")]
        public List<Vector3> Vertices = new();

        [Tooltip("多边形洞的顶点列表（每个洞为一组局部坐标顶点，可选）")]
        public List<PolygonHole> Holes = new();

        // ── 通用参数 ──────────────────────────────────────────

        [Tooltip("区域拉伸高度（从底面向上）")]
        public float Height = 3f;

        // ═══════════════════════════════════════════════════════
        // 生命周期
        // ═══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 切换到 Polygon 时，如果没有顶点，自动初始化一个默认三角形
            if (Shape == AreaShape.Polygon && Vertices.Count == 0)
            {
                float r = 3f;
                Vertices.Add(new Vector3(0, 0, r));
                Vertices.Add(new Vector3(r * Mathf.Sin(Mathf.Deg2Rad * 120f), 0, r * Mathf.Cos(Mathf.Deg2Rad * 120f)));
                Vertices.Add(new Vector3(r * Mathf.Sin(Mathf.Deg2Rad * 240f), 0, r * Mathf.Cos(Mathf.Deg2Rad * 240f)));
            }
            IncrementGeometryVersion();
        }
#endif

        // ═══════════════════════════════════════════════════════
        // 公共方法
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 返回区域底面中心作为代表位置。
        /// <para>
        /// Box/Circle/Capsule 返回 Transform 位置（底面中心）。
        /// Polygon 返回所有顶点的重心（世界坐标）。
        /// </para>
        /// </summary>
        public override Vector3 GetRepresentativePosition()
        {
            if (Shape != AreaShape.Polygon || Vertices.Count == 0)
                return transform.position;

            var center = Vector3.zero;
            foreach (var v in Vertices)
                center += v;
            return transform.position + center / Vertices.Count;
        }

        /// <summary>
        /// 获取世界坐标下的多边形顶点列表（Polygon 模式）。
        /// </summary>
        public List<Vector3> GetWorldVertices()
        {
            var worldVerts = new List<Vector3>(Vertices.Count);
            foreach (var v in Vertices)
                worldVerts.Add(transform.TransformPoint(v));
            return worldVerts;
        }

        /// <summary>
        /// 获取世界坐标下的洞轮廓列表（Polygon 模式）。
        /// </summary>
        public List<Vector3[]>? GetWorldHoles()
        {
            if (Holes.Count == 0) return null;
            var result = new List<Vector3[]>(Holes.Count);
            foreach (var hole in Holes)
            {
                if (hole.Vertices == null || hole.Vertices.Count < 3) continue;
                var worldHole = new Vector3[hole.Vertices.Count];
                for (int i = 0; i < hole.Vertices.Count; i++)
                    worldHole[i] = transform.TransformPoint(hole.Vertices[i]);
                result.Add(worldHole);
            }
            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// 获取世界坐标下的 Box 边界。
        /// <para>锚点在底部，Bounds 中心在 position + up * Height/2。</para>
        /// </summary>
        public Bounds GetWorldBounds()
        {
            var boundsCenter = transform.position + Vector3.up * (Height * 0.5f);
            var boundsSize = new Vector3(BoxSize.x, Height, BoxSize.y);
            return new Bounds(boundsCenter, boundsSize);
        }

        /// <summary>
        /// 获取 Capsule 两端圆心的世界坐标（沿局部 Z 轴对称分布）。
        /// </summary>
        public (Vector3 pointA, Vector3 pointB) GetCapsuleWorldPoints()
        {
            float halfLen = CapsuleLength * 0.5f;
            var localA = new Vector3(0, 0, -halfLen);
            var localB = new Vector3(0, 0,  halfLen);
            return (transform.TransformPoint(localA), transform.TransformPoint(localB));
        }

        // ═══════════════════════════════════════════════════════
        // 导出用：底面顺时针顶点生成
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 生成底面顺时针顶点列表（局部坐标，底面 y=0）。
        /// <para>
        /// Box → 4 顶点；Polygon → 原始顶点（确保顺时针）。
        /// Circle / Capsule 不使用此方法，它们有专用导出路径。
        /// </para>
        /// </summary>
        public Vector3[] GenerateFloorVerticesCW()
        {
            switch (Shape)
            {
                case AreaShape.Box:
                {
                    float hw = BoxSize.x * 0.5f;
                    float hd = BoxSize.y * 0.5f; // BoxSize.y = depth(Z)
                    // 俯视顺时针（Y 轴朝上，Z 轴朝前）
                    return new[]
                    {
                        new Vector3( hw, 0, -hd),  // 右后
                        new Vector3( hw, 0,  hd),  // 右前
                        new Vector3(-hw, 0,  hd),  // 左前
                        new Vector3(-hw, 0, -hd),  // 左后
                    };
                }

                case AreaShape.Polygon:
                {
                    if (Vertices.Count < 3)
                        return System.Array.Empty<Vector3>();
                    var verts = Vertices.ToArray();
                    return EnsureClockwiseXZ(verts);
                }

                default:
                    // Circle/Capsule 使用专用导出路径，不通过此方法
                    return System.Array.Empty<Vector3>();
            }
        }

        /// <summary>
        /// 将局部底面顶点转换为世界坐标。
        /// </summary>
        public Vector3[] FloorVerticesToWorld(Vector3[] localVerts)
        {
            var world = new Vector3[localVerts.Length];
            for (int i = 0; i < localVerts.Length; i++)
                world[i] = transform.TransformPoint(localVerts[i]);
            return world;
        }

        // ═══════════════════════════════════════════════════════
        // 快照序列化
        // ═══════════════════════════════════════════════════════

        public override string SerializeShapeData()
        {
            var data = new AreaShapeData
            {
                shape = Shape.ToString(),
                boxSizeX = BoxSize.x,
                boxSizeY = BoxSize.y,
                radius = Radius,
                capsuleLength = CapsuleLength,
                capsuleRadius = CapsuleRadius,
                height = Height,
                vertexCount = Vertices.Count
            };
            data.verticesFlat = new float[Vertices.Count * 3];
            for (int i = 0; i < Vertices.Count; i++)
            {
                data.verticesFlat[i * 3]     = Vertices[i].x;
                data.verticesFlat[i * 3 + 1] = Vertices[i].y;
                data.verticesFlat[i * 3 + 2] = Vertices[i].z;
            }

            // 序列化洞数据
            if (Holes.Count > 0)
            {
                data.holeCount = Holes.Count;
                var holeVerts = new List<float>();
                var holeSizes = new List<int>();
                foreach (var hole in Holes)
                {
                    if (hole.Vertices == null) { holeSizes.Add(0); continue; }
                    holeSizes.Add(hole.Vertices.Count);
                    foreach (var v in hole.Vertices)
                    {
                        holeVerts.Add(v.x);
                        holeVerts.Add(v.y);
                        holeVerts.Add(v.z);
                    }
                }
                data.holeVerticesFlat = holeVerts.ToArray();
                data.holeSizes = holeSizes.ToArray();
            }

            return JsonUtility.ToJson(data);
        }

        public override void DeserializeShapeData(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "{}") return;
            var data = JsonUtility.FromJson<AreaShapeData>(json);

            if (System.Enum.TryParse<AreaShape>(data.shape, out var parsedShape))
                Shape = parsedShape;
            BoxSize = new Vector2(data.boxSizeX, data.boxSizeY);
            Radius = data.radius;
            CapsuleLength = data.capsuleLength;
            CapsuleRadius = data.capsuleRadius;
            Height = data.height;

            Vertices.Clear();
            if (data.verticesFlat != null)
            {
                for (int i = 0; i + 2 < data.verticesFlat.Length; i += 3)
                    Vertices.Add(new Vector3(data.verticesFlat[i], data.verticesFlat[i + 1], data.verticesFlat[i + 2]));
            }

            // 反序列化洞数据
            Holes.Clear();
            if (data.holeCount > 0 && data.holeSizes != null && data.holeVerticesFlat != null)
            {
                int offset = 0;
                for (int h = 0; h < data.holeCount; h++)
                {
                    int size = h < data.holeSizes.Length ? data.holeSizes[h] : 0;
                    var hole = new PolygonHole();
                    for (int j = 0; j < size && offset + 2 < data.holeVerticesFlat.Length; j++)
                    {
                        hole.Vertices.Add(new Vector3(
                            data.holeVerticesFlat[offset],
                            data.holeVerticesFlat[offset + 1],
                            data.holeVerticesFlat[offset + 2]));
                        offset += 3;
                    }
                    Holes.Add(hole);
                }
            }
            IncrementGeometryVersion();
        }

        // ═══════════════════════════════════════════════════════
        // 内部工具方法
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 确保顶点数组为俯视顺时针绕序（XZ 平面）。
        /// 如果为逆时针则原地翻转。
        /// </summary>
        private static Vector3[] EnsureClockwiseXZ(Vector3[] verts)
        {
            // 计算 XZ 平面有符号面积（Shoelace 公式）
            // 正值 = 逆时针，负值 = 顺时针
            float signedArea = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % verts.Length];
                signedArea += (b.x - a.x) * (b.z + a.z);
            }

            // signedArea > 0 表示顺时针（XZ 平面，Z 轴朝前、X 轴朝右）
            // 如果 < 0 则是逆时针，需要翻转
            if (signedArea < 0)
                System.Array.Reverse(verts);

            return verts;
        }

        // ═══════════════════════════════════════════════════════
        // 几何形状缓存（IAreaShape）
        // ═══════════════════════════════════════════════════════

        private IAreaShape? _cachedShape;
        private uint _cachedShapeVersion = uint.MaxValue;  // 初始值不等于 GeometryVersion(0)，强制首次重建
        private Matrix4x4 _cachedTransformMatrix;          // 缓存 Transform 矩阵，检测移动/旋转/缩放变化

        /// <summary>
        /// 获取当前区域的几何形状对象（带版本号 + Transform 变化双重缓存）。
        /// <para>
        /// 当 <see cref="SceneMarker.GeometryVersion"/> 变化或 Transform 移动/旋转/缩放时自动重建。
        /// 所有消费方（渲染、随机点生成、点包含检测）统一通过此方法获取几何。
        /// </para>
        /// </summary>
        public IAreaShape GetShape()
        {
            var currentMatrix = transform.localToWorldMatrix;
            if (_cachedShape == null
                || _cachedShapeVersion != GeometryVersion
                || _cachedTransformMatrix != currentMatrix)
            {
                _cachedShape            = BuildShapeInternal();
                _cachedShapeVersion     = GeometryVersion;
                _cachedTransformMatrix  = currentMatrix;
            }
            return _cachedShape;
        }

        /// <summary>
        /// 供编辑器 Handle 代码调用——递增几何版本号使 IAreaShape 缓存失效。
        /// <para>由于 IncrementGeometryVersion() 为 protected，编辑器层通过此方法触发。</para>
        /// </summary>
        public void IncrementGeometryVersionEditor() => IncrementGeometryVersion();

        private IAreaShape BuildShapeInternal()
        {
            switch (Shape)
            {
                case AreaShape.Box:
                    return new BoxAreaShape(transform, BoxSize, Height);
                case AreaShape.Circle:
                    return new CircleAreaShape(transform.position, Radius, Height);
                case AreaShape.Capsule:
                {
                    var (pA, pB) = GetCapsuleWorldPoints();
                    return new CapsuleAreaShape(pA, pB, CapsuleRadius, Height);
                }
                default: // Polygon
                {
                    var worldVerts = GetWorldVertices();
                    var worldHoles = GetWorldHoles();
                    if (worldHoles != null && worldHoles.Count > 0)
                        return new PolygonAreaShape(worldVerts, worldHoles, Height);
                    return new PolygonAreaShape(worldVerts, Height);
                }
            }
        }

        // ── 序列化数据结构 ──

        [System.Serializable]
        private class AreaShapeData
        {
            public string shape = "Box";
            public float boxSizeX, boxSizeY;
            public float radius;
            public float capsuleLength, capsuleRadius;
            public float height;
            public int vertexCount;
            public float[] verticesFlat = System.Array.Empty<float>();
            // 洞数据
            public int holeCount;
            public int[] holeSizes = System.Array.Empty<int>();
            public float[] holeVerticesFlat = System.Array.Empty<float>();
        }
    }

    /// <summary>
    /// 多边形洞轮廓数据——一个洞由若干顶点（局部坐标）定义。
    /// <para>洞轮廓需为简单闭合多边形，≥3 个顶点。</para>
    /// </summary>
    [System.Serializable]
    public class PolygonHole
    {
        [Tooltip("洞轮廓顶点（相对于 AreaMarker 的局部坐标）")]
        public List<Vector3> Vertices = new();
    }
}
