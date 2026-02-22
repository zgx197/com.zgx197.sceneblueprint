#nullable enable

namespace SceneBlueprint.Editor.Logging
{
    /// <summary>
    /// 预定义的日志模块 Tag 常量。
    /// <para>Tag 不限于此处定义——任意字符串均可，预定义仅为统一命名和自动补全。</para>
    /// </summary>
    public static class SBLogTags
    {
        /// <summary>蓝图加载/保存/创建</summary>
        public const string Blueprint = "Blueprint";

        /// <summary>绑定上下文/同步到场景</summary>
        public const string Binding = "Binding";

        /// <summary>双向联动/选中高亮</summary>
        public const string Selection = "Selection";

        /// <summary>Gizmo 绘制管线</summary>
        public const string Pipeline = "Pipeline";

        /// <summary>标记创建/删除/编辑</summary>
        public const string Marker = "Marker";

        /// <summary>验证系统</summary>
        public const string Validator = "Validator";

        /// <summary>导出</summary>
        public const string Export = "Export";

        /// <summary>图层系统</summary>
        public const string Layer = "Layer";

        /// <summary>注册表加载（ActionRegistry / MarkerRegistry）</summary>
        public const string Registry = "Registry";

        /// <summary>模板系统（ActionTemplateSO / BlueprintTemplateSO）</summary>
        public const string Template = "Template";
    }
}
