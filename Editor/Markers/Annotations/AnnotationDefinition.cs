#nullable enable
using System;

namespace SceneBlueprint.Editor.Markers.Annotations
{
    /// <summary>
    /// 标注类型定义 — 描述一种标注"是什么、适用于什么 Marker、有什么默认值"。
    /// <para>
    /// 与 <see cref="Definitions.MarkerDefinition"/> 对称，是标注类型的元数据描述。
    /// 用于驱动编辑器 UI（Add Component 菜单过滤、Inspector 提示、位置生成工具的标注选项等）。
    /// </para>
    /// </summary>
    public class AnnotationDefinition
    {
        /// <summary>
        /// 标注类型 ID（如 "Spawn", "Camera", "Patrol"）。
        /// <para>对应 <see cref="Runtime.Markers.Annotations.MarkerAnnotation.AnnotationTypeId"/>。</para>
        /// </summary>
        public string TypeId { get; set; } = "";

        /// <summary>
        /// 编辑器中显示的名称（如 "刷怪点标注"）。
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 描述文本 — 在菜单悬停或 Inspector 提示中显示。
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 对应的 MonoBehaviour 组件类型（必须是 <see cref="Runtime.Markers.Annotations.MarkerAnnotation"/> 的具体子类）。
        /// </summary>
        public Type ComponentType { get; set; } = typeof(Runtime.Markers.Annotations.MarkerAnnotation);

        /// <summary>
        /// 适用的 Marker 类型 ID 列表。
        /// <para>
        /// null 或空数组表示适用所有 Marker 类型。
        /// 非空时，只有匹配的 Marker 类型才能添加此标注。
        /// 值对应 <see cref="Core.MarkerTypeIds"/> 中的常量。
        /// </para>
        /// </summary>
        public string[]? ApplicableMarkerTypes { get; set; }

        /// <summary>
        /// 是否允许同一 Marker 上挂多个同类标注。
        /// <para>默认 false（一个 Marker 只能有一个同类标注）。</para>
        /// </summary>
        public bool AllowMultiple { get; set; }

        /// <summary>
        /// 关联的 Tag 前缀（用于图层系统集成）。
        /// <para>如 "Combat"、"Camera"，可为 null。</para>
        /// </summary>
        public string? TagPrefix { get; set; }
    }
}
