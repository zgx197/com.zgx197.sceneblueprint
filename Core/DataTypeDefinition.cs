#nullable enable
using System;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 数据类型定义——描述一个数据类型的元数据。
    /// <para>
    /// 用于增强类型系统，支持子类型检查、运行时类型映射、编辑器提示等。
    /// </para>
    /// </summary>
    public class DataTypeDefinition
    {
        /// <summary>
        /// 类型唯一 ID，如 "Vector3", "MonsterConfig[]"
        /// <para>在 DataTypeRegistry 中必须唯一</para>
        /// </summary>
        public string TypeId { get; set; } = "";

        /// <summary>
        /// 编辑器中显示的名称，如 "三维向量", "怪物配置数组"
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 运行时对应的 C# 类型（可选）
        /// <para>用于类型检查和反射，如果不需要运行时类型可为 null</para>
        /// </summary>
        public Type? RuntimeType { get; set; }

        /// <summary>
        /// 是否为数组类型
        /// </summary>
        public bool IsArray { get; set; }

        /// <summary>
        /// 数组元素类型（仅当 IsArray=true 时有效）
        /// <para>例如：Vector3[] 的 ElementType 为 Vector3 对应的定义</para>
        /// </summary>
        public DataTypeDefinition? ElementType { get; set; }

        /// <summary>
        /// 基类型 ID（用于子类型检查）
        /// <para>例如：MonsterConfig 的 BaseTypeId 可以是 UnitConfig</para>
        /// </summary>
        public string? BaseTypeId { get; set; }

        /// <summary>
        /// 类型描述（用于编辑器提示和文档生成）
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 类型颜色（用于编辑器可视化）
        /// <para>例如在连线时用不同颜色区分数据类型</para>
        /// </summary>
        public NodeGraph.Math.Color4? EditorColor { get; set; }

        /// <summary>
        /// 是否为内置类型（不可修改）
        /// </summary>
        public bool IsBuiltIn { get; set; }

        /// <summary>
        /// 创建内置类型定义（简化版）
        /// </summary>
        public static DataTypeDefinition BuiltIn(
            string typeId,
            string displayName,
            Type? runtimeType = null,
            bool isArray = false,
            string? baseTypeId = null)
        {
            return new DataTypeDefinition
            {
                TypeId = typeId,
                DisplayName = displayName,
                RuntimeType = runtimeType,
                IsArray = isArray,
                BaseTypeId = baseTypeId,
                IsBuiltIn = true
            };
        }

        /// <summary>
        /// 创建数组类型定义
        /// </summary>
        public static DataTypeDefinition Array(
            string elementTypeId,
            DataTypeDefinition elementTypeDef,
            Type? runtimeType = null)
        {
            return new DataTypeDefinition
            {
                TypeId = $"{elementTypeId}[]",
                DisplayName = $"{elementTypeDef.DisplayName}数组",
                RuntimeType = runtimeType,
                IsArray = true,
                ElementType = elementTypeDef,
                IsBuiltIn = elementTypeDef.IsBuiltIn
            };
        }
    }
}
