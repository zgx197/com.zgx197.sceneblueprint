#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Geometry;

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
            var shape = area.GetShape();
            var rng = new System.Random(System.Environment.TickCount);
            var maxAttempts = count * 30;

            for (int i = 0; i < maxAttempts && positions.Count < count; i++)
            {
                var randomPos = shape.SampleRandomPoint(rng);

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
            var shape = area.GetShape();
            var rng = new System.Random(System.Environment.TickCount);

            for (int i = 0; i < count; i++)
                positions[i] = shape.SampleRandomPoint(rng);

            return positions;
        }

        /// <summary>
        /// 网格分布
        /// </summary>
        private static Vector3[] GenerateGrid(AreaMarker area, int count)
        {
            var positions = new List<Vector3>();
            var shape = area.GetShape();
            var bounds = shape.GetBounds();
            var rng = new System.Random(System.Environment.TickCount);

            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(count));
            float stepX = bounds.size.x / gridSize;
            float stepZ = bounds.size.z / gridSize;

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

                    if (shape.ContainsPointXZ(pos))
                        positions.Add(pos);
                }
            }

            // 网格点不够时，补充随机点
            while (positions.Count < count)
                positions.Add(shape.SampleRandomPoint(rng));

            return positions.ToArray();
        }
    }
}
