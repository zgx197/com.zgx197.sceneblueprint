#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 预定义的数据类型常量。
    /// <para>
    /// 用于 Data 端口的类型声明和连线验证。
    /// 这些常量确保类型字符串的一致性，避免拼写错误。
    /// </para>
    /// <para>
    /// 这些类型已在 <see cref="DataTypeRegistry"/> 中注册，
    /// 支持类型兼容性检查和子类型验证。
    /// </para>
    /// </summary>
    public static class DataTypes
    {
        // ── 基础类型 ──

        /// <summary>任意类型通配符，用于不限制输入类型的 DataIn 端口（与 TypeCompatibilityRegistry.AnyType 对齐）</summary>
        public const string Any = "any";

        /// <summary>浮点数</summary>
        public const string Float = "float";
        
        /// <summary>整数</summary>
        public const string Int = "int";
        
        /// <summary>布尔值</summary>
        public const string Bool = "bool";
        
        /// <summary>字符串</summary>
        public const string String = "string";

        // ── Unity 基础类型 ──
        
        /// <summary>三维向量（位置、方向等）</summary>
        public const string Vector3 = "Vector3";
        
        /// <summary>三维向量数组</summary>
        public const string Vector3Array = "Vector3[]";
        
        /// <summary>二维向量</summary>
        public const string Vector2 = "Vector2";
        
        /// <summary>二维向量数组</summary>
        public const string Vector2Array = "Vector2[]";
        
        /// <summary>变换（位置+旋转+缩放）</summary>
        public const string Transform = "Transform";
        
        /// <summary>变换数组</summary>
        public const string TransformArray = "Transform[]";

        // ── SceneBlueprint 自定义类型 ──
        
        /// <summary>实体引用（运行时生成的实体）</summary>
        public const string EntityRef = "EntityRef";
        
        /// <summary>实体引用数组</summary>
        public const string EntityRefArray = "EntityRef[]";
        
        /// <summary>怪物配置</summary>
        public const string MonsterConfig = "MonsterConfig";
        
        /// <summary>怪物配置数组</summary>
        public const string MonsterConfigArray = "MonsterConfig[]";
        
        /// <summary>刷怪节奏数据</summary>
        public const string RhythmData = "RhythmData";
        
        /// <summary>区域数据</summary>
        public const string AreaData = "AreaData";
        
        /// <summary>路径数据</summary>
        public const string PathData = "PathData";

        // ── 扩展点 ──
        // 未来可以在此添加更多预定义类型，如：
        // - AIBehaviorConfig
        // - DialogueData
        // - CameraSettings
        // 等等
    }
}
