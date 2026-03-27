#nullable enable
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Settings
{
    /// <summary>
    /// 旧设置窗口兼容入口。
    /// <para>
    /// 该窗口不再承载独立设置逻辑，只负责把旧菜单入口统一重定向到 Settings Hub，
    /// 避免继续演化出第二套配置界面。
    /// </para>
    /// </summary>
    public sealed class BlueprintSettingsWindow : EditorWindow
    {
        public static BlueprintSettingsWindow ShowWindow()
        {
            var window = GetWindow<BlueprintSettingsWindow>();
            window.titleContent = new GUIContent("蓝图设置");
            window.minSize = new Vector2(420f, 180f);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            SceneBlueprintSettingsHubWindow.ShowWindow();
            Close();
        }
    }
}
