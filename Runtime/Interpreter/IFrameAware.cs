#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 标记接口——声明此 System 需要 BlueprintFrame 引用注入。
    /// <para>
    /// BlueprintRunner 在加载蓝图时会自动扫描所有注册的 System，
    /// 对实现了此接口的 System 注入 BlueprintFrame 引用。
    /// </para>
    /// <para>
    /// 使用场景：System 需要访问 FrameView 尚未包含的 BlueprintFrame 特有功能
    /// （如 SceneBindings、DataPort、Runner.GetService、Blackboard 等）。
    /// </para>
    /// </summary>
    public interface IFrameAware
    {
        /// <summary>BlueprintFrame 引用（由 BlueprintRunner 在 Load 时注入）</summary>
        BlueprintFrame? Frame { get; set; }
    }
}
