#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Geometry
{
    /// <summary>
    /// Ear Clipping 三角剖分算法——将简单多边形（含凹多边形）分解为三角形列表。
    /// <para>
    /// 支持带洞多边形：先用桥接算法（Bridge Decomposition）将每个洞合并到外轮廓，
    /// 再对合并后的单一多边形做标准 Ear Clipping。
    /// </para>
    /// <para>
    /// 复杂度：O(n²)，适用于顶点数少（&lt;100）的 Marker 多边形。
    /// 所有计算在 XZ 平面进行，忽略 Y 分量。
    /// </para>
    /// </summary>
    public static class EarClippingTriangulator
    {
        // ═══════════════════════════════════════════════════════
        // 公共 API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 简单多边形三角剖分（无洞）。
        /// </summary>
        /// <param name="vertices">顶点列表（世界坐标，XZ 平面，任意绕序）</param>
        public static Triangle2D[] Triangulate(IReadOnlyList<Vector3> vertices)
        {
            if (vertices.Count < 3) return System.Array.Empty<Triangle2D>();

            var poly = new List<Vector3>(vertices);
            EnsureCCW(poly);
            return RunEarClipping(poly);
        }

        /// <summary>
        /// 带洞多边形三角剖分。
        /// </summary>
        /// <param name="outer">外轮廓顶点（世界坐标，XZ 平面，任意绕序）</param>
        /// <param name="holes">洞轮廓列表（每个洞任意绕序）；为 null 或空时等同于无洞版本</param>
        public static Triangle2D[] Triangulate(IReadOnlyList<Vector3> outer, IReadOnlyList<IReadOnlyList<Vector3>>? holes)
        {
            if (outer.Count < 3) return System.Array.Empty<Triangle2D>();
            if (holes == null || holes.Count == 0) return Triangulate(outer);

            var poly = new List<Vector3>(outer);
            EnsureCCW(poly);

            // 对每个洞做桥接合并
            foreach (var hole in holes)
            {
                if (hole.Count < 3) continue;
                var holePoly = new List<Vector3>(hole);
                EnsureCW(holePoly);   // 洞轮廓必须 CW（与外轮廓相反）
                BridgeMerge(poly, holePoly);
            }

            return RunEarClipping(poly);
        }

        // ═══════════════════════════════════════════════════════
        // Ear Clipping 核心
        // ═══════════════════════════════════════════════════════

        private static Triangle2D[] RunEarClipping(List<Vector3> poly)
        {
            int n = poly.Count;
            if (n < 3) return System.Array.Empty<Triangle2D>();
            if (n == 3) return new[] { new Triangle2D(poly[0], poly[1], poly[2]) };

            var result = new List<Triangle2D>(n - 2);
            // 使用双向链表模拟，用索引数组表示 prev/next
            var prev = new int[n];
            var next = new int[n];
            for (int i = 0; i < n; i++)
            {
                prev[i] = (i - 1 + n) % n;
                next[i] = (i + 1) % n;
            }

            int remaining = n;
            int current = 0;
            int safety = n * n + 10;  // 防无限循环

            while (remaining > 3 && safety-- > 0)
            {
                int p = prev[current];
                int nx = next[current];

                if (IsEar(poly, p, current, nx, prev, next, remaining))
                {
                    result.Add(new Triangle2D(poly[p], poly[current], poly[nx]));
                    // 从链表移除 current
                    next[p] = nx;
                    prev[nx] = p;
                    remaining--;
                    current = nx;
                }
                else
                {
                    current = next[current];
                }
            }

            // 最后剩余 3 个顶点
            if (remaining == 3)
            {
                int a = current;
                int b = next[a];
                int c = next[b];
                result.Add(new Triangle2D(poly[a], poly[b], poly[c]));
            }

            return result.ToArray();
        }

        /// <summary>判断顶点 curr 是否是耳朵（ear）</summary>
        private static bool IsEar(List<Vector3> poly, int prev, int curr, int next,
            int[] prevArr, int[] nextArr, int remaining)
        {
            var a = poly[prev];
            var b = poly[curr];
            var c = poly[next];

            // 耳朵条件1：三角形必须是逆时针（CCW），即 curr 是凸顶点
            if (CrossXZ(a, b, c) <= 0f) return false;

            // 耳朵条件2：三角形内部不能包含任何其他顶点
            // 注意：跳过与三角形顶点坐标重合的点（桥接后多边形存在重复顶点）
            int idx = nextArr[next];
            for (int i = 0; i < remaining - 3; i++)
            {
                var p = poly[idx];
                if (!ApproxEqualXZ(p, a) && !ApproxEqualXZ(p, b) && !ApproxEqualXZ(p, c))
                {
                    if (IsPointInTriangleXZ(p, a, b, c))
                        return false;
                }
                idx = nextArr[idx];
            }

            return true;
        }

        /// <summary>XZ 平面上两点是否近似重合</summary>
        private static bool ApproxEqualXZ(Vector3 a, Vector3 b)
        {
            const float eps = 1e-6f;
            return Mathf.Abs(a.x - b.x) < eps && Mathf.Abs(a.z - b.z) < eps;
        }

        // ═══════════════════════════════════════════════════════
        // 桥接算法（Bridge Decomposition）——将洞合并到外轮廓
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 将一个洞合并到外轮廓多边形中（Mutating poly）。
        /// <para>
        /// 算法：找洞上 X 最大顶点 M，向 +X 方向射线与外轮廓求最近交点，
        /// 从交点所在边选"最可见"顶点 V，在 M 和 V 之间插入桥边。
        /// </para>
        /// </summary>
        private static void BridgeMerge(List<Vector3> outer, List<Vector3> hole)
        {
            // 1. 找洞上 X 最大顶点 M
            int mIdx = 0;
            for (int i = 1; i < hole.Count; i++)
                if (hole[i].x > hole[mIdx].x) mIdx = i;
            var m = hole[mIdx];

            // 2. 向 +X 方向射线，找外轮廓上最近交点
            float nearestX = float.MaxValue;
            int bridgeOuterIdx = -1;

            for (int i = 0; i < outer.Count; i++)
            {
                var a = outer[i];
                var b = outer[(i + 1) % outer.Count];

                // 射线 M→+X 与线段 AB 的交点（XZ 平面）
                if (!RayXIntersect(m, a, b, out float ix)) continue;
                if (ix < m.x) continue;  // 交点在射线反方向

                if (ix < nearestX)
                {
                    nearestX = ix;
                    // 取线段中 X 更大的顶点作为候选桥接点
                    bridgeOuterIdx = (a.x >= b.x) ? i : (i + 1) % outer.Count;
                }
            }

            if (bridgeOuterIdx < 0)
            {
                // 退化情况：直接用外轮廓最近顶点
                bridgeOuterIdx = FindNearestVertexXZ(outer, m);
            }
            else
            {
                // 3. 从候选桥接点附近选"最可见"顶点（无遮挡且 X 最大）
                bridgeOuterIdx = FindMostVisibleVertex(outer, m, nearestX, bridgeOuterIdx);
            }

            // 4. 在 bridgeOuterIdx 处拼接洞：
            //    合并后的多边形 = [...outerPre..., V, M, hole[mIdx+1], ..., hole[mIdx-1], M, V, ...outerPost...]
            //    V 已在 outer 中，只需在其后插入：洞顶点序列（从 M 开始旋转一圈回到 M）+ V 副本
            var insertList = new List<Vector3>(hole.Count + 2);
            // 洞顶点从 M 开始旋转一圈（M, M+1, ..., M-1, M）
            for (int i = 0; i <= hole.Count; i++)
                insertList.Add(hole[(mIdx + i) % hole.Count]);
            // 桥边回到 V（重复外轮廓桥接点）
            insertList.Add(outer[bridgeOuterIdx]);
            outer.InsertRange(bridgeOuterIdx + 1, insertList);
        }

        /// <summary>
        /// 射线 M→+X 与线段 AB 的 XZ 平面交点 X 坐标。
        /// 返回 false 表示无交点（线段不跨越射线 Z 坐标）。
        /// </summary>
        private static bool RayXIntersect(Vector3 m, Vector3 a, Vector3 b, out float intersectX)
        {
            intersectX = 0f;
            // 线段需跨越 m.z（一端在上，一端在下或相等）
            if ((a.z > m.z) == (b.z > m.z)) return false;
            // 线性插值得到 X
            float t = (m.z - a.z) / (b.z - a.z);
            intersectX = a.x + t * (b.x - a.x);
            return true;
        }

        /// <summary>找外轮廓中距离 target 最近的顶点索引</summary>
        private static int FindNearestVertexXZ(List<Vector3> poly, Vector3 target)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < poly.Count; i++)
            {
                float dx = poly[i].x - target.x;
                float dz = poly[i].z - target.z;
                float d = dx * dx + dz * dz;
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// 在交点附近选"最可见"顶点：X 坐标最大且从 M 到它的线段不被外轮廓任何边遮挡。
        /// </summary>
        private static int FindMostVisibleVertex(List<Vector3> outer, Vector3 m, float intersectX, int candidateIdx)
        {
            // 简化：检查候选索引及其相邻顶点，选 X 最大且满足可见性的
            var candidates = new[] { candidateIdx, (candidateIdx + 1) % outer.Count, (candidateIdx - 1 + outer.Count) % outer.Count };
            int best = candidateIdx;
            float bestX = -float.MaxValue;

            foreach (int idx in candidates)
            {
                var v = outer[idx];
                if (v.x > bestX && !IsSegmentBlockedByPoly(m, v, outer, idx))
                {
                    bestX = v.x;
                    best = idx;
                }
            }
            return best;
        }

        /// <summary>线段 MV 是否被外轮廓（除 V 所在边）遮挡</summary>
        private static bool IsSegmentBlockedByPoly(Vector3 m, Vector3 v, List<Vector3> outer, int vIdx)
        {
            for (int i = 0; i < outer.Count; i++)
            {
                if (i == vIdx || (i + 1) % outer.Count == vIdx) continue;
                var a = outer[i];
                var b = outer[(i + 1) % outer.Count];
                if (SegmentsIntersectXZ(m, v, a, b)) return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════
        // 绕序工具
        // ═══════════════════════════════════════════════════════

        /// <summary>确保多边形为 CCW（逆时针）绕序（XZ 平面）</summary>
        private static void EnsureCCW(List<Vector3> poly)
        {
            if (ComputeSignedAreaXZ(poly) < 0f)
                poly.Reverse();
        }

        /// <summary>确保多边形为 CW（顺时针）绕序（XZ 平面）</summary>
        private static void EnsureCW(List<Vector3> poly)
        {
            if (ComputeSignedAreaXZ(poly) > 0f)
                poly.Reverse();
        }

        /// <summary>Shoelace 公式计算 XZ 平面有符号面积（正 = CCW）</summary>
        private static float ComputeSignedAreaXZ(List<Vector3> poly)
        {
            float area = 0f;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                area += (b.x - a.x) * (b.z + a.z);
            }
            return -area * 0.5f;  // 注意：Unity XZ 平面的 CCW 对应 area > 0
        }

        // ═══════════════════════════════════════════════════════
        // 几何工具（XZ 平面）
        // ═══════════════════════════════════════════════════════

        /// <summary>叉积（XZ 平面）：正值 = CCW，负值 = CW，0 = 共线</summary>
        private static float CrossXZ(Vector3 o, Vector3 a, Vector3 b)
            => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);

        /// <summary>点是否在三角形内（XZ 平面，含边界）</summary>
        private static bool IsPointInTriangleXZ(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d1 = CrossXZ(a, b, p);
            float d2 = CrossXZ(b, c, p);
            float d3 = CrossXZ(c, a, p);
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        /// <summary>两线段是否在 XZ 平面上相交（不含端点）</summary>
        private static bool SegmentsIntersectXZ(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            float d1 = CrossXZ(p3, p4, p1);
            float d2 = CrossXZ(p3, p4, p2);
            float d3 = CrossXZ(p1, p2, p3);
            float d4 = CrossXZ(p1, p2, p4);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;
            return false;
        }
    }
}
