#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Annotations
{
    /// <summary>
    /// 刷怪点标注 — 标记该位置要生成什么怪物。
    /// <para>
    /// 挂在 PointMarker 的 GameObject 上，为空间点附加怪物配置信息。
    /// 配合 Blueprint 中的 Spawn.Preset 节点使用：
    /// 节点绑定 AreaMarker 或 PointMarker，导出时自动收集 SpawnAnnotation 数据。
    /// </para>
    /// <para>
    /// 设计原则：
    /// - SpawnAnnotation 属于 SceneView 层（空间标注），不是 Blueprint 层
    /// - "这个点位放骷髅战士"是策划在场景中做的标注，和"这个区域叫 Boss 房间"同层次
    /// - Blueprint 只引用 Marker ID，不存储怪物配置
    /// - 导出时合并：PointMarker 空间数据 + SpawnAnnotation 标注数据 → Playbook 条目
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Annotations/Spawn Annotation")]
    public class SpawnAnnotation : MarkerAnnotation
    {
        public override string AnnotationTypeId => "Spawn";

        [Header("怪物配置")]

        [Tooltip("怪物模板 ID（对应运行时的怪物表）")]
        public string MonsterId = "";

        [Tooltip("怪物等级")]
        [Range(1, 100)]
        public int Level = 1;

        [Header("初始行为")]

        [Tooltip("生成后的初始 AI 行为模式")]
        public InitialBehavior Behavior = InitialBehavior.Idle;

        [Tooltip("警戒半径（仅 Guard 模式下生效）")]
        [Min(0.5f)]
        public float GuardRadius = 5f;

        /// <summary>
        /// 收集导出数据。导出器调用此方法将怪物配置写入 Playbook。
        /// </summary>
        public override void CollectExportData(IDictionary<string, object> data)
        {
            data["monsterId"] = MonsterId;
            data["level"] = Level;
            data["behavior"] = Behavior.ToString();
            if (Behavior == InitialBehavior.Guard)
                data["guardRadius"] = GuardRadius;
        }

        // ── Gizmo 装饰 ──

        public override bool HasGizmoDecoration => !string.IsNullOrEmpty(MonsterId);

        public override Color? GetGizmoColorOverride()
        {
            // 有怪物配置的点位用红色，便于策划在 SceneView 中一眼区分
            return string.IsNullOrEmpty(MonsterId)
                ? null
                : new Color(0.9f, 0.2f, 0.2f);
        }

        public override void DrawGizmoDecoration(bool isSelected)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(MonsterId)) return;

            // 在 PointMarker 上方绘制怪物 ID + 等级标签
            var labelPos = transform.position + Vector3.up * 1.2f;
            var label = $"{MonsterId} Lv.{Level}";
            var labelColor = new Color(1f, 0.85f, 0.2f); // 金黄色标签

            var style = new GUIStyle(UnityEditor.EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                richText = false,
            };
            style.normal.textColor = labelColor;
            UnityEditor.Handles.Label(labelPos, label, style);

            // Guard 模式下绘制警戒范围半透明圆盘
            if (Behavior == InitialBehavior.Guard && GuardRadius > 0)
            {
                UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, isSelected ? 0.2f : 0.1f);
                UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, GuardRadius);
                UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.4f);
                UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, GuardRadius);
            }
#endif
        }
    }
}
