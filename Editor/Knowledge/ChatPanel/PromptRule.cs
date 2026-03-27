#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Editor.Settings;
using UnityEngine;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// 可配置 Prompt 规则。
    /// 每条规则包含一段追加到 SystemPrompt 尾部的文本，可独立开关。
    /// 支持内置规则（不可删除但可禁用）和用户自定义规则。
    /// Phase 2 起统一持久化到 <see cref="SceneBlueprintUserConfig"/> 的 Prompt section 中。
    /// </summary>
    [Serializable]
    public class PromptRule
    {
        /// <summary>规则唯一 ID</summary>
        public string Id = "";

        /// <summary>规则显示名</summary>
        public string Label = "";

        /// <summary>规则描述（鼠标悬停提示）</summary>
        public string Description = "";

        /// <summary>追加到 SystemPrompt 的文本</summary>
        public string Prompt = "";

        /// <summary>是否为内置规则（不可删除）</summary>
        public bool Builtin;

        /// <summary>是否启用</summary>
        public bool Enabled = true;
    }

    /// <summary>
    /// Prompt 规则管理器。
    /// 管理内置规则和用户自定义规则的加载、保存、增删。
    /// <para>
    /// Phase 2 起，该管理器不再读写 EditorPrefs，而是统一读写 UserConfig 中的 Prompt 配置。
    /// </para>
    /// <para>
    /// 设计目标是让 Prompt 最终生效结果完全由统一配置中心管理，
    /// 同时保留“内置规则 + 用户自定义规则”这一使用模型。
    /// </para>
    /// </summary>
    public static class PromptRuleManager
    {
        private static List<PromptRule>? _cachedRules;

        /// <summary>获取所有规则（内置 + 用户自定义）。</summary>
        public static List<PromptRule> GetRules()
        {
            _cachedRules ??= Load();
            return _cachedRules;
        }

        /// <summary>
        /// 保存规则到 UserConfig。
        /// <para>
        /// 内置规则和自定义规则都会被展开成统一的配置列表，保证启用状态完全由用户配置决定。
        /// </para>
        /// </summary>
        public static void Save()
        {
            if (_cachedRules == null) return;

            var user = SceneBlueprintSettingsService.User;
            user.Prompt.Rules.Clear();
            for (int i = 0; i < _cachedRules.Count; i++)
            {
                var rule = _cachedRules[i];
                user.Prompt.Rules.Add(new SceneBlueprintPromptRuleConfig
                {
                    Id = rule.Id,
                    Label = rule.Label,
                    Description = rule.Description,
                    Prompt = rule.Prompt,
                    Enabled = rule.Enabled,
                    Builtin = rule.Builtin,
                });
            }

            user.SaveConfig();
        }

        /// <summary>添加用户自定义规则。</summary>
        public static PromptRule AddCustomRule(string label, string prompt)
        {
            var rules = GetRules();
            var rule = new PromptRule
            {
                Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Label = label,
                Description = "用户自定义规则",
                Prompt = prompt,
                Builtin = false,
                Enabled = true,
            };
            rules.Add(rule);
            Save();
            return rule;
        }

        /// <summary>删除用户自定义规则（内置规则不可删除）。</summary>
        public static bool RemoveRule(string id)
        {
            var rules = GetRules();
            int idx = rules.FindIndex(r => r.Id == id);
            if (idx < 0 || rules[idx].Builtin) return false;
            rules.RemoveAt(idx);
            Save();
            return true;
        }

        /// <summary>组装所有已启用规则的 Prompt 文本，用于追加到 SystemPrompt 尾部。</summary>
        public static string AssembleEnabledPrompts()
        {
            var rules = GetRules();
            var sb = new System.Text.StringBuilder();
            bool any = false;
            foreach (var rule in rules)
            {
                if (!rule.Enabled || string.IsNullOrEmpty(rule.Prompt)) continue;
                if (!any)
                {
                    sb.AppendLine("\n【Prompt 规则】");
                    any = true;
                }
                sb.AppendLine($"- {rule.Prompt}");
            }
            return sb.ToString();
        }

        /// <summary>重置为默认内置规则（清除所有自定义）。</summary>
        public static void Reset()
        {
            _cachedRules = CreateBuiltinRules();
            Save();
        }

        // ══════════════════════════════════════
        //  内部实现
        // ══════════════════════════════════════

        private static List<PromptRule> Load()
        {
            var storedRules = SceneBlueprintSettingsService.User.Prompt.Rules;
            if (storedRules.Count > 0)
            {
                var rules = new List<PromptRule>(storedRules.Count);
                for (int i = 0; i < storedRules.Count; i++)
                {
                    var stored = storedRules[i];
                    rules.Add(new PromptRule
                    {
                        Id = stored.Id,
                        Label = stored.Label,
                        Description = stored.Description,
                        Prompt = stored.Prompt,
                        Enabled = stored.Enabled,
                        Builtin = stored.Builtin,
                    });
                }

                MergeBuiltinRules(rules);
                return rules;
            }

            var builtins = CreateBuiltinRules();
            _cachedRules = builtins;
            Save();
            return builtins;
        }

        /// <summary>确保所有内置规则都存在（用户可能删除了旧缓存中的内置规则）。</summary>
        private static void MergeBuiltinRules(List<PromptRule> existing)
        {
            var builtins = CreateBuiltinRules();
            var existingIds = new HashSet<string>();
            foreach (var r in existing) existingIds.Add(r.Id);

            foreach (var b in builtins)
            {
                if (!existingIds.Contains(b.Id))
                    existing.Insert(0, b);
            }

            for (int i = 0; i < existing.Count; i++)
            {
                var rule = existing[i];
                if (!rule.Builtin)
                {
                    continue;
                }

                for (int j = 0; j < builtins.Count; j++)
                {
                    var builtin = builtins[j];
                    if (builtin.Id != rule.Id)
                    {
                        continue;
                    }

                    rule.Label = builtin.Label;
                    rule.Description = builtin.Description;
                    rule.Prompt = builtin.Prompt;
                    rule.Builtin = true;
                    break;
                }
            }
        }

        private static List<PromptRule> CreateBuiltinRules()
        {
            return new List<PromptRule>
            {
                new()
                {
                    Id = "builtin_chinese", Label = "中文回复", Builtin = true, Enabled = true,
                    Description = "强制 AI 使用中文回复",
                    Prompt = "请始终使用中文回复，技术术语可保留英文原文但需附中文说明。"
                },
                new()
                {
                    Id = "builtin_data_driven", Label = "数据驱动", Builtin = true, Enabled = true,
                    Description = "回答必须基于实际数据，禁止编造",
                    Prompt = "回答必须基于实际蓝图数据和知识文档。如果需要更多信息，优先调用工具查询，而非猜测。"
                },
                new()
                {
                    Id = "builtin_table_format", Label = "表格格式化", Builtin = true, Enabled = false,
                    Description = "数据对比优先使用表格格式",
                    Prompt = "当需要对比多个选项或展示结构化数据时，优先使用 Markdown 表格格式。"
                },
                new()
                {
                    Id = "builtin_concise", Label = "简洁模式", Builtin = true, Enabled = true,
                    Description = "精炼直接，避免冗余",
                    Prompt = "回答简洁直接，不要添加不必要的前缀、客套话或重复信息。"
                },
                new()
                {
                    Id = "builtin_terminology", Label = "术语规范", Builtin = true, Enabled = true,
                    Description = "使用核心概念文档中定义的术语",
                    Prompt = "使用知识文档中定义的标准术语（如 Action/Marker/蓝图/节点/端口/字段），不要随意替换为其他说法。"
                },
                new()
                {
                    Id = "builtin_highlight_issues", Label = "异常高亮", Builtin = true, Enabled = true,
                    Description = "发现问题时主动标注",
                    Prompt = "如果在蓝图数据或用户描述中发现潜在问题（校验错误、配置不合理），请主动标注并给出修复建议。"
                },
                new()
                {
                    Id = "builtin_cite_source", Label = "引用来源", Builtin = true, Enabled = false,
                    Description = "回答中引用具体 Action/Marker 类型名",
                    Prompt = "回答中引用具体的 Action TypeId（如 Spawn.Wave）和 Marker TypeId（如 Area），方便用户对照。"
                },
                new()
                {
                    Id = "builtin_step_analysis", Label = "分步分析", Builtin = true, Enabled = false,
                    Description = "复杂问题分步骤解答",
                    Prompt = "对于复杂问题，先分析问题的各个方面，然后分步骤给出解决方案。"
                },
            };
        }

    }
}
