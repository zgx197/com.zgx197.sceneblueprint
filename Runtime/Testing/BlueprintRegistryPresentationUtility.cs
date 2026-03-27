#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Runtime.Testing
{
    public static class BlueprintRegistryPresentationUtility
    {
        public static string GetScopeTitle(BlueprintRegistryContractScope scope)
        {
            return scope switch
            {
                BlueprintRegistryContractScope.FrameworkDefault => "框架默认",
                BlueprintRegistryContractScope.ProjectBaseline => "项目基线",
                BlueprintRegistryContractScope.RuntimeSample => "运行时样例",
                BlueprintRegistryContractScope.CompatibilityBoundary => "兼容边界",
                _ => "未分类",
            };
        }

        public static string GetRuntimeSmokeEntryTitle(BlueprintRuntimeSmokeEntryKind entryKind)
        {
            return entryKind switch
            {
                BlueprintRuntimeSmokeEntryKind.RuntimeTestWindowLoadAndRun => "蓝图运行时测试窗口",
                BlueprintRuntimeSmokeEntryKind.RuntimeTestWindowReload => "蓝图运行时测试窗口重载",
                BlueprintRuntimeSmokeEntryKind.DemoHostReload => "Demo Host 重载",
                _ => "未分类入口",
            };
        }

        public static string BuildSmokeCatalogSummary(IReadOnlyList<BlueprintRuntimeSmokeDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
            {
                return "当前未注册正式最小冒烟入口。";
            }

            var titles = new string[definitions.Count];
            var entryTitle = GetRuntimeSmokeEntryTitle(definitions[0].EntryKind);
            var hasSingleEntryKind = true;

            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                titles[index] = definition.Title;
                if (definition.EntryKind != definitions[0].EntryKind)
                {
                    hasSingleEntryKind = false;
                }
            }

            var summary = $"当前正式最小冒烟入口（{definitions.Count} 项）：{string.Join(" / ", titles)}。";
            if (hasSingleEntryKind)
            {
                summary += $"默认从{entryTitle}触发。";
            }

            return summary;
        }
    }
}
