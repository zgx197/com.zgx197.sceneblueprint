#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 信号观察者接口——监听 SignalSystem 内部的关键事件，用于调试、历史记录和可视化。
    /// <para>
    /// 通过 <c>BlueprintRunner.RegisterService&lt;ISignalObserver&gt;()</c> 注入。
    /// SignalSystem 在各关键路径调用对应方法，实现者可记录/转发/分析信号流。
    /// </para>
    /// <para>
    /// 所有方法均为可选通知——实现者不应在回调中抛出异常，否则会中断蓝图执行。
    /// </para>
    /// </summary>
    public interface ISignalObserver
    {
        /// <summary>蓝图节点向外部发射信号（Signal.Emit 节点执行时）</summary>
        void OnSignalEmitted(int actionIndex, SignalTag tag, SignalPayload payload);

        /// <summary>外部系统向蓝图注入信号（ISignalBridge.Send 或 ISignalBus.Inject 触发时）</summary>
        void OnSignalInjected(SignalTag tag, SignalPayload payload);

        /// <summary>注入的信号成功匹配到一个等待中的 WaitSignal 节点</summary>
        void OnSignalMatched(int listenerActionIndex, SignalTag tag);

        /// <summary>WatchCondition 节点成功创建条件监听句柄</summary>
        void OnWatchCreated(int actionIndex, string conditionType, IReadOnlyList<string> entityIds);

        /// <summary>WatchCondition 的条件已满足，句柄触发</summary>
        void OnWatchTriggered(int actionIndex, SignalPayload? payload);

        /// <summary>节点超时（WaitSignal 或 WatchCondition 或 CompositeCondition）</summary>
        void OnTimeout(int actionIndex, string nodeType);

        /// <summary>CompositeCondition 节点收到一个子条件触发</summary>
        void OnCompositeConditionUpdated(int actionIndex, int triggeredMask, int requiredMask);
    }
}
