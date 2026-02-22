#nullable enable
using System;

namespace SceneBlueprint.Editor.Markers.Annotations
{
    /// <summary>
    /// 标注在 <see cref="IAnnotationDefinitionProvider"/> 实现类上，声明标注类型 ID。
    /// <para>
    /// <see cref="AnnotationDefinitionRegistry.AutoDiscover"/> 会扫描所有标注了此 Attribute 的类，
    /// 并调用其 Define() 方法来获取 <see cref="AnnotationDefinition"/>。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [AnnotationDef("Spawn")]
    /// public class SpawnAnnotationDef : IAnnotationDefinitionProvider
    /// {
    ///     public AnnotationDefinition Define() => new AnnotationDefinition { ... };
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class AnnotationDefAttribute : Attribute
    {
        /// <summary>标注类型 ID，如 "Spawn", "Camera", "Patrol"</summary>
        public string TypeId { get; }

        public AnnotationDefAttribute(string typeId) { TypeId = typeId; }
    }

    /// <summary>
    /// 标注定义提供者接口——实现此接口并标注 <see cref="AnnotationDefAttribute"/> 即可被自动发现。
    /// <para>
    /// 每个实现类代表一种标注类型，Define() 方法返回该标注的完整定义。
    /// 这种设计让标注定义分散在各自的文件中，新增标注类型只需添加一个文件。
    /// </para>
    /// </summary>
    public interface IAnnotationDefinitionProvider
    {
        /// <summary>返回标注类型定义（元数据）</summary>
        AnnotationDefinition Define();
    }
}
