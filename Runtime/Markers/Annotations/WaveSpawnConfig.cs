#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Annotations
{
    /// <summary>
    /// 波次刷怪配置标注 — 挂在 AreaMarker 上，描述该区域能刷出什么怪（怪物池）。
    /// <para>
    /// 职责拆分后的设计：
    /// - WaveSpawnConfig（SceneView 层）：只描述"这个区域能刷什么怪"（怪物池 + 空间设置）
    /// - Spawn.Wave 节点（Blueprint 层）：描述"怎么刷"（波次数量、间隔、筛选）
    /// </para>
    /// <para>
    /// 配合 Blueprint 中的 Spawn.Wave 节点使用：
    /// 节点绑定带有 WaveSpawnConfig 的 AreaMarker，导出时收集区域几何 + 此标注数据。
    /// 运行时 SpawnWaveSystem 根据 Spawn.Wave 节点的波次配置，从怪物池中按标签筛选、按权重抽取。
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Annotations/Wave Spawn Config")]
    public class WaveSpawnConfig : MarkerAnnotation
    {
        public override string AnnotationTypeId => "WaveSpawn";

        /// <summary>怪物分类标签（用于波次筛选）</summary>
        public enum MonsterTag
        {
            Normal,     // 普通怪
            Elite,      // 精英怪
            Boss,       // Boss
            Minion,     // 小兵/召唤物
            Special     // 特殊（自定义用途）
        }

        [Serializable]
        public struct MonsterEntry
        {
            [Tooltip("怪物模板 ID")]
            public string monsterId;

            [Tooltip("怪物等级")]
            [Range(1, 100)]
            public int level;

            [Tooltip("初始行为")]
            public InitialBehavior behavior;

            [Tooltip("警戒半径（仅 Guard 生效）")]
            [Min(0.5f)]
            public float guardRadius;

            [Tooltip("分类标签（用于波次筛选，如 Normal / Elite / Boss）")]
            public MonsterTag tag;

            [Tooltip("权重（同标签内随机抽取时的权重）")]
            [Range(1, 100)]
            public int weight;
        }

        [Header("怪物池")]
        [Tooltip("该区域可刷出的怪物列表")]
        public MonsterEntry[] Monsters = Array.Empty<MonsterEntry>();

        [Header("空间设置")]
        [Tooltip("怪物之间的最小间距")]
        [Min(0.5f)]
        public float MinSpacing = 1.5f;

        // ─── 已移除的字段（职责拆分，移至 Spawn.Wave 节点） ───
        // WaveCount        → Spawn.Wave 节点的 Waves[] 属性
        // WaveIntervalTicks → Spawn.Wave 节点的 Waves[].intervalTicks
        // MonsterEntry.count → Spawn.Wave 节点的 Waves[].count

        public override void CollectExportData(IDictionary<string, object> data)
        {
            data["monsters"] = JsonUtility.ToJson(new MonsterListWrapper { items = Monsters });
            data["minSpacing"] = MinSpacing;
        }

        /// <summary>JsonUtility 序列化包装器（顶层必须是 class/struct）</summary>
        [Serializable]
        public struct MonsterListWrapper
        {
            public MonsterEntry[] items;
        }

        // ── Gizmo 装饰 ──

        public override bool HasGizmoDecoration => Monsters.Length > 0;

        public override Color? GetGizmoColorOverride()
        {
            return Monsters.Length > 0
                ? new Color(0.8f, 0.4f, 0.1f) // 橙色，区分于预设怪的红色
                : null;
        }

        public override void DrawGizmoDecoration(bool isSelected)
        {
#if UNITY_EDITOR
            if (Monsters.Length == 0) return;

            // 统计各标签的怪物数量
            int normalCount = 0, eliteCount = 0, bossCount = 0, minionCount = 0, specialCount = 0;
            foreach (var m in Monsters)
            {
                switch (m.tag)
                {
                    case MonsterTag.Normal: normalCount++; break;
                    case MonsterTag.Elite: eliteCount++; break;
                    case MonsterTag.Boss: bossCount++; break;
                    case MonsterTag.Minion: minionCount++; break;
                    case MonsterTag.Special: specialCount++; break;
                }
            }

            var labelPos = transform.position + Vector3.up * 2.5f;
            var parts = new System.Collections.Generic.List<string>();
            if (normalCount > 0) parts.Add($"普通x{normalCount}");
            if (eliteCount > 0) parts.Add($"精英x{eliteCount}");
            if (bossCount > 0) parts.Add($"Bossx{bossCount}");
            if (minionCount > 0) parts.Add($"小兵x{minionCount}");
            if (specialCount > 0) parts.Add($"特殊x{specialCount}");
            var label = $"怪物池 ({string.Join(", ", parts)})";
            var labelColor = new Color(1f, 0.6f, 0.2f);

            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
            };
            style.normal.textColor = labelColor;
            UnityEditor.Handles.Label(labelPos, label, style);
#endif
        }
    }
}
