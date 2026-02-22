#nullable enable
using System;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 数据类型标记特性——用于标记泛型参数对应的类型 ID。
    /// <para>
    /// 使用示例：
    /// <code>
    /// [DataType("Vector3[]")]
    /// public class Vector3ArrayType { }
    /// 
    /// // 使用时：
    /// Port.DataIn&lt;Vector3ArrayType&gt;("positions", "位置列表")
    /// </code>
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class DataTypeAttribute : Attribute
    {
        /// <summary>类型 ID</summary>
        public string TypeId { get; }

        public DataTypeAttribute(string typeId)
        {
            TypeId = typeId;
        }
    }

    // ── 预定义的类型标记类 ──

    /// <summary>Vector3 数组类型标记</summary>
    [DataType(DataTypes.Vector3Array)]
    public sealed class Vector3ArrayType { }

    /// <summary>Vector2 数组类型标记</summary>
    [DataType(DataTypes.Vector2Array)]
    public sealed class Vector2ArrayType { }

    /// <summary>Transform 数组类型标记</summary>
    [DataType(DataTypes.TransformArray)]
    public sealed class TransformArrayType { }

    /// <summary>实体引用数组类型标记</summary>
    [DataType(DataTypes.EntityRefArray)]
    public sealed class EntityRefArrayType { }

    /// <summary>怪物配置数组类型标记</summary>
    [DataType(DataTypes.MonsterConfigArray)]
    public sealed class MonsterConfigArrayType { }

    /// <summary>Vector3 类型标记</summary>
    [DataType(DataTypes.Vector3)]
    public sealed class Vector3Type { }

    /// <summary>Vector2 类型标记</summary>
    [DataType(DataTypes.Vector2)]
    public sealed class Vector2Type { }

    /// <summary>Transform 类型标记</summary>
    [DataType(DataTypes.Transform)]
    public sealed class TransformType { }

    /// <summary>实体引用类型标记</summary>
    [DataType(DataTypes.EntityRef)]
    public sealed class EntityRefType { }

    /// <summary>怪物配置类型标记</summary>
    [DataType(DataTypes.MonsterConfig)]
    public sealed class MonsterConfigType { }

    /// <summary>刷怪节奏数据类型标记</summary>
    [DataType(DataTypes.RhythmData)]
    public sealed class RhythmDataType { }

    /// <summary>区域数据类型标记</summary>
    [DataType(DataTypes.AreaData)]
    public sealed class AreaDataType { }

    /// <summary>路径数据类型标记</summary>
    [DataType(DataTypes.PathData)]
    public sealed class PathDataType { }
}
