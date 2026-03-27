#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// BlueprintRunner 配置器作用域。
    /// 用于显式区分 framework 默认 bootstrap 与项目基线 bootstrap。
    /// </summary>
    internal enum BlueprintRunnerConfiguratorScope
    {
        Unknown = 0,
        FrameworkDefault = 1,
        ProjectBaseline = 2,
        RuntimeSample = 3,
        CompatibilityBoundary = 4,
    }
}
