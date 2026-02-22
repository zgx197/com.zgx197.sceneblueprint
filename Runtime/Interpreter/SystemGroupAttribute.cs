#nullable enable
using System;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// System 执行分组，替代魔法数字 Order。
    /// <para>
    /// BlueprintRunner 按分组顺序排列 System，同组内按 <see cref="UpdateAfterAttribute"/> 做拓扑排序。
    /// </para>
    /// </summary>
    public enum SystemGroup
    {
        /// <summary>框架级：FlowSystem、BlackboardSystem、FlowFilterSystem</summary>
        Framework   = 0,
        /// <summary>业务级：SpawnSystem、TriggerSystem、VFX 等</summary>
        Business    = 100,
        /// <summary>后处理：TransitionSystem（需在所有业务 System 之后执行）</summary>
        PostProcess = 900,
    }

    /// <summary>
    /// 声明 System 所属的执行分组。
    /// <para>不标记则行为与旧 <c>Order</c> 字段兼容：BlueprintRunner 将直接以 Order 值排序。</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class UpdateInGroupAttribute : Attribute
    {
        public SystemGroup Group { get; }
        public UpdateInGroupAttribute(SystemGroup group) => Group = group;
    }

    /// <summary>
    /// 声明当前 System 必须在指定 System 之后执行（同分组内生效）。
    /// <para>
    /// 用法示例：
    /// <code>
    /// [UpdateInGroup(SystemGroup.Framework)]
    /// [UpdateAfter(typeof(FlowSystem))]
    /// public class BlackboardSetSystem : BlueprintSystemBase { ... }
    /// </code>
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class UpdateAfterAttribute : Attribute
    {
        public Type SystemType { get; }
        public UpdateAfterAttribute(Type systemType) => SystemType = systemType;
    }
}
