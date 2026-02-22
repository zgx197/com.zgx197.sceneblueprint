#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  属性定义 (PropertyDefinition)
    //
    //  属性是行动节点上可编辑的字段，类似 GAS 中的 GameplayAttribute。
    //  PropertyDefinition 描述属性的元数据（类型、默认值、约束等），
    //  而实际的值存储在 PropertyBag 中。
    //
    //  设计思路：
    //  - 属性定义是“声明式”的——你声明一个属性是什么类型、叫什么名字、
    //    有什么约束，Inspector 就能自动生成对应的 UI 控件
    //  - 支持条件可见性（VisibleWhen）——某些属性只在特定条件下显示
    //  - 预留 AI Director 支持——标记哪些属性可被 AI 动态调整
    //
    //  与 GAS 的映射：
    //    PropertyDefinition  ↔  GameplayAttribute 的声明
    //    PropertyBag         ↔  AttributeSet 的实例数据
    //    Prop.工厂方法       ↔  UPROPERTY() 宏
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 属性值类型——决定 Inspector 生成什么样的 UI 控件
    /// </summary>
    public enum PropertyType
    {
        /// <summary>浮点数 → Slider 或 FloatField</summary>
        Float,
        /// <summary>整数 → IntField</summary>
        Int,
        /// <summary>布尔 → Toggle</summary>
        Bool,
        /// <summary>字符串 → TextField</summary>
        String,
        /// <summary>枚举 → Popup 下拉菜单</summary>
        Enum,
        /// <summary>资产引用 → ObjectField（如怪物模板、技能配置等）</summary>
        AssetRef,
        /// <summary>二维向量 → Vector2Field</summary>
        Vector2,
        /// <summary>三维向量 → Vector3Field</summary>
        Vector3,
        /// <summary>颜色 → ColorField</summary>
        Color,
        /// <summary>标签 → Tag 选择器（Phase 5 实现）</summary>
        Tag,
        /// <summary>场景绑定 → 场景对象拖拽框（如刷怪区域、Boss 出场点）</summary>
        SceneBinding,
        /// <summary>结构化列表 → Inspector 中显示为 ReorderableList，每个元素包含多个子字段</summary>
        StructList,
        /// <summary>Blackboard 变量选择器 → Popup 下拉，选项来自当前蓝图的变量声明列表</summary>
        VariableSelector
    }

    /// <summary>
    /// 属性定义——声明一个行动拥有的可编辑字段。
    /// <para>
    /// 这是整个属性系统的核心类，承担以下职责：
    /// <list type="bullet">
    ///   <item>Inspector 自动生成——根据 Type 自动选择 UI 控件（Slider / TextField / Popup 等）</item>
    ///   <item>序列化/反序列化——属性的 Key 作为 JSON 的键名导出</item>
    ///   <item>验证规则——Min/Max 限制数值范围</item>
    ///   <item>动态 UI——VisibleWhen 控制属性在特定条件下才显示</item>
    ///   <item>AI Director 集成——标记哪些属性可被 AI 动态调整（Phase 6+）</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <example>
    /// 创建方式（推荐使用 <see cref="Prop"/> 工厂）：
    /// <code>
    /// // 浮点数属性，带范围约束和条件可见性
    /// Prop.Float("interval", "刷怪间隔(秒)", defaultValue: 2f, min: 0.1f, max: 30f,
    ///     visibleWhen: "tempoType == Interval", category: "节奏")
    /// 
    /// // 枚举属性，自动提取选项
    /// Prop.Enum&lt;ActionDuration&gt;("duration", "持续类型")
    /// </code>
    /// </example>
    public class PropertyDefinition
    {
        // ─── 基础信息 ───

        /// <summary>
        /// 属性键名，如 "interval", "template", "monstersPerWave"
        /// <para>在同一个 ActionDefinition 内必须唯一。
        /// 会作为 PropertyBag 的 key 和导出 JSON 的键名。</para>
        /// </summary>
        public string Key { get; set; } = "";

        /// <summary>
        /// 编辑器 Inspector 中显示的名称，如 "刷怪间隔", "怪物模板"
        /// <para>建议使用中文，让策划可以直观理解</para>
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 属性值类型——决定 Inspector 生成的 UI 控件类型
        /// </summary>
        public PropertyType Type { get; set; }

        // ─── 默认值 ───

        /// <summary>
        /// 默认值——创建节点时自动填充到 PropertyBag 中。
        /// <para>为 null 表示无默认值，节点创建后该属性不会被自动设置。</para>
        /// </summary>
        public object? DefaultValue { get; set; }

        // ─── UI 提示 ───

        /// <summary>鼠标悬停时显示的提示文本，辅助策划理解该属性的用途</summary>
        public string? Tooltip { get; set; }

        /// <summary>
        /// Inspector 中的分组名，如 "约束", "节奏"
        /// <para>相同 Category 的属性会被分到同一个 Foldout 分组中。
        /// 为 null 的属性显示在“默认”分组。</para>
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// 排列顺序——小值在前，控制属性在 Inspector 中的上下顺序
        /// </summary>
        public int Order { get; set; }

        // ─── 约束（仅对数值类型有效） ───

        /// <summary>数值最小值（对 Float/Int 类型有效）。Inspector 中会以 Slider 或输入框限制展示。</summary>
        public float? Min { get; set; }

        /// <summary>数值最大值（对 Float/Int 类型有效）。同时有 Min 和 Max 时会显示为 Slider。</summary>
        public float? Max { get; set; }

        /// <summary>
        /// 枚举选项名数组（仅 Enum 类型时有效）。
        /// <para>通过 Prop.Enum&lt;T&gt;() 工厂方法会自动从 C# 枚举类型提取。</para>
        /// </summary>
        public string[]? EnumOptions { get; set; }

        /// <summary>
        /// 资产引用的类型过滤全名（仅 AssetRef 类型时有效）。
        /// <para>如 "MonsterGroupTemplate"，限制 ObjectField 只能选择指定类型的资产。</para>
        /// </summary>
        public string? AssetFilterTypeName { get; set; }

        /// <summary>
        /// 场景绑定类型（仅 SceneBinding 类型时有效）。
        /// <para>决定导出时的数据格式：Transform 导出坐标，Area 导出顶点数组。</para>
        /// </summary>
        public BindingType? SceneBindingType { get; set; }

        // ─── 条件可见性 ───

        /// <summary>
        /// 条件表达式——控制该属性在 Inspector 中是否可见。
        /// <para>
        /// 为 null 或空字符串时始终可见。
        /// 支持的操作符：==, !=, &gt;, &lt;, ||, &amp;&amp;
        /// </para>
        /// <example>
        /// "tempoType == Interval"  → 只在 tempoType 为 Interval 时显示
        /// "waves &gt; 1"             → 只在 waves 大于 1 时显示
        /// </example>
        /// </summary>
        public string? VisibleWhen { get; set; }

        // ─── AI Director 支持（Phase 6+ 实现） ───

        /// <summary>
        /// 是否允许 AI Director 在运行时动态调整该属性值。
        /// <para>默认 false，表示该属性只能在编辑器中手动设置。</para>
        /// </summary>
        public bool DirectorControllable { get; set; }

        /// <summary>
        /// AI 调整权限，范围 0~1。
        /// <para>
        /// 0 = 完全固定（使用编辑器设定的值）
        /// 1 = 完全由 AI 决定（在 Min~Max 范围内自由选择）
        /// 0.5 = AI 可在中间范围微调
        /// </para>
        /// </summary>
        public float DirectorInfluence { get; set; }

        // ─── StructList 专用字段 ───

        /// <summary>
        /// 子字段定义（仅 StructList 类型时有效）。
        /// 描述列表中每个元素的结构——每个元素是一组 key-value 对，
        /// 子字段的 PropertyDefinition 描述每个 key 的类型和约束。
        /// </summary>
        public PropertyDefinition[]? StructFields { get; set; }

        /// <summary>
        /// StructList 的摘要格式模板（仅 StructList 类型时有效）。
        /// 用于节点内容区域的摘要显示。
        /// 示例："波次: {count} 波" — {count} 会被替换为列表长度。
        /// </summary>
        public string? SummaryFormat { get; set; }

        // ─── 动态类型字段 ───

        /// <summary>
        /// 类型来源属性键（仅 String 类型且需要随变量类型动态切换控件时设置）。
        /// <para>
        /// 指向同节点内一个 <see cref="PropertyType.VariableSelector"/> 属性的 Key。
        /// Inspector 会读取该属性当前选中的变量类型，并将本字段渲染为对应控件：
        /// Int → IntField, Float → FloatField, Bool → Toggle, String/其他 → TextField。
        /// 值始终以字符串形式存储，控件切换只影响 UI。
        /// </para>
        /// </summary>
        public string? TypeSourceKey { get; set; }
    }
}
