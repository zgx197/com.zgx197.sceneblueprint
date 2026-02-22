#nullable enable
using System;
using NodeGraph.Core;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  端口定义 (PortDefinition)
    //
    //  端口是行动节点上的连接点，用于表达执行流和数据流。
    //  每个端口有类型（Control/Event/Data）、方向（输入/输出）和容量（单连接/多连接）。
    //
    //  设计思路：
    //  - Control（控制流）：同步执行，决定执行顺序
    //  - Event（事件流）：异步触发，条件满足时触发
    //  - Data（数据流）：传递配置或状态，不影响执行顺序
    //  
    //  - 控制流输入端口：Single（执行来源必须唯一，避免竞争条件）
    //    特例：Flow.Join 等汇合节点使用 Port.InMulti()，语义是"等待多个上游"
    //  - 控制流输出端口：Multiple（一个完成点可以并联触发多个下游）
    //  - 事件输出端口：Multiple（一个事件可以触发多个后续行动）
    //
    //  示例：
    //    Spawn 节点有 6 个端口：
    //      in(Control输入)  out(Control输出)  onComplete(Event)  
    //      spawnedEntities(Data输出)  positions(Data输入)  monsters(Data输入)
    //
    //  注意：PortKind、PortDirection、PortCapacity 使用 NodeGraph.Core 中的定义
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 端口定义——声明一个行动节点上的输入/输出端口。
    /// <para>
    /// 端口定义是 <see cref="ActionDefinition"/> 的一部分，
    /// 在 ActionDefinition.Ports 数组中声明该行动有哪些端口。
    /// </para>
    /// <para>
    /// 端口 ID 在同一个 ActionDefinition 内必须唯一，
    /// 它会作为连线数据中的 FromPortId / ToPortId 使用。
    /// </para>
    /// </summary>
    public class PortDefinition
    {
        /// <summary>
        /// 端口唯一 ID，如 "in", "out", "onWaveComplete", "positions"
        /// <para>在同一个 ActionDefinition 内必须唯一</para>
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 编辑器中显示的端口名，如 "输入", "输出", "波次完成", "位置列表"
        /// <para>为空时编辑器可回退显示 Id</para>
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>端口类型——Control(控制流) / Event(事件流) / Data(数据流)</summary>
        public PortKind Kind { get; set; } = PortKind.Control;

        /// <summary>端口方向——In(输入) 或 Out(输出)</summary>
        public PortDirection Direction { get; set; }

        /// <summary>端口容量——Single(单连接) 或 Multiple(多连接)</summary>
        public PortCapacity Capacity { get; set; }

        // ── Data 端口专用字段 ──

        /// <summary>
        /// 数据类型（仅 Data 端口有效），如 "Vector3[]", "EntityRef[]", "MonsterConfig[]"
        /// <para>用于连线验证和类型检查</para>
        /// </summary>
        public string DataType { get; set; } = "";

        /// <summary>
        /// 是否必需（仅 Data 输入端口有效）。
        /// <para>如果为 true，编辑器会在此端口未连接时显示警告</para>
        /// </summary>
        public bool Required { get; set; } = false;

        /// <summary>
        /// 端口描述文本（可选），用于编辑器提示和文档生成
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 端口未连接时的默认值（仅 Data 输入端口有效）。
        /// <para>
        /// 为 null 表示无默认值（端口未连接时系统代码自行处理）。
        /// 编辑器可在端口旁以灰色文字显示默认值提示，如 "未连接时: 1.0"。
        /// </para>
        /// </summary>
        public object? DefaultValue { get; set; }
    }

    /// <summary>
    /// 端口便捷工厂——提供常用端口的快捷创建方法。
    /// <para>
    /// 使用示例：
    /// <code>
    /// Ports = new[] {
    ///     Port.In("in"),                          // 标准控制流输入
    ///     Port.Out("out"),                        // 标准控制流输出
    ///     Port.Event("onWaveComplete", "波次完成") // 事件输出（可连多条线）
    ///     Port.DataIn("positions", "位置列表", DataTypes.Vector3Array)  // 数据输入
    ///     Port.DataOut("entities", "实体列表", DataTypes.EntityRefArray) // 数据输出
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public static class Port
    {
        /// <summary>
        /// 创建控制流输入端口（单连接）。
        /// <para>输入端口只允许一个上游节点连入（Single），
        /// 保证执行来源唯一，避免多路竞争触发。</para>
        /// <para>若需要多上游汇合（如 Flow.Join），请使用 <see cref="InMulti"/>。</para>
        /// </summary>
        /// <param name="id">端口 ID，如 "in"</param>
        /// <param name="displayName">显示名，为空则使用 id</param>
        public static PortDefinition In(string id, string displayName = "")
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Kind = PortKind.Control,
                Direction = PortDirection.Input,
                Capacity = PortCapacity.Single
            };
        }

        /// <summary>
        /// 创建控制流输入端口（多连接，仅供汇合类节点使用）。
        /// <para>
        /// 普通节点应使用 <see cref="In"/>（Single）。
        /// 仅当节点的语义是"等待多个上游汇合"时（如 Flow.Join），才使用此方法。
        /// </para>
        /// </summary>
        /// <param name="id">端口 ID，如 "in"</param>
        /// <param name="displayName">显示名，为空则使用 id</param>
        public static PortDefinition InMulti(string id, string displayName = "")
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Kind = PortKind.Control,
                Direction = PortDirection.Input,
                Capacity = PortCapacity.Multiple
            };
        }

        /// <summary>
        /// 创建控制流输出端口（多连接）。
        /// <para>输出端口允许连接多条线，同一输出可并联触发多个下游节点。
        /// 若需要多路分支（如 Branch 的 true/false），创建多个 Out 端口即可。</para>
        /// </summary>
        /// <param name="id">端口 ID，如 "out"</param>
        /// <param name="displayName">显示名，为空则使用 id</param>
        public static PortDefinition Out(string id, string displayName = "")
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Kind = PortKind.Control,
                Direction = PortDirection.Output,
                Capacity = PortCapacity.Multiple
            };
        }

        /// <summary>
        /// 创建事件输出端口（多连接）。
        /// <para>事件端口允许连多条线，一个事件可以同时触发多个后续行动。
        /// 典型用途：onWaveComplete（波次完成时可同时触发增援和摄像机切换）。</para>
        /// </summary>
        /// <param name="id">端口 ID，如 "onWaveComplete"</param>
        /// <param name="displayName">显示名，如 "波次完成"</param>
        public static PortDefinition Event(string id, string displayName = "")
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Kind = PortKind.Event,
                Direction = PortDirection.Output,
                Capacity = PortCapacity.Multiple
            };
        }

        /// <summary>
        /// 创建数据输入端口（单连接）。
        /// <para>数据输入端口接收上游节点传递的配置或状态数据。
        /// 每个数据输入端口只能连一条线（避免数据来源冲突）。</para>
        /// </summary>
        /// <param name="id">端口 ID，如 "positions", "monsters"</param>
        /// <param name="displayName">显示名，如 "位置列表", "怪物配置"</param>
        /// <param name="dataType">数据类型，如 "Vector3[]", "MonsterConfig[]"</param>
        /// <param name="required">是否必需，默认 false</param>
        /// <param name="description">描述文本（可选）</param>
        public static PortDefinition DataIn(string id, string displayName, string dataType, 
            bool required = false, string description = "", object? defaultValue = null)
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Kind = PortKind.Data,
                Direction = PortDirection.Input,
                Capacity = PortCapacity.Single,
                DataType = dataType,
                Required = required,
                Description = description,
                DefaultValue = defaultValue
            };
        }

        /// <summary>
        /// 创建数据输出端口（多连接）。
        /// <para>数据输出端口向下游节点传递配置或状态数据。
        /// 一个数据输出可以连多条线（多个下游节点共享同一数据）。</para>
        /// </summary>
        /// <param name="id">端口 ID，如 "spawnedEntities", "positions"</param>
        /// <param name="displayName">显示名，如 "已刷出实体", "生成的位置"</param>
        /// <param name="dataType">数据类型，如 "EntityRef[]", "Vector3[]"</param>
        /// <param name="description">描述文本（可选）</param>
        public static PortDefinition DataOut(string id, string displayName, string dataType, 
            string description = "")
        {
            return new PortDefinition
            {
                Id = id,
                DisplayName = displayName,
                Kind = PortKind.Data,
                Direction = PortDirection.Output,
                Capacity = PortCapacity.Multiple,
                DataType = dataType,
                Description = description
            };
        }

        // ── 泛型重载版本（简化调用）──

        /// <summary>
        /// 创建数据输入端口（泛型版本）。
        /// <para>
        /// 使用示例：
        /// <code>
        /// Port.DataIn&lt;Vector3ArrayType&gt;("positions", "位置列表")
        /// Port.DataIn&lt;MonsterConfigArrayType&gt;("monsters", "怪物配置", required: true)
        /// </code>
        /// </para>
        /// </summary>
        /// <typeparam name="TDataType">数据类型标记类（需标记 DataTypeAttribute）</typeparam>
        /// <param name="id">端口 ID</param>
        /// <param name="displayName">显示名</param>
        /// <param name="required">是否必需，默认 false</param>
        /// <param name="description">描述文本（可选）</param>
        public static PortDefinition DataIn<TDataType>(string id, string displayName, 
            bool required = false, string description = "", object? defaultValue = null)
        {
            var dataType = GetDataTypeId<TDataType>();
            return DataIn(id, displayName, dataType, required, description, defaultValue);
        }

        /// <summary>
        /// 创建数据输出端口（泛型版本）。
        /// <para>
        /// 使用示例：
        /// <code>
        /// Port.DataOut&lt;EntityRefArrayType&gt;("spawnedEntities", "已刷出实体")
        /// Port.DataOut&lt;Vector3ArrayType&gt;("positions", "位置列表", "随机生成的位置")
        /// </code>
        /// </para>
        /// </summary>
        /// <typeparam name="TDataType">数据类型标记类（需标记 DataTypeAttribute）</typeparam>
        /// <param name="id">端口 ID</param>
        /// <param name="displayName">显示名</param>
        /// <param name="description">描述文本（可选）</param>
        public static PortDefinition DataOut<TDataType>(string id, string displayName, 
            string description = "")
        {
            var dataType = GetDataTypeId<TDataType>();
            return DataOut(id, displayName, dataType, description);
        }

        /// <summary>
        /// 从泛型参数获取数据类型 ID
        /// </summary>
        private static string GetDataTypeId<TDataType>()
        {
            var type = typeof(TDataType);
            var attr = (DataTypeAttribute?)System.Attribute.GetCustomAttribute(
                type, typeof(DataTypeAttribute));

            if (attr == null)
            {
                throw new System.InvalidOperationException(
                    $"类型 {type.Name} 必须标记 [DataType] 特性");
            }

            return attr.TypeId;
        }
    }
}

