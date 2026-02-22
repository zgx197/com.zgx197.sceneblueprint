#nullable enable
using NodeGraph.Abstraction;
using NodeGraph.Core;
using NodeGraph.View;
using NodeGraph.Math;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Analysis.Rules;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Templates;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// SceneBlueprint 蓝图配置工厂。
    /// 创建并配置 BlueprintProfile，将 SceneBlueprint 的 Action 体系
    /// 完整注册到 NodeGraph 的节点类型系统中。
    /// </summary>
    public static class SceneBlueprintProfile
    {
        /// <summary>
        /// 创建 SceneBlueprint 专用的 BlueprintProfile。
        /// </summary>
        /// <param name="textMeasurer">文字测量器（由引擎层提供）</param>
        /// <returns>
        /// Profile、ActionRegistry（供调用方复用）、
        /// ActionRegistryNodeTypeCatalog（同时作为 GraphSettings.NodeTypes 注入 Graph）
        /// </returns>
        public static (BlueprintProfile Profile, ActionRegistry ActionRegistry, ActionRegistryNodeTypeCatalog Catalog)
            Create(ITextMeasurer textMeasurer)
        {
            // 1. 创建并填充 ActionRegistry
            var actionRegistry = BuildRegistry();

            // 2. P1-B: 直接用 ActionRegistryNodeTypeCatalog 适配，不再走 NodeTypeRegistry + Adapter
            var catalog = new ActionRegistryNodeTypeCatalog(actionRegistry);

            // 3. 创建通用内容渲染器
            var contentRenderer = new ActionContentRenderer(actionRegistry);

            // 4. 构建 BlueprintProfile
            var profile = new BlueprintProfile
            {
                Name = "SceneBlueprint",
                FrameBuilder = new DefaultFrameBuilder(textMeasurer),
                Theme = NodeVisualTheme.Dark,
                Topology = GraphTopologyPolicy.DAG,
                NodeTypes = catalog,
                Features = BlueprintFeatureFlags.MiniMap | BlueprintFeatureFlags.Search
            };

            foreach (var actionDef in actionRegistry.GetAll())
                profile.ContentRenderers[actionDef.TypeId] = contentRenderer;

            return (profile, actionRegistry, catalog);
        }

        /// <summary>
        /// 创建配置完成的 BlueprintAnalyzer（T4 Analyze Phase 入口）。
        /// 按推荐顺序注册内置规则：SB003 → SB001 → SB002 → SB006 → SB004 → SB005。
        /// </summary>
        public static BlueprintAnalyzer CreateAnalyzer(INodeTypeCatalog typeProvider, ActionRegistry actionRegistry)
        {
            return new BlueprintAnalyzer(typeProvider, actionRegistry)
                .AddRule(new MultipleStartRule())              // SB003：快速失败
                .AddRule(new ReachabilityRule())               // SB001：计算可达集合
                .AddRule(new RequiredPortRule())               // SB002：依赖可达集合
                .AddRule(new TypeValidationRule())             // SB006：类型级自定义验证（ActionDefinition.Validator）
                .AddRule(new DeadOutputRule())                 // SB004
                .AddRule(new IsolatedNodeRule());              // SB005
        }

        /// <summary>
        /// 获取 ActionRegistry 实例（用于外部查询已注册的 Action 类型）。
        /// 每次调用都会重新发现，适合在编辑器初始化时使用。
        /// </summary>
        public static ActionRegistry CreateActionRegistry() => BuildRegistry();

        private static ActionRegistry BuildRegistry()
        {
            var registry = new ActionRegistry();
            registry.AutoDiscover(UnityEditor.TypeCache.GetTypesDerivedFrom<Core.IActionDefinitionProvider>());
            RegisterTemplates(registry);
            ApplyCategoryThemeColors(registry);
            return registry;
        }

        /// <summary>
        /// 扫描项目中所有 ActionTemplateSO 资产，转换为 ActionDefinition 并注册。
        /// C# 已注册的 TypeId 不会被 SO 覆盖。
        /// </summary>
        private static void RegisterTemplates(ActionRegistry registry)
        {
            var guids = AssetDatabase.FindAssets("t:ActionTemplateSO");
            int registered = 0;
            int skipped = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<ActionTemplateSO>(path);
                if (template == null || string.IsNullOrEmpty(template.TypeId)) continue;

                // C# 已注册的 TypeId 不被 SO 覆盖
                if (registry.TryGet(template.TypeId, out _))
                {
                    SBLog.Warn(SBLogTags.Registry,
                        $"ActionTemplateSO '{template.name}' 的 TypeId '{template.TypeId}' " +
                        $"与 C# 定义冲突，已跳过 ({path})");
                    skipped++;
                    continue;
                }

                try
                {
                    var def = ActionTemplateConverter.Convert(template);
                    registry.Register(def);
                    registered++;
                }
                catch (System.Exception ex)
                {
                    SBLog.Error(SBLogTags.Registry,
                        $"ActionTemplateSO '{template.name}' 转换失败: {ex.Message}");
                }
            }

            if (registered > 0 || skipped > 0)
            {
                SBLog.Info(SBLogTags.Registry,
                    $"ActionTemplateSO 加载完成：注册 {registered} 个，跳过 {skipped} 个");
            }
        }

        /// <summary>
        /// 遍历所有已注册的 ActionDefinition，如果 ThemeColor 是默认灰色且存在匹配的 CategorySO，
        /// 则继承 CategorySO.ThemeColor。
        /// </summary>
        private static void ApplyCategoryThemeColors(ActionRegistry registry)
        {
            int inherited = 0;
            foreach (var def in registry.GetAll())
            {
                // 只处理使用默认灰色的 Action
                if (!IsDefaultGray(def.ThemeColor)) continue;

                var catColor = CategoryRegistry.GetThemeColor(def.Category);
                if (catColor.HasValue)
                {
                    var c = catColor.Value;
                    def.ThemeColor = new Color4(c.r, c.g, c.b, c.a);
                    inherited++;
                }
            }

            if (inherited > 0)
            {
                SBLog.Debug(SBLogTags.Template,
                    $"ThemeColor 继承：{inherited} 个 Action 从 CategorySO 继承了主题色");
            }
        }

        /// <summary>
        /// SceneBlueprint 子蓝图的默认边界端口：Input 激活 / Output 完成。
        /// <para>
        /// 收拢端口语义定义，避免在 Session 或窗口层硬编码端口名称和方向。
        /// </para>
        /// </summary>
        public static NodeGraph.Core.PortDefinition[] DefaultSubGraphBoundaryPorts { get; } =
        {
            new NodeGraph.Core.PortDefinition("激活", NodeGraph.Core.PortDirection.Input,  NodeGraph.Core.PortKind.Control, "exec", NodeGraph.Core.PortCapacity.Single, 0),
            new NodeGraph.Core.PortDefinition("完成", NodeGraph.Core.PortDirection.Output, NodeGraph.Core.PortKind.Control, "exec", NodeGraph.Core.PortCapacity.Single, 0),
        };

        /// <summary>判断 Color4 是否为默认灰色 (0.5, 0.5, 0.5, 1.0)</summary>
        private static bool IsDefaultGray(Color4 c)
        {
            const float eps = 0.01f;
            return System.Math.Abs(c.R - 0.5f) < eps &&
                   System.Math.Abs(c.G - 0.5f) < eps &&
                   System.Math.Abs(c.B - 0.5f) < eps &&
                   System.Math.Abs(c.A - 1.0f) < eps;
        }
    }
}
