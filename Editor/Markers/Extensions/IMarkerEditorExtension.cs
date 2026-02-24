#nullable enable
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Extensions
{
    /// <summary>
    /// Marker 编辑器扩展接口 — 业务层通过实现此接口为特定 Marker 添加编辑器工具。
    /// <para>
    /// 设计目标：
    /// - 框架层的 Marker Inspector 保持通用性，不包含业务特定的编辑器工具
    /// - 业务层通过此接口注册自定义工具（如位置生成器、路径编辑器等）
    /// - 扩展工具在 Inspector 中自动显示，无需修改框架层代码
    /// </para>
    /// <para>
    /// 使用方式：
    /// 1. 实现此接口并添加 [MarkerEditorExtension("MarkerTypeId")] 特性
    /// 2. MarkerEditorExtensionRegistry.AutoDiscover() 自动发现并注册
    /// 3. SceneMarkerEditor 在 Inspector 中自动调用 DrawInspectorGUI()
    /// </para>
    /// </summary>
    public interface IMarkerEditorExtension
    {
        /// <summary>
        /// 适用的 Marker 类型 ID（如 "PresetSpawnArea"、"PatrolRoute"）。
        /// <para>
        /// 必须与 SceneMarker.MarkerTypeId 匹配，框架层根据此值过滤适用的扩展。
        /// </para>
        /// </summary>
        string TargetMarkerTypeId { get; }

        /// <summary>
        /// 工具显示名称（用于 Inspector 中的折叠组标题）。
        /// <para>
        /// 示例："刷怪点生成工具"、"巡逻路径编辑器"
        /// </para>
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 在 Inspector 中绘制工具 UI。
        /// <para>
        /// 框架层在 SceneMarkerEditor.OnInspectorGUI() 中调用此方法，
        /// 在 Marker 默认属性之后、其他扩展工具之前绘制。
        /// </para>
        /// </summary>
        /// <param name="marker">当前正在编辑的 Marker 实例</param>
        void DrawInspectorGUI(SceneMarker marker);

        /// <summary>
        /// 工具是否适用于当前 Marker（可选的额外过滤逻辑）。
        /// <para>
        /// 默认实现：只要 marker.MarkerTypeId == TargetMarkerTypeId 即适用。
        /// 如果需要更复杂的过滤条件（如检查特定组件、Tag 等），可重写此方法。
        /// </para>
        /// </summary>
        /// <param name="marker">当前 Marker 实例</param>
        /// <returns>true 表示工具适用，会调用 DrawInspectorGUI；false 则跳过</returns>
        bool IsApplicable(SceneMarker marker) => marker.MarkerTypeId == TargetMarkerTypeId;
    }
}
