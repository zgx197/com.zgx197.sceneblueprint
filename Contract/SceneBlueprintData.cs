#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 场景绑定类型——决定导出时的数据格式。
    /// <para>从 SceneBlueprint.Core.PropertyDefinition 迁移至此，供 Runtime 和 Core 共同引用。</para>
    /// </summary>
    public enum BindingType
    {
        /// <summary>位置/朝向——导出为位置+旋转数据</summary>
        Transform,
        /// <summary>多边形区域——导出为顶点坐标数组</summary>
        Area,
        /// <summary>路径——导出为路径点序列</summary>
        Path,
        /// <summary>碰撞器/触发器——导出为字符串 ID</summary>
        Collider
    }


    /// <summary>
    /// 蓝图导出数据（顶层）。
    /// 纯数据类，零框架依赖。运行时通过 JSON 反序列化消费此数据。
    /// </summary>
    [Serializable]
    public class SceneBlueprintData
    {
        // ── 元信息 ──
        public string BlueprintId = "";
        public string BlueprintName = "";
        public int Version = 1;
        public string ExportTime = "";

        // ── 核心数据 ──
        public ActionEntry[] Actions = Array.Empty<ActionEntry>();
        public TransitionEntry[] Transitions = Array.Empty<TransitionEntry>();
        public VariableDeclaration[] Variables = Array.Empty<VariableDeclaration>();
        public DataConnectionEntry[] DataConnections = Array.Empty<DataConnectionEntry>();
    }

    /// <summary>
    /// 数据连接条目——对应图中的一条 Data 边。
    /// <para>
    /// 与 <see cref="TransitionEntry"/>（控制流边）分离存储，语义互不干扰：
    /// - TransitionEntry：触发下游节点执行
    /// - DataConnectionEntry：向下游节点的 DataIn 端口提供值
    /// </para>
    /// </summary>
    [Serializable]
    public class DataConnectionEntry
    {
        /// <summary>生产者节点 ID</summary>
        public string FromActionId = "";
        /// <summary>生产者 DataOut 端口 ID</summary>
        public string FromPortId = "";
        /// <summary>消费者节点 ID</summary>
        public string ToActionId = "";
        /// <summary>消费者 DataIn 端口 ID</summary>
        public string ToPortId = "";
    }

    /// <summary>单个 DataIn 端口的默认值（无连线时使用）</summary>
    [Serializable]
    public class PortDefaultValue
    {
        public string PortId = "";
        public string DefaultValue = "";
    }

    /// <summary>行动条目（对应图中的一个节点）</summary>
    [Serializable]
    public class ActionEntry
    {
        public string Id = "";
        public string TypeId = "";
        public PropertyValue[] Properties = Array.Empty<PropertyValue>();
        public SceneBindingEntry[] SceneBindings = Array.Empty<SceneBindingEntry>();
        public PortDefaultValue[] PortDefaults = Array.Empty<PortDefaultValue>();
    }

    /// <summary>属性值（扁平化键值对，字符串序列化）</summary>
    [Serializable]
    public class PropertyValue
    {
        public string Key = "";
        public string ValueType = "";
        public string Value = "";
    }

    /// <summary>过渡条目（对应图中的一条连线）</summary>
    [Serializable]
    public class TransitionEntry
    {
        public string FromActionId = "";
        public string FromPortId = "";
        public string ToActionId = "";
        public string ToPortId = "";
        public ConditionData Condition = new ConditionData();
    }

    /// <summary>
    /// 条件数据（可嵌套组合）。
    /// Type: "Immediate" | "Delay" | "Expression" | "Tag" | "Event" | "AllOf" | "AnyOf"
    /// </summary>
    [Serializable]
    public class ConditionData
    {
        public string Type = "Immediate";
        public string Expression = "";

        /// <summary>
        /// 子条件（用于 AllOf/AnyOf 组合条件，Phase 2+ 预留）。
        /// 标记 NonSerialized 避免 Unity JsonUtility 递归展开导致深度溢出警告。
        /// 需要时通过自定义解析器处理。
        /// </summary>
        [NonSerialized]
        public ConditionData[] Children = Array.Empty<ConditionData>();
    }

    /// <summary>场景绑定条目</summary>
    [Serializable]
    public class SceneBindingEntry
    {
        public string BindingKey = "";
        public string BindingType = "";
        /// <summary>绑定对象标识（V2 统一语义）。</summary>
        public string SceneObjectId = "";
        /// <summary>V2 字段：稳定对象 ID（抗重命名）。</summary>
        public string StableObjectId = "";
        /// <summary>V2 字段：导出时使用的空间适配器类型（如 Unity3D / Unity2D）。</summary>
        public string AdapterType = "";
        /// <summary>V2 字段：空间扩展载荷（JSON 字符串）。</summary>
        public string SpatialPayloadJson = "";
        public string SourceSubGraph = "";
        public string SourceActionTypeId = "";
        /// <summary>标注数据（由 MarkerAnnotation.CollectExportData 收集）。</summary>
        public AnnotationDataEntry[] Annotations = Array.Empty<AnnotationDataEntry>();
    }

    /// <summary>标注数据条目（一个 Annotation 组件的导出数据）</summary>
    [Serializable]
    public class AnnotationDataEntry
    {
        /// <summary>标注类型 ID（如 "Spawn", "Camera"）</summary>
        public string TypeId = "";
        /// <summary>标注属性（扁平化键值对）</summary>
        public PropertyValue[] Properties = Array.Empty<PropertyValue>();
    }

    /// <summary>
    /// 变量声明——蓝图级别的 Blackboard 变量定义。
    /// <para>
    /// Index: 运行时唯一整型索引，用于 O(1) 访问（替代字符串 Key）。
    /// Type: "Int" | "Float" | "Bool" | "String"。
    /// Scope: "Local"（蓝图实例级）| "Global"（游戏会话级）。
    /// </para>
    /// </summary>
    [Serializable]
    public class VariableDeclaration
    {
        public int    Index        = -1;
        public string Name         = "";
        public string Type         = "Int";
        public string Scope        = "Local";
        public string InitialValue = "";
    }
}
