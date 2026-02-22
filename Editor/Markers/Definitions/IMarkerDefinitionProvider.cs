#nullable enable
using System;

namespace SceneBlueprint.Editor.Markers.Definitions
{
    /// <summary>
    /// 标注在 <see cref="IMarkerDefinitionProvider"/> 实现类上，声明标记类型 ID。
    /// <para>
    /// <see cref="MarkerDefinitionRegistry.AutoDiscover"/> 会扫描所有标注了此 Attribute 的类，
    /// 并调用其 Define() 方法来获取 <see cref="MarkerDefinition"/>。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [MarkerDef("Point")]
    /// public class PointMarkerDef : IMarkerDefinitionProvider
    /// {
    ///     public MarkerDefinition Define() => new MarkerDefinition { ... };
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class MarkerDefAttribute : Attribute
    {
        /// <summary>标记类型 ID，如 "Point", "Area", "Entity"</summary>
        public string TypeId { get; }

        public MarkerDefAttribute(string typeId) { TypeId = typeId; }
    }

    /// <summary>
    /// 标记定义提供者接口——实现此接口并标注 <see cref="MarkerDefAttribute"/> 即可被自动发现。
    /// <para>
    /// 每个实现类代表一种标记类型，Define() 方法返回该标记的完整定义。
    /// 这种设计让标记定义分散在各自的文件中，新增标记类型只需添加一个文件。
    /// </para>
    /// </summary>
    public interface IMarkerDefinitionProvider
    {
        /// <summary>返回标记类型定义（元数据）</summary>
        MarkerDefinition Define();
    }
}
