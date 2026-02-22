#nullable enable

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// UI 副作用接口——服务类触发窗口级 UI 操作。
    /// </summary>
    public interface IBlueprintUIContext
    {
        /// <summary>请求编辑器窗口重绘</summary>
        void RequestRepaint();

        /// <summary>确保工作台面板可见（分析失败时打开面板）</summary>
        void EnsureWorkbenchVisible();
    }
}
