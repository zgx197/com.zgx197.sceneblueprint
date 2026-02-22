#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 蓝图 System 提供者接口——声明本程序集向 BlueprintRunner 贡献哪些 System。
    /// <para>
    /// 使用方式：
    /// 1. 在用户层程序集中实现此接口
    /// 2. 使用 [RuntimeInitializeOnLoadMethod] 向 BlueprintSystemRegistry 注册自身
    /// 3. BlueprintRunnerFactory.CreateDefault() 自动从 Registry 加载所有 System
    /// </para>
    /// <para>
    /// 示例：
    /// <code>
    /// public class MySystemProvider : IBlueprintSystemProvider
    /// {
    ///     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    ///     static void AutoRegister() => BlueprintSystemRegistry.Register(new MySystemProvider());
    ///
    ///     public IEnumerable&lt;BlueprintSystemBase&gt; CreateSystems()
    ///     {
    ///         yield return new MyCustomSystem();
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public interface IBlueprintSystemProvider
    {
        IEnumerable<BlueprintSystemBase> CreateSystems();
    }
}
