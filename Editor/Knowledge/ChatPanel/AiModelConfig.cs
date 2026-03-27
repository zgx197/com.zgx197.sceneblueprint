#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Editor.Settings;
using UnityEngine;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// AI 模型配置。
    /// 每个配置包含 Provider、API URL、Model 名称。
    /// API Key 不再放在该对象中，而是由用户配置容器按规则单独持久化。
    /// </summary>
    [Serializable]
    public class AiModelConfig
    {
        /// <summary>配置唯一 ID</summary>
        public string Id = "";

        /// <summary>显示名（如 "Moonshot 8K"、"DeepSeek Chat"）</summary>
        public string Name = "";

        /// <summary>服务商标识</summary>
        public string Provider = "";

        /// <summary>API Base URL（如 "https://api.moonshot.cn/v1/chat/completions"）</summary>
        public string ApiUrl = "";

        /// <summary>模型名称（如 "moonshot-v1-8k"、"deepseek-chat"）</summary>
        public string Model = "";

        /// <summary>是否为预设模型（不可删除）</summary>
        public bool Preset;
    }

    /// <summary>
    /// 预设 Provider 定义。
    /// </summary>
    public static class AiProviders
    {
        public static readonly (string Id, string Name, string ApiUrl, string Model)[] Presets =
        {
            ("moonshot_8k", "Moonshot 8K", "https://api.moonshot.cn/v1/chat/completions", "moonshot-v1-8k"),
            ("moonshot_32k", "Moonshot 32K", "https://api.moonshot.cn/v1/chat/completions", "moonshot-v1-32k"),
            ("deepseek_chat", "DeepSeek Chat", "https://api.deepseek.com/v1/chat/completions", "deepseek-chat"),
            ("openai_gpt4o", "OpenAI GPT-4o", "https://api.openai.com/v1/chat/completions", "gpt-4o"),
            ("openai_gpt4o_mini", "OpenAI GPT-4o Mini", "https://api.openai.com/v1/chat/completions", "gpt-4o-mini"),
            ("qwen_plus", "Qwen Plus", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-plus"),
            ("qwen_turbo", "Qwen Turbo", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-turbo"),
            ("qwen35_flash", "Qwen 3.5 Flash", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen3.5-flash"),
        };

        /// <summary>
        /// 支持 Embedding API 的预设配置。
        /// 对话模型和 Embedding 模型可以来自不同 Provider。
        /// </summary>
        public static readonly (string Id, string Name, string Provider, string ApiUrl, string Model)[] EmbeddingPresets =
        {
            ("emb_openai_small", "OpenAI text-embedding-3-small", "openai", "https://api.openai.com/v1/embeddings", "text-embedding-3-small"),
            ("emb_openai_large", "OpenAI text-embedding-3-large", "openai", "https://api.openai.com/v1/embeddings", "text-embedding-3-large"),
            ("emb_qwen_v4", "Qwen text-embedding-v4", "dashscope", "https://dashscope.aliyuncs.com/api/v1/services/embeddings/text-embedding/text-embedding", "text-embedding-v4"),
        };
    }

    /// <summary>
    /// AI 模型配置管理器。
    /// 管理模型列表、活跃模型选择、API Key 的加载和保存。
    /// <para>
    /// Phase 2 起，该管理器不再直接读写 EditorPrefs，而是统一读写
    /// <see cref="SceneBlueprintUserConfig"/> 中的 AI section。
    /// </para>
    /// <para>
    /// 设计原则：
    /// 1. 内置预设仍由框架/代码提供；
    /// 2. 用户只持久化自定义模型、活跃模型、API Key 与 Embedding 选择；
    /// 3. 对话模型列表在运行时视角下是“预设 + 用户自定义”的合并结果。
    /// </para>
    /// </summary>
    public static class AiModelManager
    {
        private static List<AiModelConfig>? _cachedConfigs;

        // ══════════════════════════════════════
        //  模型配置管理
        // ══════════════════════════════════════

        /// <summary>获取所有模型配置。</summary>
        public static List<AiModelConfig> GetConfigs()
        {
            _cachedConfigs ??= LoadConfigs();
            return _cachedConfigs;
        }

        /// <summary>获取当前活跃模型配置。</summary>
        public static AiModelConfig GetActiveConfig()
        {
            var configs = GetConfigs();
            if (configs.Count == 0)
            {
                return new AiModelConfig();
            }

            var activeId = GetActiveId();
            var active = configs.Find(c => c.Id == activeId);
            return active ?? configs[0]; // 回退到第一个
        }

        /// <summary>获取活跃模型 ID。</summary>
        public static string GetActiveId()
        {
            string id = SceneBlueprintSettingsService.AIUser.ActiveModelId;
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            var configs = GetConfigs();
            return configs.Count > 0 ? configs[0].Id : "moonshot_8k";
        }

        /// <summary>设置活跃模型。</summary>
        public static void SetActiveId(string id)
        {
            var user = SceneBlueprintSettingsService.User;
            user.AI.ActiveModelId = id;
            user.SaveConfig();
        }

        /// <summary>添加自定义模型配置。</summary>
        public static AiModelConfig AddCustomConfig(string name, string apiUrl, string model)
        {
            var configs = GetConfigs();
            var config = new AiModelConfig
            {
                Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = name,
                Provider = "custom",
                ApiUrl = apiUrl,
                Model = model,
                Preset = false,
            };

            var userConfig = new SceneBlueprintAIUserModelConfig
            {
                Id = config.Id,
                Name = config.Name,
                Provider = config.Provider,
                ApiUrl = config.ApiUrl,
                Model = config.Model,
            };

            var user = SceneBlueprintSettingsService.User;
            user.AI.CustomModels.Add(userConfig);
            user.SaveConfig();

            configs.Add(config);
            return config;
        }

        /// <summary>删除自定义模型配置（预设不可删除）。</summary>
        public static bool RemoveConfig(string id)
        {
            var configs = GetConfigs();
            int idx = configs.FindIndex(c => c.Id == id);
            if (idx < 0 || configs[idx].Preset) return false;
            configs.RemoveAt(idx);

            var user = SceneBlueprintSettingsService.User;
            int userIdx = user.AI.CustomModels.FindIndex(c => c.Id == id);
            if (userIdx >= 0)
            {
                user.AI.CustomModels.RemoveAt(userIdx);
            }

            // 如果删除的是活跃模型，回退到第一个
            if (GetActiveId() == id && configs.Count > 0)
                SetActiveId(configs[0].Id);

            user.SaveConfig();
            return true;
        }

        /// <summary>
        /// 保存模型配置。
        /// <para>
        /// 当前仅需要把缓存中的“自定义模型”回写到 UserConfig；
        /// 预设模型始终由代码/框架提供，不持久化到用户配置中。
        /// </para>
        /// </summary>
        public static void SaveConfigs()
        {
            if (_cachedConfigs == null) return;

            var user = SceneBlueprintSettingsService.User;
            user.AI.CustomModels.Clear();
            for (int i = 0; i < _cachedConfigs.Count; i++)
            {
                var config = _cachedConfigs[i];
                if (config.Preset)
                {
                    continue;
                }

                user.AI.CustomModels.Add(new SceneBlueprintAIUserModelConfig
                {
                    Id = config.Id,
                    Name = config.Name,
                    Provider = config.Provider,
                    ApiUrl = config.ApiUrl,
                    Model = config.Model,
                });
            }

            user.SaveConfig();
        }

        // ══════════════════════════════════════
        //  API Key 管理（按模型 ID 隔离存储）
        // ══════════════════════════════════════

        /// <summary>获取指定模型的 API Key。</summary>
        public static string GetApiKey(string modelId)
        {
            return GetSecret(SceneBlueprintSettingsService.AIUser.ModelApiKeys, modelId);
        }

        /// <summary>设置指定模型的 API Key。</summary>
        public static void SetApiKey(string modelId, string key)
        {
            var user = SceneBlueprintSettingsService.User;
            SetSecret(user.AI.ModelApiKeys, modelId, key);
            user.SaveConfig();
        }

        /// <summary>获取当前活跃模型的 API Key。</summary>
        public static string GetActiveApiKey()
        {
            return GetApiKey(GetActiveId());
        }

        /// <summary>当前活跃模型是否已配置 API Key。</summary>
        public static bool HasActiveApiKey()
        {
            return !string.IsNullOrEmpty(GetActiveApiKey());
        }

        // ══════════════════════════════════════
        //  Embedding 模型配置（独立于对话模型）
        // ══════════════════════════════════════

        /// <summary>获取所有 Embedding 预设配置。</summary>
        public static (string Id, string Name, string Provider, string ApiUrl, string Model)[] GetEmbeddingPresets()
        {
            return AiProviders.EmbeddingPresets;
        }

        /// <summary>获取当前 Embedding Provider ID。</summary>
        public static string GetEmbeddingId()
        {
            return SceneBlueprintSettingsService.AIUser.EmbeddingModelId;
        }

        /// <summary>设置 Embedding Provider。</summary>
        public static void SetEmbeddingId(string id)
        {
            var user = SceneBlueprintSettingsService.User;
            user.AI.EmbeddingModelId = id;
            user.SaveConfig();
        }

        /// <summary>获取当前 Embedding 配置。</summary>
        public static (string Id, string Name, string Provider, string ApiUrl, string Model) GetEmbeddingConfig()
        {
            var embId = GetEmbeddingId();
            foreach (var preset in AiProviders.EmbeddingPresets)
            {
                if (preset.Id == embId) return preset;
            }
            // 未配置或 ID 无效时返回空配置
            return ("", "未配置", "none", "", "");
        }

        /// <summary>获取 Embedding 的 API Key。</summary>
        public static string GetEmbeddingApiKey()
        {
            var config = GetEmbeddingConfig();
            return GetSecret(SceneBlueprintSettingsService.AIUser.EmbeddingApiKeys, config.Provider);
        }

        /// <summary>设置 Embedding 的 API Key。</summary>
        public static void SetEmbeddingApiKey(string provider, string key)
        {
            var user = SceneBlueprintSettingsService.User;
            SetSecret(user.AI.EmbeddingApiKeys, provider, key);
            user.SaveConfig();
        }

        /// <summary>当前 Embedding 是否已配置（选择了有效模型且填写了 API Key）。</summary>
        public static bool HasEmbeddingSupport()
        {
            var config = GetEmbeddingConfig();
            if (string.IsNullOrEmpty(config.Id) || config.Provider == "none") return false;
            return !string.IsNullOrEmpty(GetEmbeddingApiKey());
        }

        /// <summary>Embedding 配置的显示名（用于标题栏）。</summary>
        public static string GetEmbeddingDisplayName()
        {
            var config = GetEmbeddingConfig();
            return config.Name;
        }

        // ══════════════════════════════════════
        //  内部实现
        // ══════════════════════════════════════

        private static List<AiModelConfig> LoadConfigs()
        {
            var result = CreatePresetConfigs();
            var customModels = SceneBlueprintSettingsService.AIUser.CustomModels;
            for (int i = 0; i < customModels.Count; i++)
            {
                var custom = customModels[i];
                if (string.IsNullOrEmpty(custom.Id))
                {
                    continue;
                }

                result.Add(new AiModelConfig
                {
                    Id = custom.Id,
                    Name = custom.Name,
                    Provider = custom.Provider,
                    ApiUrl = custom.ApiUrl,
                    Model = custom.Model,
                    Preset = false,
                });
            }

            return result;
        }

        private static List<AiModelConfig> CreatePresetConfigs()
        {
            var list = new List<AiModelConfig>();
            foreach (var (id, name, apiUrl, model) in AiProviders.Presets)
            {
                list.Add(new AiModelConfig
                {
                    Id = id, Name = name, Provider = id.Split('_')[0],
                    ApiUrl = apiUrl, Model = model, Preset = true,
                });
            }
            return list;
        }

        private static string GetSecret(List<SceneBlueprintUserSecretEntry> entries, string key)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Key == key)
                {
                    return entry.Value;
                }
            }

            return "";
        }

        private static void SetSecret(List<SceneBlueprintUserSecretEntry> entries, string key, string value)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key != key)
                {
                    continue;
                }

                entries[i].Value = value;
                return;
            }

            entries.Add(new SceneBlueprintUserSecretEntry
            {
                Key = key,
                Value = value,
            });
        }
    }
}
