#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 数据类型注册表——管理所有可用的数据类型定义。
    /// <para>
    /// 职责：
    /// - 注册和查询数据类型定义
    /// - 验证类型兼容性（包括子类型检查）
    /// - 提供类型元数据给编辑器使用
    /// </para>
    /// </summary>
    public class DataTypeRegistry
    {
        private readonly Dictionary<string, DataTypeDefinition> _types = new();
        private static DataTypeRegistry? _instance;

        /// <summary>获取全局单例实例</summary>
        public static DataTypeRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DataTypeRegistry();
                    _instance.RegisterBuiltInTypes();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 注册数据类型定义
        /// </summary>
        public void Register(DataTypeDefinition def)
        {
            if (string.IsNullOrEmpty(def.TypeId))
                throw new ArgumentException("TypeId 不能为空");

            if (_types.ContainsKey(def.TypeId))
                throw new ArgumentException($"类型 '{def.TypeId}' 已注册");

            _types[def.TypeId] = def;
        }

        /// <summary>
        /// 查询类型定义
        /// </summary>
        public DataTypeDefinition? Find(string typeId)
        {
            return _types.TryGetValue(typeId, out var def) ? def : null;
        }

        /// <summary>
        /// 获取所有已注册的类型
        /// </summary>
        public IEnumerable<DataTypeDefinition> GetAll()
        {
            return _types.Values;
        }

        /// <summary>
        /// 检查两个类型是否兼容（支持子类型检查）
        /// </summary>
        /// <param name="sourceType">源类型 ID</param>
        /// <param name="targetType">目标类型 ID</param>
        /// <returns>是否兼容</returns>
        public bool IsCompatible(string sourceType, string targetType)
        {
            // 精确匹配
            if (sourceType == targetType)
                return true;

            // DataTypes.Any ("any") 与任意非-exec 类型兼容
            if (sourceType == DataTypes.Any || targetType == DataTypes.Any)
                return true;

            // 查找类型定义
            var sourceDef = Find(sourceType);
            var targetDef = Find(targetType);

            // 如果找不到定义，回退到字符串比较
            if (sourceDef == null || targetDef == null)
                return sourceType == targetType;

            // 数组类型检查：元素类型必须兼容
            if (sourceDef.IsArray && targetDef.IsArray)
            {
                if (sourceDef.ElementType != null && targetDef.ElementType != null)
                {
                    return IsCompatible(
                        sourceDef.ElementType.TypeId,
                        targetDef.ElementType.TypeId);
                }
                return false;
            }

            // 数组与非数组不兼容
            if (sourceDef.IsArray != targetDef.IsArray)
                return false;

            // 子类型检查：源类型是目标类型的子类型
            return IsSubTypeOf(sourceType, targetType);
        }

        /// <summary>
        /// 检查 derivedType 是否是 baseType 的子类型
        /// </summary>
        public bool IsSubTypeOf(string derivedType, string baseType)
        {
            if (derivedType == baseType)
                return true;

            var derivedDef = Find(derivedType);
            if (derivedDef == null)
                return false;

            // 递归检查基类型链
            if (!string.IsNullOrEmpty(derivedDef.BaseTypeId))
            {
                if (derivedDef.BaseTypeId == baseType)
                    return true;

                return IsSubTypeOf(derivedDef.BaseTypeId, baseType);
            }

            return false;
        }

        /// <summary>
        /// 获取类型的显示名称
        /// </summary>
        public string GetDisplayName(string typeId)
        {
            var def = Find(typeId);
            return def?.DisplayName ?? typeId;
        }

        /// <summary>
        /// 注册内置类型
        /// </summary>
        private void RegisterBuiltInTypes()
        {
            // 基础类型
            Register(DataTypeDefinition.BuiltIn("float", "浮点数", typeof(float)));
            Register(DataTypeDefinition.BuiltIn("int", "整数", typeof(int)));
            Register(DataTypeDefinition.BuiltIn("bool", "布尔值", typeof(bool)));
            Register(DataTypeDefinition.BuiltIn("string", "字符串", typeof(string)));

            // Unity 基础类型
            Register(DataTypeDefinition.BuiltIn("Vector3", "三维向量"));
            Register(DataTypeDefinition.BuiltIn("Vector2", "二维向量"));
            Register(DataTypeDefinition.BuiltIn("Transform", "变换"));

            // 数组类型（基于元素类型自动生成）
            RegisterArrayType("Vector3");
            RegisterArrayType("Vector2");
            RegisterArrayType("Transform");

            // SceneBlueprint 自定义类型
            Register(DataTypeDefinition.BuiltIn("EntityRef", "实体引用"));
            RegisterArrayType("EntityRef");

            Register(DataTypeDefinition.BuiltIn("MonsterConfig", "怪物配置"));
            RegisterArrayType("MonsterConfig");

            Register(DataTypeDefinition.BuiltIn("RhythmData", "刷怪节奏"));
            Register(DataTypeDefinition.BuiltIn("AreaData", "区域数据"));
            Register(DataTypeDefinition.BuiltIn("PathData", "路径数据"));
        }

        /// <summary>
        /// 注册数组类型（基于元素类型）
        /// </summary>
        private void RegisterArrayType(string elementTypeId)
        {
            var elementDef = Find(elementTypeId);
            if (elementDef == null)
                return;

            var arrayDef = DataTypeDefinition.Array(elementTypeId, elementDef);
            Register(arrayDef);
        }

        /// <summary>
        /// 清空注册表（仅用于测试）
        /// </summary>
        internal void Clear()
        {
            _types.Clear();
        }

        /// <summary>
        /// 重置为默认状态（仅用于测试）
        /// </summary>
        internal static void Reset()
        {
            _instance = null;
        }
    }
}
