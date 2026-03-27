#nullable enable

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// BlueprintRunner 运行时配置器。
    /// 用于向默认 Runner 注入业务层 service / bus evaluator / bootstrap 逻辑。
    /// <para>
    /// 外部项目应通过正式 bootstrap/factory 入口注册 configurator，
    /// 而不是直接依赖底层 registry/scope 细节。
    /// </para>
    /// </summary>
    public interface IBlueprintRunnerConfigurator
    {
        void Configure(BlueprintRunner runner);
    }
}
