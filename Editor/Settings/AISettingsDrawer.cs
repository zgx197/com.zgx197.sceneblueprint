#nullable enable
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Editor.Knowledge;
using SceneBlueprint.Editor.Knowledge.ChatPanel;

namespace SceneBlueprint.Editor.Settings
{
    /// <summary>
    /// AI 助手设置面板的可复用绘制器。
    /// <para>
    /// 从 <see cref="AIChatPanel"/> 中提取的设置面板绘制逻辑，
    /// 可在 AIChatPanel 内联折叠面板和独立 Settings Window 中共享使用。
    /// 所有 UI 状态存储在 <see cref="AISettingsDrawerState"/> 中，调用方负责持有实例。
    /// </para>
    /// </summary>
    public static class AISettingsDrawer
    {
        /// <summary>
        /// 绘制完整的 AI 设置面板（对话模型 + Embedding + MCP + Prompt 规则 + 语义索引）。
        /// </summary>
        /// <param name="state">UI 状态（调用方持有）</param>
        /// <param name="boxStyle">外层 Box 样式（可为 null，使用默认 helpBox）</param>
        /// <param name="llmClient">LLM 客户端实例（用于 ReloadConfig，可为 null）</param>
        public static void Draw(AISettingsDrawerState state, GUIStyle? boxStyle = null, LLMClient? llmClient = null)
        {
            var style = boxStyle ?? EditorStyles.helpBox;
            EditorGUILayout.BeginVertical(style);
            {
                DrawModelSelector(llmClient, state);
                EditorGUILayout.Space(4);

                DrawApiKeyRow(state);
                EditorGUILayout.Space(6);

                DrawEmbeddingModelSelector(state);
                EditorGUILayout.Space(2);

                DrawStatusRow();
                EditorGUILayout.Space(4);

                DrawPromptRules(state);
                EditorGUILayout.Space(4);

                DrawEmbeddingSection(state);
            }
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════
        //  对话模型选择
        // ══════════════════════════════════════

        private static void DrawModelSelector(LLMClient? llmClient, AISettingsDrawerState state)
        {
            var configs = AiModelManager.GetConfigs();
            var activeId = AiModelManager.GetActiveId();

            var names = new string[configs.Count];
            int activeIdx = 0;
            for (int i = 0; i < configs.Count; i++)
            {
                names[i] = configs[i].Name;
                if (configs[i].Id == activeId) activeIdx = i;
            }

            EditorGUILayout.LabelField("对话模型", EditorStyles.miniLabel);
            int newIdx = EditorGUILayout.Popup(activeIdx, names);
            if (newIdx != activeIdx)
            {
                AiModelManager.SetActiveId(configs[newIdx].Id);
                llmClient?.ReloadConfig();
                state.ApiKeyInput = "";
            }
        }

        // ══════════════════════════════════════
        //  API Key
        // ══════════════════════════════════════

        private static void DrawApiKeyRow(AISettingsDrawerState state)
        {
            var activeId = AiModelManager.GetActiveId();
            EditorGUILayout.LabelField("API Key", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (string.IsNullOrEmpty(state.ApiKeyInput))
                state.ApiKeyInput = AiModelManager.GetApiKey(activeId);
            state.ApiKeyInput = EditorGUILayout.TextField(state.ApiKeyInput);
            if (GUILayout.Button("保存", EditorStyles.miniButton, GUILayout.Width(36)))
            {
                AiModelManager.SetApiKey(activeId, state.ApiKeyInput);
                UnityEngine.Debug.Log($"[AI] API Key 已保存 ({AiModelManager.GetActiveConfig().Name})");
            }
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════
        //  Embedding 模型 + Key
        // ══════════════════════════════════════

        private static void DrawEmbeddingModelSelector(AISettingsDrawerState state)
        {
            var presets = AiModelManager.GetEmbeddingPresets();
            var currentId = AiModelManager.GetEmbeddingId();

            var names = new string[presets.Length + 1];
            names[0] = "请选择 Embedding 模型...";
            int currentIdx = 0;
            for (int i = 0; i < presets.Length; i++)
            {
                names[i + 1] = presets[i].Name;
                if (presets[i].Id == currentId) currentIdx = i + 1;
            }

            EditorGUILayout.LabelField("Embedding 模型（必须配置）", EditorStyles.miniLabel);
            int newIdx = EditorGUILayout.Popup(currentIdx, names);
            if (newIdx != currentIdx)
            {
                if (newIdx == 0)
                    AiModelManager.SetEmbeddingId("");
                else
                    AiModelManager.SetEmbeddingId(presets[newIdx - 1].Id);
                state.EmbeddingApiKeyInput = "";
            }

            var embConfig = AiModelManager.GetEmbeddingConfig();
            if (!string.IsNullOrEmpty(embConfig.Id))
            {
                EditorGUILayout.LabelField("Embedding API Key", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                if (string.IsNullOrEmpty(state.EmbeddingApiKeyInput))
                    state.EmbeddingApiKeyInput = AiModelManager.GetEmbeddingApiKey();
                state.EmbeddingApiKeyInput = EditorGUILayout.TextField(state.EmbeddingApiKeyInput);
                if (GUILayout.Button("保存", EditorStyles.miniButton, GUILayout.Width(36)))
                {
                    state.EmbeddingApiKeyInput = state.EmbeddingApiKeyInput.Trim();
                    AiModelManager.SetEmbeddingApiKey(embConfig.Provider, state.EmbeddingApiKeyInput);
                    UnityEngine.Debug.Log($"[AI] Embedding API Key 已保存 ({embConfig.Name}), len={state.EmbeddingApiKeyInput.Length}");
                }
                EditorGUILayout.EndHorizontal();

                bool hasEmbKey = !string.IsNullOrEmpty(AiModelManager.GetEmbeddingApiKey());
                var prevColor = GUI.color;
                GUI.color = hasEmbKey ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.7f, 0.3f);
                EditorGUILayout.LabelField(
                    hasEmbKey ? "Embedding Key 已配置" : "请配置 Embedding API Key",
                    EditorStyles.miniLabel);
                GUI.color = prevColor;
            }
        }

        // ══════════════════════════════════════
        //  状态行（Key + MCP）
        // ══════════════════════════════════════

        private static void DrawStatusRow()
        {
            bool hasKey = AiModelManager.HasActiveApiKey();
            var prevColor = GUI.color;
            GUI.color = hasKey ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.7f, 0.3f);
            EditorGUILayout.LabelField(
                hasKey ? "Key 已配置" : "请配置 API Key",
                EditorStyles.miniLabel);
            GUI.color = prevColor;

            var service = KnowledgeService.Instance;
            EditorGUILayout.BeginHorizontal();
            {
                bool serverRunning = service.IsServerRunning;
                GUI.color = serverRunning ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.6f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(
                    serverRunning ? "MCP 运行中" : "MCP 未启动",
                    EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                GUI.color = prevColor;

                if (serverRunning)
                {
                    if (GUILayout.Button("停止", EditorStyles.miniButton, GUILayout.Width(36)))
                        service.StopServer();
                }
                else
                {
                    if (GUILayout.Button("启动", EditorStyles.miniButton, GUILayout.Width(36)))
                        service.StartServer(SceneBlueprintSettingsService.GetKnowledgeServerPort());
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════
        //  Prompt 规则
        // ══════════════════════════════════════

        private static void DrawPromptRules(AISettingsDrawerState state)
        {
            state.ShowRules = EditorGUILayout.Foldout(state.ShowRules, "Prompt 规则", true);
            if (!state.ShowRules) return;

            var rules = PromptRuleManager.GetRules();
            bool changed = false;

            foreach (var rule in rules)
            {
                EditorGUILayout.BeginHorizontal();
                bool newEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent(rule.Label, rule.Description),
                    rule.Enabled);
                if (newEnabled != rule.Enabled)
                {
                    rule.Enabled = newEnabled;
                    changed = true;
                }

                if (!rule.Builtin)
                {
                    if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        PromptRuleManager.RemoveRule(rule.Id);
                        changed = false;
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (changed) PromptRuleManager.Save();

            state.ShowAddCustomRule = EditorGUILayout.Foldout(state.ShowAddCustomRule, "添加自定义规则", true);
            if (state.ShowAddCustomRule)
            {
                state.CustomRuleLabel = EditorGUILayout.TextField("规则名", state.CustomRuleLabel);
                EditorGUILayout.LabelField("Prompt", EditorStyles.miniLabel);
                state.CustomRulePrompt = EditorGUILayout.TextArea(state.CustomRulePrompt, GUILayout.MinHeight(36));
                if (GUILayout.Button("添加", EditorStyles.miniButton))
                {
                    if (!string.IsNullOrEmpty(state.CustomRuleLabel) && !string.IsNullOrEmpty(state.CustomRulePrompt))
                    {
                        PromptRuleManager.AddCustomRule(state.CustomRuleLabel, state.CustomRulePrompt);
                        state.CustomRuleLabel = "";
                        state.CustomRulePrompt = "";
                        state.ShowAddCustomRule = false;
                    }
                }
            }
        }

        // ══════════════════════════════════════
        //  语义索引
        // ══════════════════════════════════════

        private static void DrawEmbeddingSection(AISettingsDrawerState state)
        {
            var service = KnowledgeService.Instance;
            var embedding = service.Embedding;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"语义索引（{embedding.ChunkCount} 块）",
                EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

            using (new EditorGUI.DisabledScope(embedding.IsIndexing))
            {
                if (GUILayout.Button(embedding.IsIndexing ? "索引中..." : "重建索引",
                    EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    embedding.IndexAllAsync(service.Registry,
                        status => state.EmbeddingStatus = status,
                        () => state.EmbeddingStatus = $"完成（{embedding.ChunkCount} 块）");
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(state.EmbeddingStatus))
            {
                var prevColor = GUI.color;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                EditorGUILayout.LabelField(state.EmbeddingStatus, EditorStyles.miniLabel);
                GUI.color = prevColor;
            }
        }
    }

    /// <summary>
    /// AI 设置面板的 UI 状态容器。调用方持有实例以保持状态跨帧连续。
    /// </summary>
    public class AISettingsDrawerState
    {
        public string ApiKeyInput = "";
        public string EmbeddingApiKeyInput = "";
        public bool ShowRules;
        public bool ShowAddCustomRule;
        public string CustomRuleLabel = "";
        public string CustomRulePrompt = "";
        public string EmbeddingStatus = "";
    }
}
