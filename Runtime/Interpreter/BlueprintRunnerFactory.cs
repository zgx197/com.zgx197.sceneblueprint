#nullable enable
using SceneBlueprint.Runtime.Interpreter.Systems;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// BlueprintRunner 工厂——提供标准化的 Runner 创建方式。
    /// <para>
    /// CreateDefault() 会自动注册框架内置 System，并从 BlueprintSystemRegistry
    /// 中加载所有用户层通过 IBlueprintSystemProvider 注册的 System。
    /// </para>
    /// <para>
    /// 推荐用法：
    /// <code>
    /// var runner = BlueprintRunnerFactory.CreateDefault();
    /// // 按需注入 Handler
    /// runner.GetSystem&lt;CameraShakeSystem&gt;()?.ShakeHandler = myHandler;
    /// runner.Load(jsonText);
    /// </code>
    /// </para>
    /// </summary>
    public static class BlueprintRunnerFactory
    {
        /// <summary>
        /// 创建包含框架内置 System 和所有已注册用户 System 的 Runner。
        /// </summary>
        public static BlueprintRunner CreateDefault()
        {
            var runner = new BlueprintRunner();

            // 框架内置 System（始终注册）
            runner.RegisterSystem(new FlowSystem());
            runner.RegisterSystem(new FlowFilterSystem());
            runner.RegisterSystem(new BlackboardGetSystem());
            runner.RegisterSystem(new BlackboardSetSystem());

            // 用户层 System（通过 IBlueprintSystemProvider 注册）
            BlueprintSystemRegistry.RegisterSystemsTo(runner);

            return runner;
        }
    }
}
