#nullable enable
using System;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 黑板条目接口——封装一个运行时类型安全的变量值。
    /// <para>
    /// 与直接存 <c>(object, Type)</c> 元组相比，接口方式可在不破坏 API 的前提下
    /// 按需扩展（如后续添加 <c>Clone()</c>、序列化、脏标记等能力）。
    /// </para>
    /// </summary>
    public interface IBlackboardEntry
    {
        /// <summary>存储值的运行时类型（由 <see cref="BlackboardEntry{T}"/> 在写入时捕获）</summary>
        Type ValueType { get; }

        /// <summary>装箱后的原始值（供快照序列化、调试显示使用，热路径应优先使用泛型 API）</summary>
        object? BoxedValue { get; }
    }

    /// <summary>
    /// 黑板条目的强类型实现。
    /// </summary>
    public sealed class BlackboardEntry<T> : IBlackboardEntry where T : notnull
    {
        /// <summary>强类型原始值</summary>
        public T Value { get; }

        /// <inheritdoc/>
        public Type ValueType => typeof(T);

        /// <inheritdoc/>
        public object? BoxedValue => Value;

        public BlackboardEntry(T value) => Value = value;
    }
}
