#nullable enable
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图事件观察者——消费带主体语义的结构化事件上下文。
    /// <para>
    /// 通过 <c>BlueprintRunner.RegisterService&lt;IBlueprintEventObserver&gt;()</c> 注入。
    /// 第一版主要服务于运行时测试窗口、调试记录与后续事件流可视化。
    /// </para>
    /// </summary>
    public interface IBlueprintEventObserver
    {
        void OnEventRecorded(BlueprintEventContext eventContext);
    }
}
