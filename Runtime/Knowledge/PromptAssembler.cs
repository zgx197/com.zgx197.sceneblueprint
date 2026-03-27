#nullable enable
using System.Collections.Generic;
using System.Text;
using SceneBlueprint.Contract.Knowledge;

namespace SceneBlueprint.Runtime.Knowledge
{
    /// <summary>
    /// Prompt 组装器。
    /// 根据角色、实时上下文和检索到的知识文档，组装完整的 LLM Prompt。
    /// </summary>
    public class PromptAssembler
    {
        private readonly KnowledgeRegistry _registry;
        private readonly IBlueprintContextProvider? _contextProvider;

        public PromptAssembler(KnowledgeRegistry registry, IBlueprintContextProvider? contextProvider = null)
        {
            _registry = registry;
            _contextProvider = contextProvider;
        }

        /// <summary>
        /// 为指定角色组装完整的聊天消息列表。
        /// </summary>
        /// <param name="role">用户角色</param>
        /// <param name="userQuestion">用户问题</param>
        /// <param name="extraSystemSuffix">追加到 SystemPrompt 尾部的额外文本（如 PromptRule 规则）。
        /// 由调用方（Editor 层）负责组装，避免 Runtime 层引用 Editor 层。</param>
        /// <param name="semanticSnippets">第二层自动检索的语义片段（由 Editor 层 EmbeddingService 提供）。
        /// 如果为 null 或空，则跳过第二层检索。</param>
        /// <returns>LLM messages 数组（system + context + few-shot + user）</returns>
        public List<ChatMessage> Assemble(PromptRole role, string userQuestion,
            string? extraSystemSuffix = null, List<string>? semanticSnippets = null)
        {
            var messages = new List<ChatMessage>();

            // 1. 加载 Prompt 配置
            var config = _registry.LoadPromptConfig();
            var template = GetTemplate(config, role);

            // 2. System Prompt（+ 可选的规则追加）
            string systemPrompt = template?.SystemPrompt ?? GetDefaultSystemPrompt(role);
            if (!string.IsNullOrEmpty(extraSystemSuffix))
                systemPrompt += "\n" + extraSystemSuffix;
            messages.Add(new ChatMessage("system", systemPrompt));

            // 3. 上下文消息（实时蓝图状态 + 三层检索知识）
            string contextContent = BuildContextContent(role, template, userQuestion, semanticSnippets);
            if (!string.IsNullOrEmpty(contextContent))
                messages.Add(new ChatMessage("system", contextContent));

            // 4. Few-shot 示例
            if (template?.FewShotExamples != null)
            {
                foreach (var example in template.FewShotExamples)
                {
                    if (!string.IsNullOrEmpty(example.User))
                        messages.Add(new ChatMessage("user", example.User));
                    if (!string.IsNullOrEmpty(example.Assistant))
                        messages.Add(new ChatMessage("assistant", example.Assistant));
                }
            }

            // 5. 用户问题
            messages.Add(new ChatMessage("user", userQuestion));

            return messages;
        }

        // ══════════════════════════════════════
        //  上下文构建
        // ══════════════════════════════════════

        private string BuildContextContent(PromptRole role, PromptTemplate? template,
            string userQuestion, List<string>? semanticSnippets)
        {
            var sb = new StringBuilder();

            // 实时蓝图状态
            if (_contextProvider != null && _contextProvider.HasActiveSession)
            {
                var ctx = _contextProvider.GetCurrentContext();
                string contextBlock = FillContextTemplate(template?.ContextTemplate, ctx, role);
                if (!string.IsNullOrEmpty(contextBlock))
                    sb.AppendLine(contextBlock);
            }

            // ────────── 三层检索策略 ──────────

            // 第一层（必注入）：S0_CoreConcepts 每次都带
            var s0Docs = _registry.GetByLayer(Contract.Knowledge.KnowledgeLayer.S0_CoreConcepts);
            if (s0Docs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【核心概念】");
                foreach (var doc in s0Docs)
                {
                    string? content = doc.ReadContent();
                    if (string.IsNullOrEmpty(content)) continue;
                    // S0 文档通常较短，截断保护
                    if (content!.Length > 1500)
                        content = content.Substring(0, 1500) + "\n...(已截断)";
                    sb.AppendLine(content);
                }
            }

            // 第一层（必注入）：S1_Definitions 类型定义总览（双方共用）
            var s1Docs = _registry.GetByLayer(Contract.Knowledge.KnowledgeLayer.S1_Definitions);
            if (s1Docs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【类型定义总览】");
                foreach (var doc in s1Docs)
                {
                    string? content = doc.ReadContent();
                    if (string.IsNullOrEmpty(content)) continue;
                    if (content!.Length > 3000)
                        content = content.Substring(0, 3000) + "\n...(已截断)";
                    sb.AppendLine(content);
                }
            }

            // 第二层（自动检索）：Embedding 语义匹配片段（由调用方注入）
            if (semanticSnippets != null && semanticSnippets.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("【语义检索结果】");
                foreach (var snippet in semanticSnippets)
                {
                    sb.AppendLine(snippet);
                    sb.AppendLine();
                }
            }

            // 第三层（按需查询）：通过 Function Calling 暴露 search_knowledge() 工具，
            // AI 在需要更多细节时自主调用（已在 KnowledgeTools 中实现）

            // 如果选中了节点，精确注入该节点的 P1 文档
            if (_contextProvider != null && _contextProvider.HasActiveSession)
            {
                var ctx = _contextProvider.GetCurrentContext();
                if (!string.IsNullOrEmpty(ctx.SelectedNodeTypeId))
                {
                    var actionDoc = _registry.FindActionDoc(ctx.SelectedNodeTypeId);
                    if (actionDoc != null)
                    {
                        string? actionContent = actionDoc.ReadContent();
                        if (!string.IsNullOrEmpty(actionContent))
                        {
                            sb.AppendLine();
                            sb.AppendLine($"【选中节点文档: {actionDoc.Entry.Title}】");
                            sb.AppendLine(actionContent);
                        }
                    }
                }
            }

            return sb.ToString();
        }

        private string FillContextTemplate(string? templateStr, BlueprintContext ctx, PromptRole role)
        {
            if (string.IsNullOrEmpty(templateStr))
                return BuildDefaultContext(ctx, role);

            return templateStr!
                .Replace("{blueprintName}", ctx.BlueprintName)
                .Replace("{nodeCount}", ctx.NodeCount.ToString())
                .Replace("{nodeList}", ctx.NodeListSummary)
                .Replace("{selectedNode}", string.IsNullOrEmpty(ctx.SelectedNodeDisplayName)
                    ? "(未选中)"
                    : $"{ctx.SelectedNodeDisplayName} ({ctx.SelectedNodeTypeId})")
                .Replace("{selectedNodeProperties}", string.IsNullOrEmpty(ctx.SelectedNodeProperties)
                    ? "(无)" : ctx.SelectedNodeProperties)
                .Replace("{validationIssues}", string.IsNullOrEmpty(ctx.ValidationIssues)
                    ? "(无问题)" : ctx.ValidationIssues)
                .Replace("{activeFile}", ctx.ActiveFile)
                .Replace("{activeSymbol}", ctx.ActiveSymbol)
                .Replace("{relevantDocs}", ""); // relevantDocs 由上层 Search 填充
        }

        private static string BuildDefaultContext(BlueprintContext ctx, PromptRole role)
        {
            var sb = new StringBuilder();

            // 去角色化：统一显示完整的蓝图状态 + 代码上下文
            sb.AppendLine("【当前蓝图状态】");
            sb.AppendLine($"蓝图名称：{ctx.BlueprintName}");
            sb.AppendLine($"节点数量：{ctx.NodeCount}");
            sb.AppendLine($"节点列表：{ctx.NodeListSummary}");
            sb.AppendLine($"当前选中：{(string.IsNullOrEmpty(ctx.SelectedNodeDisplayName) ? "(未选中)" : ctx.SelectedNodeDisplayName)}");
            if (!string.IsNullOrEmpty(ctx.SelectedNodeProperties))
                sb.AppendLine($"选中节点配置：{ctx.SelectedNodeProperties}");
            if (!string.IsNullOrEmpty(ctx.ValidationIssues))
                sb.AppendLine($"校验问题：{ctx.ValidationIssues}");

            // 程序上下文（如果有）
            if (!string.IsNullOrEmpty(ctx.ActiveFile))
                sb.AppendLine($"当前文件：{ctx.ActiveFile}");
            if (!string.IsNullOrEmpty(ctx.ActiveSymbol))
                sb.AppendLine($"光标所在：{ctx.ActiveSymbol}");

            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  辅助方法
        // ══════════════════════════════════════

        private static PromptTemplate? GetTemplate(PromptConfigData? config, PromptRole role)
        {
            if (config == null) return null;
            return role switch
            {
                PromptRole.Developer => config.Developer,
                PromptRole.Designer => config.Designer,
                _ => null,
            };
        }

        private static string GetDefaultSystemPrompt(PromptRole role)
        {
            // 去角色化：统一的 SystemPrompt，兼具策划配置和程序架构能力
            // 保留 role 参数以兼容旧配置，但默认返回统一 Prompt
            return
                "你是 SceneBlueprint 战斗蓝图系统的 AI 助手。\n" +
                "你的能力范围包括：\n" +
                "- 帮助策划配置战斗逻辑（节点配置、Marker 绑定、蓝图校验）\n" +
                "- 解答框架架构和代码实现问题（ActionDefinition、DSL、节点图框架）\n" +
                "- 查询蓝图数据、知识文档、源代码\n\n" +
                "你可以使用工具（Function Calling）主动查询蓝图数据和知识文档。\n" +
                "当用户的问题涉及当前蓝图状态时，优先调用工具获取实际数据后再回答。\n\n" +
                "【回答规范】\n" +
                "1. 回答必须基于实际数据和知识文档，禁止编造不存在的节点、API 或功能。\n" +
                "2. 如果知识库中没有相关信息，请明确说明\"当前知识库中没有找到相关信息\"。\n" +
                "3. 涉及操作步骤时用具体描述（如\"右键点击空白区域 → 选择添加节点\"）。\n" +
                "4. 涉及代码时可引用类名、方法名，但必须与实际源码一致。\n" +
                "5. 发现校验问题时主动提醒用户并给出修复建议。\n" +
                "6. 回答简洁有条理，不重复用户问题。\n\n" +
                "【兜底规则】\n" +
                "- 如果知识库和工具都无法找到答案，请回复：「当前知识库未覆盖此问题，请联系开发人员 zhangguoxin@17paipai.cn 获取帮助。」\n" +
                "- 禁止返回空白回复，务必给出有意义的反馈。\n";
        }
    }

    /// <summary>
    /// LLM 工具调用数据（Function Calling）。
    /// 放在 Runtime 层以供 ChatMessage 引用，避免 Runtime → Editor 的反向依赖。
    /// </summary>
    public class ToolCallData
    {
        public string Id = "";
        public string FunctionName = "";
        public string Arguments = "";
    }

    /// <summary>
    /// LLM 聊天消息。
    /// 支持 Function Calling：assistant 消息可携带 ToolCalls，tool 消息需要 ToolCallId。
    /// </summary>
    public class ChatMessage
    {
        public string Role;
        public string Content;

        /// <summary>assistant 消息中 LLM 请求的工具调用列表。</summary>
        public List<ToolCallData>? ToolCalls;

        /// <summary>tool 角色消息关联的 tool_call id。</summary>
        public string? ToolCallId;

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
