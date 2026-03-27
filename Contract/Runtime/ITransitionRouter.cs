#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 事件路由接口——System 通过此接口发射端口事件。
    /// <para>
    /// System 调用时始终传入 string portId（保持可读性），
    /// 实现内部负责查出边表、hash 转换、生成 PortEvent 写入 PendingEvents。
    /// </para>
    /// <para>
    /// Package 侧实现从 BlueprintFrame.Transitions 查表；
    /// mini_game 侧实现从 SBBlueprintData.OutgoingTransitions 查表。
    /// </para>
    /// </summary>
    public interface ITransitionRouter
    {
        /// <summary>
        /// 发射指定端口事件。
        /// 实现内部查出边表并生成 PortEvent（int hash）写入 FrameView.PendingEvents。
        /// 同时自动标记 States[actionIdx].EventEmitted = true。
        /// </summary>
        void EmitFlowEvent(ref FrameView view, int actionIndex, string portId);

        /// <summary>EmitFlowEvent 的 "out" 端口快捷方式</summary>
        void EmitOutEvent(ref FrameView view, int actionIndex);
    }
}
