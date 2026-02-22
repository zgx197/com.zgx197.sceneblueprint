#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Preview
{
    /// <summary>
    /// 位置分布算法集合——用于在区域内生成随机/网格/泊松分布的位置点。
    /// <para>
    /// 支持的算法：
    /// - Random: 完全随机分布
    /// - Poisson: Poisson Disc Sampling（避免聚团，保持最小间距）
    /// - Grid: 网格分布
    /// </para>
    /// </summary>
    public static class DistributionAlgorithms
    {
        /// <summary>
        /// 生成位置分布
        /// </summary>
        /// <param name="area">区域 Marker</param>
        /// <param name="count">位置数量</param>
        /// <param name="algorithm">算法名称（Random/Poisson/Grid）</param>
        /// <param name="minSpacing">最小间距（仅 Poisson 算法使用）</param>
        /// <returns>位置列表</returns>
        public static Vector3[] Generate(
            AreaMarker area,
            int count,
            string algorithm,
            float minSpacing)
        {
            if (area == null)
                throw new System.ArgumentNullException(nameof(area));

            if (count <= 0)
                return System.Array.Empty<Vector3>();

            return algorithm switch
            {
                "Poisson" => GeneratePoisson(area, count, minSpacing),
                "Grid" => GenerateGrid(area, count),
                "Random" => GenerateRandom(area, count),
                _ => GenerateRandom(area, count)
            };
        }

        /// <summary>
        /// Poisson Disc Sampling - 保持最小间距的随机分布
        /// </summary>
        private static Vector3[] GeneratePoisson(AreaMarker area, int count, float minSpacing)
        {
            var positions = new List<Vector3>();
            var bounds = GetAreaBounds(area);
            var maxAttempts = count * 30; // 最大尝试次数

            for (int i = 0; i < maxAttempts && positions.Count < count; i++)
            {
                var randomPos = GetRandomPointInArea(area, bounds);

                // 检查与已有点的距离
                bool tooClose = false;
                foreach (var existing in positions)
                {
                    if (Vector3.Distance(randomPos, existing) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    positions.Add(randomPos);
                }
            }

            // 如果无法生成足够的点，警告并返回已生成的
            if (positions.Count < count)
            {
                UnityEngine.Debug.LogWarning($"[DistributionAlgorithms] Poisson 算法只能生成 {positions.Count}/{count} 个点（可能是 minSpacing={minSpacing} 过大或区域太小）");
            }

            return positions.ToArray();
        }

        /// <summary>
        /// 完全随机分布
        /// </summary>
        private static Vector3[] GenerateRandom(AreaMarker area, int count)
        {
            var positions = new Vector3[count];
            var bounds = GetAreaBounds(area);

            for (int i = 0; i < count; i++)
            {
                positions[i] = GetRandomPointInArea(area, bounds);
            }

            return positions;
        }

        /// <summary>
        /// 网格分布
        /// </summary>
        private static Vector3[] GenerateGrid(AreaMarker area, int count)
        {
            var positions = new List<Vector3>();
            var bounds = GetAreaBounds(area);

            // 计算网格尺寸
            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(count));
            float stepX = bounds.size.x / gridSize;
            float stepZ = bounds.size.z / gridSize;

            // 生成网格点
            for (int x = 0; x < gridSize; x++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    if (positions.Count >= count) break;

                    var pos = bounds.min + new Vector3(
                        stepX * (x + 0.5f),
                        0,
                        stepZ * (z + 0.5f)
                    );

                    // 检查点是否在区域内
                    if (IsPointInArea(area, pos))
                    {
                        positions.Add(pos);
                    }
                }
            }

            // 如果网格点不够，补充随机点
            while (positions.Count < count)
            {
                var randomPos = GetRandomPointInArea(area, bounds);
                positions.Add(randomPos);
            }

            return positions.ToArray();
        }

        /// <summary>
        /// 在区域内生成一个随机点
        /// </summary>
        private static Vector3 GetRandomPointInArea(AreaMarker area, Bounds bounds)
        {
            const int maxAttempts = 100;
            
            for (int i = 0; i < maxAttempts; i++)
            {
                // 在 Bounds 内随机一个点
                var randomPos = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    bounds.center.y, // 使用区域中心高度
                    Random.Range(bounds.min.z, bounds.max.z)
                );

                // 检查是否在区域内（Box 总是 true，Polygon 需要检测）
                if (IsPointInArea(area, randomPos))
                {
                    return randomPos;
                }
            }

            // 失败时返回区域中心
            return area.GetRepresentativePosition();
        }

        /// <summary>
        /// 获取区域的 Bounds（兼容 Box 和 Polygon）
        /// </summary>
        private static Bounds GetAreaBounds(AreaMarker area)
        {
            if (area.Shape == AreaShape.Box)
            {
                return area.GetWorldBounds();
            }
            else
            {
                // Polygon 模式：计算包围盒
                var verts = area.GetWorldVertices();
                if (verts.Count == 0)
                    return new Bounds(area.transform.position, Vector3.one * 5f);

                var min = verts[0];
                var max = verts[0];
                foreach (var v in verts)
                {
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
                var center = (min + max) * 0.5f;
                var size = max - min;
                return new Bounds(center, size);
            }
        }

        /// <summary>
        /// 检查点是否在区域内
        /// </summary>
        private static bool IsPointInArea(AreaMarker area, Vector3 point)
        {
            if (area.Shape == AreaShape.Box)
            {
                var bounds = area.GetWorldBounds();
                return bounds.Contains(point);
            }
            else
            {
                // Polygon 模式：简化的点在多边形内判断（2D XZ 平面）
                var verts = area.GetWorldVertices();
                if (verts.Count < 3) return false;

                // 射线法判断点是否在多边形内
                int intersections = 0;
                for (int i = 0; i < verts.Count; i++)
                {
                    var v1 = verts[i];
                    var v2 = verts[(i + 1) % verts.Count];

                    if ((v1.z > point.z) != (v2.z > point.z))
                    {
                        float x = v1.x + (point.z - v1.z) / (v2.z - v1.z) * (v2.x - v1.x);
                        if (point.x < x)
                            intersections++;
                    }
                }
                return (intersections % 2) == 1;
            }
        }
    }
}
