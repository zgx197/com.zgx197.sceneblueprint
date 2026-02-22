#nullable enable
using System;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Definitions
{
    /// <summary>
    /// 标记类型定义——描述一种标记"是什么、怎么创建、创建后怎么初始化"。
    /// <para>
    /// 与 <see cref="Core.ActionDefinition"/> 对称，是标记类型的元数据描述。
    /// Editor 层根据此定义自动创建标记、计算布局间距等。
    /// </para>
    /// </summary>
    public class MarkerDefinition
    {
        /// <summary>
        /// 标记类型 ID——全局唯一，对应 <see cref="Core.MarkerTypeIds"/> 中的常量。
        /// <para>如 "Point", "Area", "Entity", "Path"</para>
        /// </summary>
        public string TypeId { get; set; } = "";

        /// <summary>
        /// 编辑器中显示的名称。
        /// <para>如 "点标记", "区域标记", "实体标记"</para>
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 描述文本——在菜单悬停时显示。
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 对应的 SceneMarker 组件类型（必须是 <see cref="SceneMarker"/> 的具体子类）。
        /// <para>如 typeof(PointMarker), typeof(AreaMarker)</para>
        /// </summary>
        public Type ComponentType { get; set; } = typeof(SceneMarker);

        /// <summary>
        /// 在自动创建多个标记时，相邻标记之间的间距（世界单位）。
        /// </summary>
        public float DefaultSpacing { get; set; } = 2f;

        /// <summary>
        /// 创建标记后的初始化回调（可选）。
        /// <para>
        /// 用于设置类型特有的默认值（如 AreaMarker 的 BoxSize、Shape 等）。
        /// 参数为刚创建的 SceneMarker 实例。
        /// </para>
        /// </summary>
        public Action<SceneMarker>? Initializer { get; set; }
    }
}
