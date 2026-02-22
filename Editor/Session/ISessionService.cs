#nullable enable

namespace SceneBlueprint.Editor.Session
{
    /// <summary>
    /// Session 托管服务接口。
    /// 所有注册到 <see cref="BlueprintEditorSession"/> 的服务都可实现此接口，
    /// 由 Session 统一驱动生命周期，避免 Dispose 遗漏。
    /// <para>
    /// 默认实现均为空，服务只需覆写感兴趣的方法。
    /// </para>
    /// </summary>
    internal interface ISessionService
    {
        /// <summary>Session 完成初始化、所有服务构造完毕后调用</summary>
        void OnSessionStarted() { }

        /// <summary>Session 被销毁前调用（对应 EditorWindow.OnDestroy 或 RecreateSession）</summary>
        void OnSessionDisposed() { }
    }
}
