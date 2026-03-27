#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Settings
{
    /// <summary>
    /// 用户自定义对话模型配置。
    /// <para>
    /// 该结构只保存“用户新增的模型项”，内置预设模型仍由框架层提供，
    /// 运行时/编辑器在读取时会将两者合并，避免把框架默认值复制到每个用户配置中。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintAIUserModelConfig
    {
        [InspectorName("标识 ID")]
        public string Id = "";

        [InspectorName("名称 Name")]
        public string Name = "";

        [InspectorName("提供商 Provider")]
        public string Provider = "";

        [InspectorName("接口地址 API URL")]
        public string ApiUrl = "";

        [InspectorName("模型名 Model")]
        public string Model = "";
    }

    /// <summary>
    /// 用户配置中的键值对条目。
    /// <para>
    /// 由于 Unity 序列化默认不直接支持通用 Dictionary，
    /// 这里使用列表形式保存“Key -> Value”数据，再由上层管理器做查找与去重。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintUserSecretEntry
    {
        [InspectorName("键 Key")]
        public string Key = "";

        [InspectorName("值 Value")]
        public string Value = "";
    }

    /// <summary>
    /// AI 相关的用户私有配置。
    /// <para>
    /// 该 section 用于取代旧的 EditorPrefs 存储，统一承载：
    /// 活跃模型、用户自定义模型、按模型隔离的 API Key、Embedding 模型与其 API Key。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintAIUserSettings
    {
        [InspectorName("当前激活模型 Active Model")]
        public string ActiveModelId = "moonshot_8k";

        [InspectorName("模型 API Keys Model API Keys")]
        public List<SceneBlueprintUserSecretEntry> ModelApiKeys = new List<SceneBlueprintUserSecretEntry>();

        [InspectorName("Embedding 模型 Embedding Model")]
        public string EmbeddingModelId = "";

        [InspectorName("Embedding API Keys Embedding API Keys")]
        public List<SceneBlueprintUserSecretEntry> EmbeddingApiKeys = new List<SceneBlueprintUserSecretEntry>();

        [InspectorName("自定义模型 Custom Models")]
        public List<SceneBlueprintAIUserModelConfig> CustomModels = new List<SceneBlueprintAIUserModelConfig>();
    }

    /// <summary>
    /// Prompt 规则配置项。
    /// <para>
    /// 这里同时承载内置规则与用户自定义规则的启用状态，
    /// 从而保证 Prompt 规则的最终生效结果完全由 UserConfig 决定。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintPromptRuleConfig
    {
        [InspectorName("标识 ID")]
        public string Id = "";

        [InspectorName("标题 Label")]
        public string Label = "";

        [InspectorName("描述 Description")]
        public string Description = "";

        [InspectorName("提示词 Prompt")]
        public string Prompt = "";

        [InspectorName("启用 Enabled")]
        public bool Enabled = true;

        [InspectorName("内置规则 Builtin")]
        public bool Builtin;
    }

    /// <summary>
    /// Prompt 相关的用户私有配置。
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintPromptUserSettings
    {
        [InspectorName("规则列表 Rules")]
        public List<SceneBlueprintPromptRuleConfig> Rules = new List<SceneBlueprintPromptRuleConfig>();
    }

    /// <summary>
    /// MCP Server 的用户私有配置。
    /// <para>
    /// - AutoStart: 打开蓝图主窗口时是否自动启动知识库服务
    /// - Port: 监听端口，0 表示使用框架默认端口
    /// Host 字段暂时保留为未来扩展位，当前服务仅消费 Port。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintMcpUserSettings
    {
        [InspectorName("自动启动 Auto Start")]
        public bool AutoStart = true;

        [InspectorName("端口 Port")]
        public int Port = 0;

        [InspectorName("主机 Host")]
        public string Host = "localhost";
    }

    /// <summary>
    /// 编辑器 UI 的用户本地偏好。
    /// <para>
    /// 这类数据不应进入项目配置，也不应散落到 EditorPrefs，
    /// 因此统一收口到 UserConfig 的 UI section 中。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintEditorUiSettings
    {
        [InspectorName("显示工作台 Show Workbench")]
        public bool ShowWorkbench = true;

        [InspectorName("显示 AI 聊天 Show AI Chat")]
        public bool ShowAIChat;

        [InspectorName("工作台宽度 Workbench Width")]
        public float WorkbenchWidth = 300f;

        [InspectorName("分析面板高度 Analysis Height")]
        public float AnalysisHeight = 160f;

        [InspectorName("折叠分析面板 Collapse Analysis Panel")]
        public bool CollapseAnalysisPanel;

        [InspectorName("当前页签索引 Selected Tab Index")]
        public int SelectedTabIndex;

        [InspectorName("窗口最小宽度 Window Min Width")]
        public float WindowMinWidth = 520f;

        [InspectorName("窗口最小高度 Window Min Height")]
        public float WindowMinHeight = 580f;
    }

    /// <summary>
    /// 蓝图主窗口的本地工作区恢复配置。
    /// <para>
    /// 该 section 只承载“当前用户最近一次在蓝图主窗口中打开的资产上下文”，
    /// 属于纯编辑器态工作区信息，不应进入项目资产或团队共享配置。
    /// </para>
    /// <para>
    /// 设计约束：
    /// 1. 资产定位优先使用 GUID，避免资源移动/重命名后恢复失效；
    /// 2. Path 仅作为可读回显与 GUID 失效时的降级线索；
    /// 3. ScenePath 用于区分“图内容恢复”和“场景上下文恢复”。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintEditorWorkspaceSettings
    {
        [InspectorName("打开窗口时恢复上次蓝图 Restore Last Blueprint On Open")]
        public bool RestoreLastBlueprintOnOpen = true;

        [InspectorName("启用本地草稿自动保存 Enable Local Draft Autosave")]
        public bool EnableLocalDraftAutosave = true;

        [InspectorName("草稿自动保存间隔秒数 Draft Autosave Interval Seconds")]
        public int DraftAutosaveIntervalSeconds = 90;

        [InspectorName("草稿空闲保存延迟秒数 Draft Autosave Idle Delay Seconds")]
        public int DraftAutosaveIdleDelaySeconds = 10;

        [InspectorName("最近蓝图 GUID Last Blueprint GUID")]
        public string LastOpenedBlueprintAssetGuid = "";

        [InspectorName("最近蓝图路径 Last Blueprint Path")]
        public string LastOpenedBlueprintAssetPath = "";

        [InspectorName("最近匿名草稿 ID Last Anonymous Draft ID")]
        public string LastAnonymousDraftId = "";

        [InspectorName("最近锚定场景路径 Last Anchored Scene Path")]
        public string LastAnchoredScenePath = "";
    }

    /// <summary>
    /// SceneBlueprint 用户配置。
    /// <para>
    /// 这是统一配置中心中的“个人配置”容器，落在 UserSettings 目录中，
    /// 用于替代旧的 EditorPrefs，保存 AI、Prompt、MCP 与编辑器 UI 偏好等本地私有数据。
    /// </para>
    /// <para>
    /// 选择 ScriptableSingleton 而不是普通 ScriptableObject 的原因是：
    /// 1. 无需手动创建资产；
    /// 2. 保存路径固定且稳定；
    /// 3. 天然适合本地单实例配置。
    /// </para>
    /// </summary>
    [FilePath("UserSettings/SceneBlueprintSettings/SceneBlueprintUserConfig.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class SceneBlueprintUserConfig : ScriptableSingleton<SceneBlueprintUserConfig>
    {
        [InspectorName("AI 用户配置 AI Settings")]
        public SceneBlueprintAIUserSettings AI = new SceneBlueprintAIUserSettings();

        [InspectorName("Prompt 用户配置 Prompt Settings")]
        public SceneBlueprintPromptUserSettings Prompt = new SceneBlueprintPromptUserSettings();

        [InspectorName("MCP 用户配置 MCP Settings")]
        public SceneBlueprintMcpUserSettings Mcp = new SceneBlueprintMcpUserSettings();

        [InspectorName("编辑器界面配置 Editor UI Settings")]
        public SceneBlueprintEditorUiSettings UI = new SceneBlueprintEditorUiSettings();

        [InspectorName("编辑器工作区配置 Editor Workspace Settings")]
        public SceneBlueprintEditorWorkspaceSettings Workspace = new SceneBlueprintEditorWorkspaceSettings();

        /// <summary>
        /// 获取用户配置单例。
        /// <para>
        /// 调用该方法本身不会做旧数据迁移，只保证当前统一配置中心对应的配置实例存在。
        /// </para>
        /// </summary>
        public static SceneBlueprintUserConfig GetOrCreate()
        {
            return instance;
        }

        /// <summary>
        /// 保存用户配置到 UserSettings。
        /// </summary>
        public void SaveConfig()
        {
            Save(true);
        }
    }
}
