#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Annotations
{
    /// <summary>
    /// 摄像机标注 — 标记该位置的摄像机参数。
    /// <para>
    /// 挂在 PointMarker 的 GameObject 上，为空间点附加摄像机配置信息。
    /// 典型用途：过场镜头位、Boss 出场特写机位、剧情对话机位。
    /// </para>
    /// <para>
    /// 设计原则：
    /// - CameraAnnotation 属于 SceneView 层（空间标注），不是 Blueprint 层
    /// - "这个点位是一个 FOV=60 的特写镜头"是策划在场景中做的标注
    /// - Blueprint 只引用 Marker ID，不存储摄像机配置
    /// - 导出时合并：PointMarker 空间数据 + CameraAnnotation 标注数据 → Playbook 条目
    /// </para>
    /// </summary>
    [AddComponentMenu("SceneBlueprint/Annotations/Camera Annotation")]
    public class CameraAnnotation : MarkerAnnotation
    {
        public override string AnnotationTypeId => "Camera";

        [Header("摄像机参数")]

        [Tooltip("视野角度（Field of View）")]
        [Range(10f, 120f)]
        public float FOV = 60f;

        [Tooltip("镜头切换过渡时长（秒）")]
        [Min(0f)]
        public float TransitionDuration = 0.5f;

        [Header("缓动")]

        [Tooltip("过渡缓动曲线类型")]
        public CameraEasing Easing = CameraEasing.EaseInOut;

        [Tooltip("是否在到达后锁定视角（禁止玩家旋转）")]
        public bool LockRotation;

        /// <summary>
        /// 收集导出数据。导出器调用此方法将摄像机配置写入 Playbook。
        /// </summary>
        public override void CollectExportData(IDictionary<string, object> data)
        {
            data["fov"] = FOV;
            data["transitionDuration"] = TransitionDuration;
            data["easing"] = Easing.ToString();
            data["lockRotation"] = LockRotation;
        }

        // ── Gizmo 装饰 ──

        public override bool HasGizmoDecoration => true;

        public override Color? GetGizmoColorOverride()
        {
            return new Color(0.2f, 0.8f, 0.3f); // 绿色，与 GizmoStyleConstants.CameraColor 一致
        }

        public override void DrawGizmoDecoration(bool isSelected)
        {
#if UNITY_EDITOR
            var t = transform;
            var pos = t.position;
            var forward = t.forward;
            var up = t.up;
            var right = t.right;

            // 视锥参数
            float nearDist = 0.5f;
            float farDist = isSelected ? 3f : 2f;
            float aspect = 16f / 9f;
            float halfFovRad = FOV * 0.5f * Mathf.Deg2Rad;
            float halfHeightFar = farDist * Mathf.Tan(halfFovRad);
            float halfWidthFar = halfHeightFar * aspect;
            float halfHeightNear = nearDist * Mathf.Tan(halfFovRad);
            float halfWidthNear = halfHeightNear * aspect;

            // 近平面四角
            var nearCenter = pos + forward * nearDist;
            var ntl = nearCenter + up * halfHeightNear - right * halfWidthNear;
            var ntr = nearCenter + up * halfHeightNear + right * halfWidthNear;
            var nbr = nearCenter - up * halfHeightNear + right * halfWidthNear;
            var nbl = nearCenter - up * halfHeightNear - right * halfWidthNear;

            // 远平面四角
            var farCenter = pos + forward * farDist;
            var ftl = farCenter + up * halfHeightFar - right * halfWidthFar;
            var ftr = farCenter + up * halfHeightFar + right * halfWidthFar;
            var fbr = farCenter - up * halfHeightFar + right * halfWidthFar;
            var fbl = farCenter - up * halfHeightFar - right * halfWidthFar;

            var wireColor = new Color(0.2f, 0.8f, 0.3f, isSelected ? 0.9f : 0.5f);
            var fillColor = new Color(0.2f, 0.8f, 0.3f, isSelected ? 0.08f : 0.04f);

            UnityEditor.Handles.color = wireColor;

            // 近平面
            UnityEditor.Handles.DrawLine(ntl, ntr);
            UnityEditor.Handles.DrawLine(ntr, nbr);
            UnityEditor.Handles.DrawLine(nbr, nbl);
            UnityEditor.Handles.DrawLine(nbl, ntl);

            // 远平面
            UnityEditor.Handles.DrawLine(ftl, ftr);
            UnityEditor.Handles.DrawLine(ftr, fbr);
            UnityEditor.Handles.DrawLine(fbr, fbl);
            UnityEditor.Handles.DrawLine(fbl, ftl);

            // 连接线（近→远）
            UnityEditor.Handles.DrawLine(ntl, ftl);
            UnityEditor.Handles.DrawLine(ntr, ftr);
            UnityEditor.Handles.DrawLine(nbr, fbr);
            UnityEditor.Handles.DrawLine(nbl, fbl);

            // 半透明填充（远平面）
            UnityEditor.Handles.color = fillColor;
            UnityEditor.Handles.DrawAAConvexPolygon(ftl, ftr, fbr, fbl);

            // FOV 标签
            var labelPos = pos + Vector3.up * 1.2f;
            var labelColor = new Color(0.2f, 0.9f, 0.4f);
            var style = new GUIStyle(UnityEditor.EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                richText = false,
            };
            style.normal.textColor = labelColor;
            var label = $"FOV:{FOV:F0} T:{TransitionDuration:F1}s";
            UnityEditor.Handles.Label(labelPos, label, style);
#endif
        }
    }

    /// <summary>
    /// 摄像机过渡缓动类型。
    /// </summary>
    public enum CameraEasing
    {
        /// <summary>线性</summary>
        Linear,
        /// <summary>缓入</summary>
        EaseIn,
        /// <summary>缓出</summary>
        EaseOut,
        /// <summary>缓入缓出</summary>
        EaseInOut
    }
}
