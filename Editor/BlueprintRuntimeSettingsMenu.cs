#nullable enable
using UnityEditor;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 旧运行时设置菜单。
    /// <para>
    /// 统一配置中心落地后，该入口不再直接创建或打开旧运行时配置资产，
    /// 而是统一跳转到 Settings Hub 的项目配置页。
    /// </para>
    /// </summary>
    public static class BlueprintRuntimeSettingsMenu
    {
        private const string MenuPath = "SceneBlueprint/打开运行时设置";
        private const int MenuPriority = 1000;

        /// <summary>
        /// 菜单项：打开运行时设置。
        /// <para>
        /// 现阶段统一跳转到新的配置中心，避免继续分裂入口。
        /// </para>
        /// </summary>
        [MenuItem(MenuPath, priority = MenuPriority)]
        private static void OpenRuntimeSettings()
        {
            Settings.SceneBlueprintSettingsHubWindow.ShowWindow();
        }

        /// <summary>
        /// 验证菜单项是否可用。
        /// </summary>
        [MenuItem(MenuPath, true, MenuPriority)]
        private static bool ValidateOpenRuntimeSettings()
        {
            return true;
        }
    }
}
