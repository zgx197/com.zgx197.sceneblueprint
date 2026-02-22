#nullable enable
using UnityEditor;
using UnityEngine;
using System.IO;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// BlueprintRuntimeSettings 编辑器菜单工具——提供快捷访问和创建配置文件的功能。
    /// </summary>
    public static class BlueprintRuntimeSettingsMenu
    {
        private const string MenuPath = "SceneBlueprint/打开运行时设置";
        private const int MenuPriority = 1000;

        /// <summary>没有现成配置时自动在此路径创建。路径必须居于 Resources/SceneBlueprint/ 下才能被 Resources.Load 读到。</summary>
        private const string DefaultAssetPath = "Assets/Resources/SceneBlueprint/SceneBlueprintRuntimeSettings.asset";

        /// <summary>
        /// 菜单项：打开运行时设置
        /// <para>如果配置文件不存在，自动创建并打开</para>
        /// </summary>
        [MenuItem(MenuPath, priority = MenuPriority)]
        private static void OpenRuntimeSettings()
        {
            var settings = GetOrCreateSettings();
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        /// <summary>
        /// 获取或创建运行时设置文件
        /// </summary>
        private static Runtime.BlueprintRuntimeSettings? GetOrCreateSettings()
        {
            // 尝试从 Resources 加载
            var settings = Resources.Load<Runtime.BlueprintRuntimeSettings>("SceneBlueprint/SceneBlueprintRuntimeSettings");
            
            if (settings != null)
            {
                return settings;
            }

            // 配置文件不存在，创建新的
            UnityEngine.Debug.Log("[BlueprintRuntimeSettings] 配置文件不存在，正在创建...");

            // 确保目录存在
            string directory = Path.GetDirectoryName(DefaultAssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                UnityEngine.Debug.Log($"[BlueprintRuntimeSettings] 创建目录: {directory}");
            }

            // 创建配置文件
            settings = ScriptableObject.CreateInstance<Runtime.BlueprintRuntimeSettings>();
            AssetDatabase.CreateAsset(settings, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"[BlueprintRuntimeSettings] 配置文件已创建: {DefaultAssetPath}");

            return settings;
        }

        /// <summary>
        /// 验证菜单项是否可用（配置文件存在时显示为已选中）
        /// </summary>
        [MenuItem(MenuPath, true, MenuPriority)]
        private static bool ValidateOpenRuntimeSettings()
        {
            // 菜单项始终可用
            return true;
        }
    }
}
