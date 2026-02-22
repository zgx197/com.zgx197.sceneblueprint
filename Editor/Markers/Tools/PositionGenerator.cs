#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.ToolKit
{
    /// <summary>
    /// 位置生成策略枚举
    /// </summary>
    public enum PositionGenerationStrategy
    {
        /// <summary>随机分布（Poisson Disc Sampling，保持最小间距）</summary>
        Random,
        /// <summary>圆形阵型（均匀分布在圆周上）</summary>
        Circle
    }

    /// <summary>
    /// 生成的位置数据（位置 + 朝向）
    /// </summary>
    public struct GeneratedPosition
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>
    /// 位置生成器——在 AreaMarker 区域内生成一组位置点。
    /// <para>
    /// 纯算法类，不依赖 Editor API。
    /// 支持策略：随机（Poisson Disc）、圆形阵型。
    /// 用于编辑器侧的"位置生成工具"，产物最终固化为 PointMarker。
    /// </para>
    /// </summary>
    public static class PositionGenerator
    {
        /// <summary>
        /// 根据策略生成位置列表
        /// </summary>
        /// <param name="area">目标区域</param>
        /// <param name="count">生成数量</param>
        /// <param name="strategy">生成策略</param>
        /// <param name="minSpacing">最小间距（随机策略使用）</param>
        /// <param name="seed">随机种子（0 = 使用当前时间）</param>
        /// <returns>生成的位置列表</returns>
        public static GeneratedPosition[] Generate(
            AreaMarker area,
            int count,
            PositionGenerationStrategy strategy,
            float minSpacing = 2f,
            int seed = 0)
        {
            if (area == null || count <= 0)
                return System.Array.Empty<GeneratedPosition>();

            // 使用确定性随机种子，方便 re-random 时换种子
            var rng = seed == 0
                ? new System.Random(System.Environment.TickCount)
                : new System.Random(seed);

            return strategy switch
            {
                PositionGenerationStrategy.Random => GenerateRandom(area, count, minSpacing, rng),
                PositionGenerationStrategy.Circle => GenerateCircle(area, count, rng),
                _ => GenerateRandom(area, count, minSpacing, rng)
            };
        }

        /// <summary>
        /// 随机分布（Poisson Disc Sampling 简化版）
        /// </summary>
        private static GeneratedPosition[] GenerateRandom(
            AreaMarker area, int count, float minSpacing, System.Random rng)
        {
            var positions = new List<GeneratedPosition>();
            var bounds = GetAreaBounds(area);
            int maxAttempts = count * 30;

            for (int i = 0; i < maxAttempts && positions.Count < count; i++)
            {
                var randomPos = GetRandomPointInArea(area, bounds, rng);

                // 检查与已有点的距离
                bool tooClose = false;
                foreach (var existing in positions)
                {
                    if (Vector3.Distance(randomPos, existing.Position) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    // 随机朝向（Y 轴旋转）
                    float yAngle = (float)(rng.NextDouble() * 360.0);
                    positions.Add(new GeneratedPosition
                    {
                        Position = randomPos,
                        Rotation = Quaternion.Euler(0, yAngle, 0)
                    });
                }
            }

            return positions.ToArray();
        }

        /// <summary>
        /// 圆形阵型——在区域中心画圆，均匀分布
        /// </summary>
        private static GeneratedPosition[] GenerateCircle(
            AreaMarker area, int count, System.Random rng)
        {
            var center = area.GetRepresentativePosition();
            float radius = GetAreaRadius(area) * 0.7f; // 留 30% 边距

            if (radius < 0.5f) radius = 0.5f;

            var positions = new GeneratedPosition[count];

            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * Mathf.PI * 2f;
                var pos = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );

                // 朝向圆心
                var lookDir = center - pos;
                lookDir.y = 0;
                var rotation = lookDir.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(lookDir)
                    : Quaternion.identity;

                positions[i] = new GeneratedPosition
                {
                    Position = pos,
                    Rotation = rotation
                };
            }

            return positions;
        }

        /// <summary>
        /// 获取区域的等效半径
        /// </summary>
        private static float GetAreaRadius(AreaMarker area)
        {
            if (area.Shape == AreaShape.Box)
            {
                return Mathf.Min(area.BoxSize.x, area.BoxSize.z) * 0.5f;
            }
            else
            {
                var verts = area.Vertices;
                if (verts.Count == 0) return 2f;
                float maxR = 0;
                foreach (var v in verts)
                    maxR = Mathf.Max(maxR, new Vector2(v.x, v.z).magnitude);
                return maxR;
            }
        }

        /// <summary>
        /// 获取区域的 Bounds
        /// </summary>
        private static Bounds GetAreaBounds(AreaMarker area)
        {
            if (area.Shape == AreaShape.Box)
            {
                return area.GetWorldBounds();
            }
            else
            {
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
                return new Bounds((min + max) * 0.5f, max - min);
            }
        }

        /// <summary>
        /// 在区域内生成一个随机点
        /// </summary>
        private static Vector3 GetRandomPointInArea(AreaMarker area, Bounds bounds, System.Random rng)
        {
            const int maxAttempts = 100;

            for (int i = 0; i < maxAttempts; i++)
            {
                var randomPos = new Vector3(
                    bounds.min.x + (float)rng.NextDouble() * bounds.size.x,
                    bounds.center.y,
                    bounds.min.z + (float)rng.NextDouble() * bounds.size.z
                );

                if (IsPointInArea(area, randomPos))
                    return randomPos;
            }

            return area.GetRepresentativePosition();
        }

        /// <summary>
        /// 检查点是否在区域内
        /// </summary>
        private static bool IsPointInArea(AreaMarker area, Vector3 point)
        {
            if (area.Shape == AreaShape.Box)
            {
                return area.GetWorldBounds().Contains(point);
            }
            else
            {
                var verts = area.GetWorldVertices();
                if (verts.Count < 3) return false;

                // 射线法
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
