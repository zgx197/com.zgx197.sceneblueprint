#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using UnityEngine;

namespace SceneBlueprint.Runtime.Settings
{
    /// <summary>
    /// AI Provider 预设项。
    /// <para>
    /// 用于定义框架内置的对话模型预设。它属于框架默认配置，
    /// 不是用户自定义模型，也不是项目级覆盖数据。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintAIProviderPreset
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
    /// Embedding Provider 预设项。
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintEmbeddingProviderPreset
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
    /// 框架内置 Prompt 模板。
    /// <para>
    /// 它代表框架层提供的默认 Prompt 规则模板，后续可作为 UserConfig 中实际启用规则的来源之一。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintPromptTemplate
    {
        [InspectorName("标识 ID")]
        public string Id = "";

        [InspectorName("标题 Label")]
        public string Label = "";

        [InspectorName("描述 Description")]
        public string Description = "";
        [TextArea(3, 8)]
        [InspectorName("提示词 Prompt")]
        public string Prompt = "";

        [InspectorName("默认启用 Enabled By Default")]
        public bool EnabledByDefault = true;
    }

    /// <summary>
    /// 框架默认值集合。
    /// <para>
    /// 这里存放的是“框架推荐默认值”，例如默认 TickRate、默认空间模式、默认时间舍入策略。
    /// 它们是基线值，而不是项目最终选择结果。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SceneBlueprintFrameworkDefaults
    {
        [InspectorName("默认 Tick 频率 Default Target Tick Rate")]
        public int DefaultTargetTickRate = 10;

        [InspectorName("默认时间舍入 Default Time Rounding Mode")]
        public BlueprintTimeRoundingMode DefaultTimeRoundingMode = BlueprintTimeRoundingMode.Ceil;

        [InspectorName("默认空间模式 Default Spatial Mode")]
        public string DefaultSpatialModeId = "Unity3D";

        /// <summary>
        /// 规范化框架默认值，避免 package 资产中出现非法配置。
        /// </summary>
        public void Normalize()
        {
            DefaultTargetTickRate = Mathf.Max(1, DefaultTargetTickRate);
            DefaultSpatialModeId = string.IsNullOrWhiteSpace(DefaultSpatialModeId)
                ? "Unity3D"
                : DefaultSpatialModeId.Trim();
        }
    }

    /// <summary>
    /// SceneBlueprint 框架默认配置资产。
    /// <para>
    /// 该资产位于 Package 中，职责是承载框架级默认配置与内置预设，
    /// 作为统一配置中心的“框架默认配置”数据源。
    /// </para>
    /// <para>
    /// 设计约束：
    /// 1. 它是框架基线，不是项目现场配置；
    /// 2. Phase 1/2 中优先读取，不默认写回 package；
    /// 3. 若资产缺失，则允许上层回退到代码默认值。
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneBlueprintFrameworkConfig",
        menuName = "SceneBlueprint/Framework Config",
        order = 60)]
    public sealed class SceneBlueprintFrameworkConfig : ScriptableObject
    {
        [InspectorName("AI Provider 预设 AI Providers")]
        [SerializeField] private List<SceneBlueprintAIProviderPreset> _aiProviders = new List<SceneBlueprintAIProviderPreset>();

        [InspectorName("Embedding Provider 预设 Embedding Providers")]
        [SerializeField] private List<SceneBlueprintEmbeddingProviderPreset> _embeddingProviders = new List<SceneBlueprintEmbeddingProviderPreset>();

        [InspectorName("Prompt 模板 Prompt Templates")]
        [SerializeField] private List<SceneBlueprintPromptTemplate> _promptTemplates = new List<SceneBlueprintPromptTemplate>();

        [InspectorName("框架默认值 Framework Defaults")]
        [SerializeField] private SceneBlueprintFrameworkDefaults _defaults = new SceneBlueprintFrameworkDefaults();

        /// <summary>框架内置 AI Provider 预设。</summary>
        public IReadOnlyList<SceneBlueprintAIProviderPreset> AIProviders => _aiProviders;

        /// <summary>框架内置 Embedding Provider 预设。</summary>
        public IReadOnlyList<SceneBlueprintEmbeddingProviderPreset> EmbeddingProviders => _embeddingProviders;

        /// <summary>框架内置 Prompt 模板。</summary>
        public IReadOnlyList<SceneBlueprintPromptTemplate> PromptTemplates => _promptTemplates;

        /// <summary>框架默认值集合。</summary>
        public SceneBlueprintFrameworkDefaults Defaults => _defaults;

        /// <summary>
        /// 资产校验入口。
        /// </summary>
        private void OnValidate()
        {
            _defaults.Normalize();
        }
    }
}
