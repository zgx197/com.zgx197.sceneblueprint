#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers.Annotations
{
    /// <summary>
    /// 标记标注组件基类 — 附加在 SceneMarker 的 GameObject 上，
    /// 为空间标记提供额外的语义属性。
    /// <para>
    /// 设计原则：
    /// - Marker 是纯空间标记（position/rotation/shape）
    /// - Annotation 是空间标注（monsterId/behavior/cameraFOV）
    /// - 两者都属于 SceneView 层，不是 Blueprint 层
    /// - 一个 Marker 可以挂多个不同类型的 Annotation
    /// - 导出时，Annotation 数据和 Marker 空间数据合并写入 Playbook
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SceneMarker))]
    public abstract class MarkerAnnotation : MonoBehaviour
    {
        /// <summary>
        /// 标注类型 ID（如 "Spawn", "Camera", "Patrol"）。
        /// <para>用于导出时按类型分组标注数据，以及注册表查询。</para>
        /// </summary>
        public abstract string AnnotationTypeId { get; }

        /// <summary>
        /// 收集导出数据 — 导出器调用此方法，将标注属性写入字典。
        /// <para>
        /// key 是属性名，value 是可序列化的值（string/int/float/bool 等）。
        /// 导出器不需要知道每种标注的具体字段，统一通过此方法收集。
        /// </para>
        /// </summary>
        public abstract void CollectExportData(IDictionary<string, object> data);

        /// <summary>
        /// 获取所在的 SceneMarker（缓存，避免重复 GetComponent）。
        /// </summary>
        public SceneMarker Marker => _marker != null ? _marker : (_marker = GetComponent<SceneMarker>());
        private SceneMarker? _marker;

        // ── Gizmo 装饰（可选覆盖，由 GizmoRenderPipeline 的 Decoration 阶段调用）──

        /// <summary>
        /// 是否提供自定义 Gizmo 装饰。
        /// <para>
        /// 返回 true 时，管线会在 Marker 基础 Gizmo 之上调用
        /// <see cref="DrawGizmoDecoration"/>。默认 false。
        /// </para>
        /// </summary>
        public virtual bool HasGizmoDecoration => false;

        /// <summary>
        /// 绘制 Gizmo 装饰。在 Marker 的基础 Gizmo（球体/箭头）之后调用。
        /// <para>
        /// 仅在编辑器中有效。具体绘制逻辑由子类实现，
        /// 可使用 UnityEditor.Handles API（需要 #if UNITY_EDITOR 保护）。
        /// </para>
        /// </summary>
        /// <param name="isSelected">当前 Marker 是否被选中</param>
        public virtual void DrawGizmoDecoration(bool isSelected) { }

        /// <summary>
        /// 覆盖 Marker 的 Gizmo 颜色。返回 null 表示不覆盖，使用默认颜色逻辑。
        /// <para>
        /// 颜色优先级：Marker.UseCustomGizmoColor > Annotation 覆盖 > 图层色 > 默认色
        /// </para>
        /// </summary>
        public virtual Color? GetGizmoColorOverride() => null;
    }
}
