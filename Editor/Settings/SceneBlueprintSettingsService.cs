#nullable enable
using System.IO;
using SceneBlueprint.Editor.Knowledge;
using SceneBlueprint.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Settings
{
    /// <summary>
    /// SceneBlueprint 统一配置访问服务。
    /// <para>
    /// 这是统一配置中心在代码侧的总入口，用于屏蔽底层配置资产路径、自动创建逻辑与容器差异。
    /// 调用方不应该再自行拼接路径、手动创建资产、或直接散读 `UserSettings / Assets / Package` 中的配置文件。
    /// </para>
    /// <para>
    /// 当前职责：
    /// 1. 提供三类配置容器的统一访问入口；
    /// 2. 在首次访问时自动创建项目配置与用户配置存储；
    /// 3. 暴露若干高频 section 的便捷访问属性；
    /// 4. 统一管理 MCP 端口与自动启动等个人配置读取规则。
    /// </para>
    /// </summary>
    public static class SceneBlueprintSettingsService
    {
        /// <summary>项目配置目录。</summary>
        public const string ProjectFolderPath = "Assets/SceneBlueprintSettings";

        /// <summary>项目配置资产路径。</summary>
        public const string ProjectAssetPath = ProjectFolderPath + "/SceneBlueprintProjectConfig.asset";

        /// <summary>框架默认配置资产路径（位于 Package）。</summary>
        public const string FrameworkAssetPath = "Packages/com.zgx197.sceneblueprint/Settings/SceneBlueprintFrameworkConfig.asset";

        /// <summary>用户配置目录（相对项目根目录）。</summary>
        public const string UserDirectoryRelativePath = "UserSettings/SceneBlueprintSettings";

        /// <summary>用户配置资产路径（相对项目根目录）。</summary>
        public const string UserAssetRelativePath = UserDirectoryRelativePath + "/SceneBlueprintUserConfig.asset";

        /// <summary>
        /// 获取框架默认配置。
        /// <para>
        /// 若 package 中未提供对应资产，则返回 null，由上层自行决定是否回退到代码默认值。
        /// </para>
        /// </summary>
        public static SceneBlueprintFrameworkConfig? Framework => LoadFrameworkConfig();

        /// <summary>
        /// 获取项目配置。
        /// <para>
        /// 若项目中尚未存在配置资产，会自动创建。
        /// </para>
        /// </summary>
        public static SceneBlueprintProjectConfig Project => GetOrCreateProjectConfig();

        /// <summary>
        /// 获取用户配置。
        /// <para>
        /// 若本地 UserSettings 目录不存在，会自动创建。
        /// </para>
        /// </summary>
        public static SceneBlueprintUserConfig User => GetOrCreateUserConfig();

        /// <summary>项目运行时配置 section 的便捷入口。</summary>
        public static SceneBlueprintRuntimeSettingsData Runtime => Project.Runtime;

        /// <summary>项目怪物映射 section 的便捷入口。</summary>
        public static SceneBlueprintMonsterMappingData MonsterMapping => SceneBlueprintMonsterMappingRegistry.GetSnapshot();

        /// <summary>项目怪物映射根设置的便捷入口。</summary>
        public static SceneBlueprintMonsterMappingProjectSettings MonsterMappingSettings => Project.MonsterMapping;

        /// <summary>项目刷怪编辑约束 section 的便捷入口。</summary>
        public static SceneBlueprintSpawnAuthoringProjectSettings SpawnAuthoringSettings => Project.SpawnAuthoring;

        /// <summary>用户 AI 配置 section 的便捷入口。</summary>
        public static SceneBlueprintAIUserSettings AIUser => User.AI;

        [InitializeOnLoadMethod]
        private static void InitializeOnEditorLoad()
        {
            EnsureStorages();
        }

        /// <summary>
        /// 确保三类配置所需的本地存储结构存在。
        /// </summary>
        public static void EnsureStorages()
        {
            EnsureProjectStorage();
            EnsureUserStorage();
        }

        /// <summary>
        /// 加载框架默认配置资产。
        /// </summary>
        public static SceneBlueprintFrameworkConfig? LoadFrameworkConfig()
        {
            return AssetDatabase.LoadAssetAtPath<SceneBlueprintFrameworkConfig>(FrameworkAssetPath);
        }

        /// <summary>
        /// 获取或创建项目配置资产。
        /// </summary>
        public static SceneBlueprintProjectConfig GetOrCreateProjectConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<SceneBlueprintProjectConfig>(ProjectAssetPath);
            if (config != null)
            {
                EnsureProjectConfigPreloaded(config);
                return config;
            }

            EnsureProjectFolder();

            config = ScriptableObject.CreateInstance<SceneBlueprintProjectConfig>();
            AssetDatabase.CreateAsset(config, ProjectAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EnsureProjectConfigPreloaded(config);
            return config;
        }

        /// <summary>
        /// 获取或创建用户配置单例。
        /// </summary>
        public static SceneBlueprintUserConfig GetOrCreateUserConfig()
        {
            EnsureUserStorage();
            var config = SceneBlueprintUserConfig.GetOrCreate();

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string userAssetPath = Path.Combine(projectRoot, UserAssetRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(userAssetPath))
            {
                config.SaveConfig();
            }

            return config;
        }

        /// <summary>
        /// 确保项目配置资产存在。
        /// </summary>
        public static void EnsureProjectStorage()
        {
            EnsureProjectFolder();
            var config = AssetDatabase.LoadAssetAtPath<SceneBlueprintProjectConfig>(ProjectAssetPath);
            if (config == null)
            {
                config = GetOrCreateProjectConfig();
            }

            if (config != null)
            {
                EnsureProjectConfigPreloaded(config);
            }

            SceneBlueprintMonsterMappingRegistry.EnsureRootFolder();
        }

        /// <summary>
        /// 确保用户配置目录存在。
        /// </summary>
        public static void EnsureUserStorage()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string userDirectory = Path.Combine(projectRoot, UserDirectoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(userDirectory))
            {
                Directory.CreateDirectory(userDirectory);
            }
        }

        /// <summary>
        /// 是否在打开蓝图主窗口时自动启动 MCP 知识服务。
        /// </summary>
        public static bool ShouldAutoStartKnowledgeServer()
        {
            return User.Mcp.AutoStart;
        }

        /// <summary>
        /// 获取 MCP 服务应使用的监听端口。
        /// <para>
        /// 配置值小于等于 0 时，回退到框架默认端口。
        /// </para>
        /// </summary>
        public static int GetKnowledgeServerPort()
        {
            int configuredPort = User.Mcp.Port;
            return configuredPort > 0 ? configuredPort : KnowledgeServer.DefaultPort;
        }

        /// <summary>
        /// 保存 MCP 用户配置。
        /// </summary>
        public static void SaveKnowledgeServerSettings(bool autoStart, int port)
        {
            var user = User;
            user.Mcp.AutoStart = autoStart;
            user.Mcp.Port = Mathf.Max(0, port);
            user.SaveConfig();
        }

        /// <summary>
        /// 确保项目配置目录存在。
        /// </summary>
        private static void EnsureProjectFolder()
        {
            if (AssetDatabase.IsValidFolder(ProjectFolderPath))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/SceneBlueprintSettings"))
            {
                AssetDatabase.CreateFolder("Assets", "SceneBlueprintSettings");
            }
        }

        private static void EnsureProjectConfigPreloaded(SceneBlueprintProjectConfig config)
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            for (int i = 0; i < preloadedAssets.Length; i++)
            {
                if (preloadedAssets[i] == config)
                {
                    return;
                }
            }

            var next = new Object[preloadedAssets.Length + 1];
            for (int i = 0; i < preloadedAssets.Length; i++)
            {
                next[i] = preloadedAssets[i];
            }

            next[^1] = config;
            PlayerSettings.SetPreloadedAssets(next);
        }
    }
}
