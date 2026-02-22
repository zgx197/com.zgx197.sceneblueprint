#nullable enable

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// Gizmo 绘制阶段枚举。
    /// <para>
    /// 管线按枚举值从小到大的顺序执行各阶段，确保：
    /// - 半透明填充在最底层
    /// - 线框和图标在中间
    /// - 编辑 Handle 在选中时绘制
    /// - 高亮效果叠加在图形之上
    /// - 文字标签始终在最上层
    /// - 拾取不产生任何视觉输出
    /// </para>
    /// </summary>
    public enum DrawPhase
    {
        /// <summary>Phase 0: 半透明填充面（最先绘制，不遮挡后续内容）</summary>
        Fill = 0,

        /// <summary>Phase 1: 线框、边框、方向箭头</summary>
        Wireframe = 1,

        /// <summary>Phase 2: 实心球体、菱形等图标图形</summary>
        Icon = 2,

        /// <summary>Phase 3: 选中时的编辑 Handle（拖拽顶点、Box Handle 等）</summary>
        Interactive = 3,

        /// <summary>Phase 4: 高亮脉冲、外扩光晕（蓝图联动叠加层）</summary>
        Highlight = 4,

        /// <summary>Phase 5: 文字标签（始终最上层）</summary>
        Label = 5,

        /// <summary>Phase 6: 不可见拾取区域（不绘制，只检测点击）</summary>
        Pick = 6,
    }
}
