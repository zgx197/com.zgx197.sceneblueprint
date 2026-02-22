#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 屏幕警告数据（由 ShowWarningSystem 解析后传递给外部处理器）。
    /// </summary>
    public struct ShowWarningData
    {
        /// <summary>显示的文字内容</summary>
        public string Text;

        /// <summary>持续时间（秒）</summary>
        public float Duration;

        /// <summary>样式（Warning / Info / Boss）</summary>
        public string Style;

        /// <summary>字号</summary>
        public float FontSize;
    }

    /// <summary>
    /// 屏幕警告处理器接口——运行时解释器与 UI 表现层的桥梁。
    /// <para>
    /// ShowWarningSystem 解析节点属性后，通过此接口通知外部显示文字。
    /// 不同环境提供不同实现：
    /// - 编辑器测试场景：OnGUI 绘制（ShowWarningHandler）
    /// - 正式运行时：接入游戏 UI 框架
    /// </para>
    /// </summary>
    public interface IShowWarningHandler
    {
        /// <summary>显示屏幕警告文字</summary>
        void OnShow(ShowWarningData data);

        /// <summary>立即隐藏（提前中断时调用）</summary>
        void OnHide() { }
    }
}
