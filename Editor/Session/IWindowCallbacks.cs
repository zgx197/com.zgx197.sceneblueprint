#nullable enable
using UnityEngine;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.Session
{
    /// <summary>
    /// Session 向 EditorWindow 注入的副作用回调接口。
    /// <para>
    /// 让 <see cref="BlueprintEditorSession"/> 不持有 EditorWindow 引用，
    /// 仅通过此接口触发窗口级副作用，从而支持在无窗口的测试环境中构造 Session。
    /// </para>
    /// </summary>
    internal interface IWindowCallbacks
    {
        /// <summary>触发窗口重绘（EditorWindow.Repaint）</summary>
        void Repaint();

        /// <summary>获取当前窗口尺寸（用于画布坐标计算）</summary>
        Vector2 GetWindowSize();

        /// <summary>确保工作台面板可见（Analysis 失败时打开黑板面板）</summary>
        void EnsureWorkbenchVisible();

        /// <summary>通知窗口记录最近一次导出时间</summary>
        void SetExportTime(string exportTime);

        /// <summary>通知窗口更新标题栏文字</summary>
        void SetTitle(string title);
    }
}
