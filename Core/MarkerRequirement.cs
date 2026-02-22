#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Action 场景需求声明——描述一个 Action 需要什么类型的场景标记。
    /// <para>
    /// 放在 <see cref="ActionDefinition.SceneRequirements"/> 中，驱动：
    /// <list type="bullet">
    ///   <item>Scene View 右键菜单：根据 Action 的需求自动创建对应标记</item>
    ///   <item>Inspector 绑定 UI：自动生成标记绑定字段</item>
    ///   <item>验证逻辑：检查必需标记是否已绑定</item>
    ///   <item>多步创建流程：按声明顺序引导设计师逐步放置标记</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // Spawn.Preset 声明需要一个刷怪区域
    /// SceneRequirements = new[]
    /// {
    ///     new MarkerRequirement("spawnArea", MarkerTypeIds.Area,
    ///         required: true, displayName: "刷怪区域"),
    /// };
    /// </code>
    /// </example>
    public class MarkerRequirement
    {
        /// <summary>
        /// 绑定键名——与 <see cref="PropertyDefinition.Key"/> 类似，作为绑定映射的 key。
        /// <para>如 "spawnArea", "triggerArea", "cameraPosition"</para>
        /// </summary>
        public string BindingKey { get; set; } = "";

        /// <summary>
        /// 需要的标记类型 ID——对应 <see cref="MarkerTypeIds"/> 中定义的常量，
        /// 也可以是自定义的字符串 ID。
        /// <para>如 "Point", "Area"</para>
        /// </summary>
        public string MarkerTypeId { get; set; } = "";

        /// <summary>
        /// 是否必需——未绑定时显示警告，阻止导出
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// 是否允许绑定多个标记——如多个刷怪点
        /// </summary>
        public bool AllowMultiple { get; set; }

        /// <summary>
        /// 最少数量——当 <see cref="AllowMultiple"/> 为 true 时有效
        /// </summary>
        public int MinCount { get; set; }

        /// <summary>显示名称（用于 Inspector 和验证信息）</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>默认 Tag（创建标记时的默认分类标签）</summary>
        public string DefaultTag { get; set; } = "";

        /// <summary>无参构造函数（序列化需要）</summary>
        public MarkerRequirement() { }

        public MarkerRequirement(
            string bindingKey,
            string markerTypeId,
            bool required = false,
            bool allowMultiple = false,
            int minCount = 0,
            string displayName = "",
            string defaultTag = "")
        {
            BindingKey = bindingKey;
            MarkerTypeId = markerTypeId;
            Required = required;
            AllowMultiple = allowMultiple;
            MinCount = minCount;
            DisplayName = displayName;
            DefaultTag = defaultTag;
        }
    }
}
