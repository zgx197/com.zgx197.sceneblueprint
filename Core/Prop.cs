#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  属性便捷工厂 (Prop)
    //
    //  Prop 是 PropertyDefinition 的便捷工厂，类似于 UE 中的 UPROPERTY() 宏。
    //  它让定义属性变得简洁可读，不需要每次都手动 new PropertyDefinition。
    //
    //  对比：
    //  ── 不用工厂（繁琐）：
    //    new PropertyDefinition { Key = "interval", DisplayName = "间隔",
    //        Type = PropertyType.Float, DefaultValue = 2f, Min = 0.1f, Max = 30f }
    //
    //  ── 用工厂（简洁）：
    //    Prop.Float("interval", "间隔", defaultValue: 2f, min: 0.1f, max: 30f)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// PropertyDefinition 便捷工厂——提供每种属性类型的快捷创建方法。
    /// <para>
    /// 使用示例：
    /// <code>
    /// Properties = new[] {
    ///     Prop.Float("interval", "刷怪间隔(秒)", defaultValue: 2f, min: 0.1f, max: 30f),
    ///     Prop.Int("count", "每波数量", defaultValue: 5, min: 1, max: 20),
    ///     Prop.Enum("mode", "节奏类型", new[] { "Instant", "Interval", "Burst" }),
    ///     Prop.SceneBinding("area", "刷怪区域", BindingType.Area),
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public static class Prop
    {
        /// <summary>
        /// 创建浮点数属性。同时指定 min 和 max 时，Inspector 会显示为 Slider。
        /// </summary>
        /// <param name="key">属性键名，如 "interval"</param>
        /// <param name="displayName">显示名，如 "刷怪间隔(秒)"</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="min">最小值约束（可选）</param>
        /// <param name="max">最大值约束（可选）</param>
        /// <param name="category">Inspector 分组名</param>
        /// <param name="tooltip">悬停提示</param>
        /// <param name="visibleWhen">条件可见性表达式</param>
        /// <param name="order">排序顺序</param>
        public static PropertyDefinition Float(string key, string displayName,
            float defaultValue = 0f, float? min = null, float? max = null,
            string? category = null, string? tooltip = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.Float,
                DefaultValue = defaultValue,
                Min = min,
                Max = max,
                Category = category,
                Tooltip = tooltip,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }

        /// <summary>
        /// 创建整数属性。Min/Max 使用 int 类型参数，内部转为 float 存储。
        /// </summary>
        public static PropertyDefinition Int(string key, string displayName,
            int defaultValue = 0, int? min = null, int? max = null,
            string? category = null, string? tooltip = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.Int,
                DefaultValue = defaultValue,
                // int? 转为 float? 存储，因为 PropertyDefinition.Min/Max 统一用 float
                Min = min.HasValue ? (float)min.Value : (float?)null,
                Max = max.HasValue ? (float)max.Value : (float?)null,
                Category = category,
                Tooltip = tooltip,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }

        /// <summary>创建布尔属性。Inspector 中显示为 Toggle。</summary>
        public static PropertyDefinition Bool(string key, string displayName,
            bool defaultValue = false, string? category = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.Bool,
                DefaultValue = defaultValue,
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }

        /// <summary>创建字符串属性。Inspector 中显示为 TextField。
        /// <para>若指定 <paramref name="typeSourceKey"/>，Inspector 会根据该属性指向的变量类型动态切换控件
        /// （Int→IntField, Float→FloatField, Bool→Toggle, String→TextField）。值始终以字符串形式存储。</para>
        /// </summary>
        public static PropertyDefinition String(string key, string displayName,
            string defaultValue = "", string? category = null, string? visibleWhen = null, int order = 0,
            string? typeSourceKey = null)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.String,
                DefaultValue = defaultValue,
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order,
                TypeSourceKey = typeSourceKey
            };
        }

        /// <summary>
        /// 创建枚举属性（泛型版）——自动从 C# 枚举类型提取所有选项。
        /// <para>示例：Prop.Enum&lt;ActionDuration&gt;("duration", "持续类型")</para>
        /// </summary>
        /// <typeparam name="T">C# 枚举类型</typeparam>
        public static PropertyDefinition Enum<T>(string key, string displayName,
            string? category = null, string? visibleWhen = null, int order = 0) where T : struct, Enum
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.Enum,
                DefaultValue = default(T).ToString(),
                // 自动从 C# 枚举类型提取所有选项名
                EnumOptions = System.Enum.GetNames(typeof(T)),
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }

        /// <summary>
        /// 创建枚举属性（字符串数组版）——手动指定选项列表。
        /// <para>示例：Prop.Enum("mode", "模式", new[] { "Instant", "Interval", "Burst" })</para>
        /// </summary>
        /// <param name="options">枚举选项名数组</param>
        /// <param name="defaultValue">默认值，为 null 时使用第一个选项</param>
        public static PropertyDefinition Enum(string key, string displayName,
            string[] options, string? defaultValue = null,
            string? category = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.Enum,
                // 未指定默认值时，自动使用第一个选项
                DefaultValue = defaultValue ?? (options.Length > 0 ? options[0] : ""),
                EnumOptions = options,
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }

        /// <summary>
        /// 创建资产引用属性。Inspector 中显示为 ObjectField。
        /// </summary>
        /// <param name="assetFilterTypeName">资产类型过滤，如 "MonsterGroupTemplate"</param>
        public static PropertyDefinition AssetRef(string key, string displayName,
            string assetFilterTypeName = "",
            string? category = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.AssetRef,
                DefaultValue = "",
                AssetFilterTypeName = assetFilterTypeName,
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }

        /// <summary>
        /// 创建场景绑定属性——引用场景中的对象（区域、点位、路径等）。
        /// </summary>
        /// <param name="bindingType">绑定类型：Transform/Area/Path/Collider</param>
        public static PropertyDefinition SceneBinding(string key, string displayName,
            BindingType bindingType,
            string? category = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.SceneBinding,
                SceneBindingType = bindingType,
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }

        /// <summary>
        /// 创建标签属性。Phase 5 实现时会显示为 Tag 选择器。
        /// </summary>
        public static PropertyDefinition Tag(string key, string displayName,
            string? category = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.Tag,
                DefaultValue = "",
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }
        /// <summary>
        /// 创建 Blackboard 变量选择器属性。
        /// Inspector 中渲染为下拉菜单，选项来自当前蓝图的变量声明列表。
        /// 存储值为变量的整数 Index（-1 = 未选择）。
        /// </summary>
        public static PropertyDefinition VariableSelector(string key, string displayName,
            int defaultValue = -1,
            string? category = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.VariableSelector,
                DefaultValue = defaultValue,
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }

        /// <summary>
        /// 创建结构化列表属性。
        /// Inspector 侧边面板中显示为 ReorderableList（可排序、可增删），
        /// 每个元素包含多个子字段。节点画布中只显示摘要文本。
        /// <para>
        /// 存储格式：JSON 数组字符串，如 [{"count":5,"intervalTicks":60},...]
        /// 导出时 ValueType = "json"，运行时直接解析 JSON。
        /// </para>
        /// </summary>
        /// <param name="fields">子字段定义数组，描述每个列表元素的结构</param>
        /// <param name="summaryFormat">摘要格式，如 "波次: {count} 波"，{count} 替换为列表长度</param>
        public static PropertyDefinition StructList(string key, string displayName,
            PropertyDefinition[] fields,
            string? summaryFormat = null,
            string? category = null, string? visibleWhen = null, int order = 0)
        {
            return new PropertyDefinition
            {
                Key = key,
                DisplayName = displayName,
                Type = PropertyType.StructList,
                DefaultValue = "[]", // 默认空列表，JSON 格式
                StructFields = fields,
                SummaryFormat = summaryFormat,
                Category = category,
                VisibleWhen = visibleWhen,
                Order = order
            };
        }
    }
}
