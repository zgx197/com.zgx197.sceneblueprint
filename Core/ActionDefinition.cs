#nullable enable
using NodeGraph.Math;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  行动定义 (ActionDefinition)
    //
    //  ActionDefinition 是场景蓝图系统的核心概念，类似于 GAS 中的
    //  GameplayAbility。它用纯数据描述“一种行动是什么样子”：
    //    - 它有什么属性（Properties）
    //    - 它有什么端口（Ports）
    //    - 它属于什么分类（Category）
    //    - 它是瞬时的还是持续的（Duration）
    //
    //  整体架构：
    //
    //  ActionDefinition    ───  “这是什么类型的行动”（元数据/模板）
    //       │
    //       ├── PortDefinition[]      ───  端口声明（决定节点能连哪些线）
    //       ├── PropertyDefinition[]  ───  属性声明（决定 Inspector 长什么样）
    //       └── 元数据 (TypeId, 分类, 颜色…)
    //
    //  ActionNodeData      ───  “这个具体节点的数据”（实例数据）
    //       │
    //       ├── ActionTypeId          ───  指向哪个 ActionDefinition
    //       └── PropertyBag           ───  属性的实际值
    //
    //  与 GAS 的映射：
    //    ActionDefinition  ↔  GameplayAbility 的 CDO (Class Default Object)
    //    ActionNodeData    ↔  GameplayAbility 的实例
    //    ActionRegistry    ↔  AbilitySystemComponent 的注册表
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 行动持续类型——决定行动的生命周期行为。
    /// <para>
    /// 生命周期：Inactive → Activated → Running → Completed / Cancelled
    /// </para>
    /// </summary>
    public enum ActionDuration
    {
        /// <summary>
        /// 瞬时行动——执行一次就完成。
        /// <para>典型：放置预设怪、切换灯光、触发事件</para>
        /// </summary>
        Instant,
        /// <summary>
        /// 持续行动——有运行状态，需要等待完成。
        /// <para>典型：节奏刷怪（多波）、摄像机跟踪、延时等待</para>
        /// </summary>
        Duration,
        /// <summary>
        /// 被动行动——条件满足时自动响应。
        /// <para>典型：玩家进入区域时触发、HP 低于阈值时响应</para>
        /// </summary>
        Passive
    }

    /// <summary>
    /// 行动定义——行动类型的元数据描述。
    /// <para>
    /// 用数据声明一种行动“长什么样、有哪些属性、能连哪些线”。
    /// 编辑器根据这个定义自动生成节点外观、Inspector 面板、搜索菜单等。
    /// </para>
    /// </summary>
    /// <example>
    /// 创建方式（通过 IActionDefinitionProvider 实现）：
    /// <code>
    /// [ActionType("Combat.Spawn")]
    /// public class SpawnActionDef : IActionDefinitionProvider
    /// {
    ///     public ActionDefinition Define() => new ActionDefinition
    ///     {
    ///         TypeId = "Combat.Spawn",
    ///         DisplayName = "刷怪",
    ///         Category = "Combat",
    ///         Duration = ActionDuration.Duration,
    ///         Ports = new[] { Port.FlowIn("in"), Port.FlowOut("out") },
    ///         Properties = new[] { Prop.Int("count", "数量", defaultValue: 5) }
    ///     };
    /// }
    /// </code>
    /// </example>
    public class ActionDefinition
    {
        // ─── 元数据 ───

        /// <summary>
        /// 全局唯一类型 ID，格式为 "域.行动名"。
        /// <para>示例："Combat.Spawn", "Presentation.Camera", "Flow.Start"</para>
        /// <para>在整个 ActionRegistry 中必须唯一，是查找和引用行动类型的主键。</para>
        /// </summary>
        public string TypeId { get; set; } = "";

        /// <summary>
        /// 编辑器中显示的名称，如 "刷怪", "摄像机控制", "延迟"
        /// <para>建议使用中文，让策划可以直观理解。搜索窗可通过此名模糊搜索。</para>
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 行动分类——用于搜索窗分组和 Registry 查询。
        /// <para>预定义分类："Flow"(流程), "Combat"(战斗), "Presentation"(表现)。
        /// 可自由扩展新分类。</para>
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>描述文本——在搜索窗悬停时显示，帮助策划理解该行动的用途</summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 节点主题色——编辑器中节点头部的颜色，用于视觉区分不同类型的行动。
        /// <para>使用 NodeGraph.Math.Color4，无 Unity 依赖。</para>
        /// </summary>
        public Color4 ThemeColor { get; set; } = Color4.Gray;

        /// <summary>图标标识（可选）——用于在节点头部或搜索窗中显示小图标</summary>
        public string? Icon { get; set; }

        // ─── 端口声明 ───

        /// <summary>
        /// 端口定义列表——声明该行动节点有哪些输入和输出端口。
        /// <para>端口决定了节点能连哪些线。使用 <see cref="Port"/> 工厂创建。</para>
        /// </summary>
        public PortDefinition[] Ports { get; set; } = System.Array.Empty<PortDefinition>();

        // ─── 属性声明 ───

        /// <summary>
        /// 属性定义列表——声明该行动有哪些可编辑字段。
        /// <para>Inspector 会根据这些定义自动生成 UI 控件。使用 <see cref="Prop"/> 工厂创建。</para>
        /// </summary>
        public PropertyDefinition[] Properties { get; set; } = System.Array.Empty<PropertyDefinition>();

        // ─── 场景需求声明 ───

        /// <summary>
        /// 场景标记需求列表——声明该行动需要什么类型的场景标记。
        /// <para>
        /// 为空数组表示该行动不需要场景标记（如 Delay、Branch 等纯逻辑节点）。
        /// 非空时，Scene View 右键菜单会根据此声明自动创建标记并绑定。
        /// </para>
        /// </summary>
        public MarkerRequirement[] SceneRequirements { get; set; } = System.Array.Empty<MarkerRequirement>();

        // ─── 行为标记 ───

        /// <summary>
        /// 行动持续类型——决定运行时的生命周期行为。
        /// <para>默认为 Instant（瞬时完成）。</para>
        /// </summary>
        public ActionDuration Duration { get; set; } = ActionDuration.Instant;

        // ─── 输出变量声明 ───

        /// <summary>
        /// 节点运行时会写入黑板的变量声明。
        /// <para>
        /// 编辑器会扫描图中所有节点的 OutputVariables，将其作为只读条目
        /// 注入变量下拉列表，使下游节点（如 Flow.Filter）可以直接选择。
        /// 运行时代码通过字符串键写入黑板，声明仅用于编辑器感知。
        /// </para>
        /// </summary>
        public OutputVariableDefinition[] OutputVariables { get; set; } = System.Array.Empty<OutputVariableDefinition>();

        // ── 类型级自定义验证 ──

        /// <summary>
        /// 类型级自定义验证器（可选）。
        /// <para>
        /// 由分析层（SB006）在验证阶段对每个可达节点调用。
        /// 用于表达“只有这种行动类型才有的约束”，
        /// 例如 Branch 节点要求 trueOut/falseOut 必须各连一条。
        /// </para>
        /// </summary>
        public IActionValidator? Validator { get; set; }
    }

    /// <summary>
    /// 节点输出变量声明——描述节点会向黑板写入的变量。
    /// 使用 <see cref="OutputVar"/> 工厂创建。
    /// </summary>
    public class OutputVariableDefinition
    {
        /// <summary>黑板 key（运行时写入时使用的字符串键，如 "waveIndex"）</summary>
        public string Name { get; set; } = "";
        /// <summary>显示名称（中文，如 "当前波次"）</summary>
        public string DisplayName { get; set; } = "";
        /// <summary>值类型（"Int" / "Float" / "Bool" / "String"）</summary>
        public string Type { get; set; } = "String";
        /// <summary>作用域（"Local" / "Global"）</summary>
        public string Scope { get; set; } = "Global";
    }

    /// <summary>OutputVariableDefinition 工厂——简化声明语法。</summary>
    public static class OutputVar
    {
        public static OutputVariableDefinition Int(string name, string displayName, string scope = "Global")
            => new OutputVariableDefinition { Name = name, DisplayName = displayName, Type = "Int", Scope = scope };

        public static OutputVariableDefinition Float(string name, string displayName, string scope = "Global")
            => new OutputVariableDefinition { Name = name, DisplayName = displayName, Type = "Float", Scope = scope };

        public static OutputVariableDefinition Bool(string name, string displayName, string scope = "Global")
            => new OutputVariableDefinition { Name = name, DisplayName = displayName, Type = "Bool", Scope = scope };

        public static OutputVariableDefinition String(string name, string displayName, string scope = "Global")
            => new OutputVariableDefinition { Name = name, DisplayName = displayName, Type = "String", Scope = scope };
    }
}
