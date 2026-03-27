#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Contract.Knowledge;
using SceneBlueprint.Runtime.Knowledge;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// AI 对话面板，作为独立左侧面板嵌入蓝图编辑器。
    /// 提供对话 UI、角色切换、上下文感知的 Prompt 组装和 LLM 调用。
    /// </summary>
    public class AIChatPanel
    {
        // ── 依赖 ──
        private readonly KnowledgeService _service;
        private readonly AIChatSession _session = new AIChatSession();
        private readonly LLMClient _llmClient;
        private readonly ToolExecutor _toolExecutor;
        private readonly EmbeddingService _embeddingService;

        // ── UI 状态 ──
        private string _inputText = "";
        private Vector2 _scrollPos;
        private PromptRole _currentRole = PromptRole.Designer;

        // ── 样式缓存 ──
        private bool _stylesInited;
        private GUIStyle _userBubbleStyle = null!;
        private GUIStyle _assistantBubbleStyle = null!;
        private GUIStyle _systemBubbleStyle = null!;
        private GUIStyle _messageLabelStyle = null!;
        private GUIStyle _roleLabelStyle = null!;
        private GUIStyle _inputAreaStyle = null!;
        private GUIStyle _actionButtonStyle = null!;
        private GUIStyle _welcomeStyle = null!;
        private GUIStyle _timestampStyle = null!;

        // ── 常量 ──
        private const float InputAreaMinHeight = 48f;
        private const float MessageFontSize    = 13f;
        private const string FallbackMessage   = "抱歉，我暂时无法回答这个问题。请联系开发人员 zhangguoxin@17paipai.cn 获取帮助。";

        // ── 意图分类 ──
        private enum QueryIntent { Knowledge, Code, Guide, Chat }

        private const string IntentClassifierPrompt =
            "你是一个意图分类器。请对用户的问题进行分类，只回复以下标签之一（不要回复其他内容）：\n" +
            "- knowledge：关于框架概念、参数含义、配置说明、Marker/Annotation/Action 的用法等知识问答\n" +
            "- code：关于具体代码实现、源文件位置、函数签名、类结构等代码查询\n" +
            "- guide：关于操作步骤、工作流程、如何完成某个任务的指导\n" +
            "- chat：闲聊、问候、感谢、与框架无关的泛化对话";

        // ── 流式输出状态 ──
        private AIChatMessage? _streamingMessage;
        private GUIStyle _toolCallStyle = null!;


        public AIChatPanel(KnowledgeService service)
        {
            _service = service;
            _embeddingService = service.Embedding;

            // 初始化工具执行器
            _toolExecutor = new ToolExecutor();
            BlueprintTools.RegisterAll(_toolExecutor);
            KnowledgeTools.RegisterAll(_toolExecutor);

            // 初始化 LLM 客户端并注入工具
            _llmClient = new LLMClient();
            _llmClient.Tools = _toolExecutor;
        }

        // ══════════════════════════════════════
        //  主绘制入口
        // ══════════════════════════════════════

        public void Draw()
        {
            InitStyles();

            // ── 标题栏 ──
            DrawTitleBar();

            // ── 消息列表 ──
            DrawMessageList();

            // ── 输入区域 ──
            DrawInputArea();
        }

        // ══════════════════════════════════════
        //  标题栏
        // ══════════════════════════════════════

        private void DrawTitleBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label("AI 助手", EditorStyles.boldLabel, GUILayout.Width(50));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(36)))
                {
                    _session.Clear();
                    _llmClient.Cancel();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════
        //  消息列表
        // ══════════════════════════════════════

        private void DrawMessageList()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
            {
                GUILayout.Space(4);

                if (_session.Messages.Count == 0)
                {
                    DrawWelcomeMessage();
                }
                else
                {
                    for (int i = 0; i < _session.Messages.Count; i++)
                    {
                        DrawMessage(_session.Messages[i], i);
                    }
                }

                // 等待响应指示器（流式输出期间由 _streamingMessage 显示内容）
                if (_session.IsWaitingForResponse && _streamingMessage == null)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.BeginVertical(_assistantBubbleStyle);
                    {
                        var prevColor = GUI.color;
                        GUI.color = new Color(0.7f, 0.85f, 1f);
                        int dotCount = (int)(EditorApplication.timeSinceStartup * 2) % 4;
                        string dots = new string('.', dotCount);
                        EditorGUILayout.LabelField($"AI 思考中{dots}", _messageLabelStyle);
                        GUI.color = prevColor;
                    }
                    EditorGUILayout.EndVertical();
                    HandleUtility.Repaint();
                }

                // 错误提示
                if (_session.PendingError != null)
                {
                    EditorGUILayout.Space(4);
                    var prevColor = GUI.color;
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(_session.PendingError, _messageLabelStyle);
                    EditorGUILayout.EndVertical();
                    GUI.color = prevColor;
                }

                GUILayout.Space(8);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawWelcomeMessage()
        {
            EditorGUILayout.Space(20);

            EditorGUILayout.BeginVertical(_assistantBubbleStyle);
            {
                EditorGUILayout.LabelField("你好! 我是蓝图 AI 助手。", _welcomeStyle);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(
                    "可以问我关于蓝图配置、节点使用、Marker 操作、框架架构、代码实现等问题。\n" +
                    "我会主动查询蓝图数据和知识文档来回答你。",
                    _messageLabelStyle);
                EditorGUILayout.Space(8);

                // 快捷问题按钮（统一角色，展示策划+程序双方向的问题）
                var prevColor = GUI.color;
                GUI.color = new Color(0.6f, 0.75f, 0.95f);
                if (GUILayout.Button("场景蓝图中有哪些节点类型?", EditorStyles.miniButton))
                    EditorApplication.delayCall += () => QuickAsk("场景蓝图中有哪些节点类型?");
                if (GUILayout.Button("如何配置一个波次刷怪?", EditorStyles.miniButton))
                    EditorApplication.delayCall += () => QuickAsk("如何配置一个波次刷怪?");
                if (GUILayout.Button("SceneBlueprint 的整体架构是什么?", EditorStyles.miniButton))
                    EditorApplication.delayCall += () => QuickAsk("SceneBlueprint 的整体架构是什么?");
                if (GUILayout.Button("如何自定义一个新的 Action 节点?", EditorStyles.miniButton))
                    EditorApplication.delayCall += () => QuickAsk("如何自定义一个新的 Action 节点?");
                GUI.color = prevColor;
            }
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════
        //  消息气泡
        // ══════════════════════════════════════

        private void DrawMessage(AIChatMessage msg, int index)
        {
            EditorGUILayout.Space(6);

            bool isUser = msg.Role == AIChatMessage.MessageRole.User;
            bool isSystem = msg.Role == AIChatMessage.MessageRole.System;
            GUIStyle bubbleStyle = isUser ? _userBubbleStyle
                                 : isSystem ? _systemBubbleStyle
                                 : _assistantBubbleStyle;

            EditorGUILayout.BeginVertical(bubbleStyle);
            {
                // 角色标签 + 时间戳 + 流式指示器
                EditorGUILayout.BeginHorizontal();
                {
                    string roleLabel = isUser ? "你" : isSystem ? "系统" : "AI";
                    var prevColor = GUI.color;
                    GUI.color = isUser ? new Color(0.6f, 0.8f, 1f)
                              : isSystem ? new Color(1f, 0.85f, 0.5f)
                              : new Color(0.6f, 0.95f, 0.7f);
                    GUILayout.Label(roleLabel, _roleLabelStyle, GUILayout.Width(24));
                    GUI.color = prevColor;

                    string time = msg.Timestamp.ToString("HH:mm");
                    GUILayout.Label(time, _timestampStyle);

                    // 流式打字指示器
                    if (msg.IsStreaming)
                    {
                        prevColor = GUI.color;
                        GUI.color = new Color(0.5f, 0.9f, 0.5f);
                        int dotCount = (int)(EditorApplication.timeSinceStartup * 3) % 4;
                        GUILayout.Label(new string('.', dotCount), _timestampStyle, GUILayout.Width(16));
                        GUI.color = prevColor;
                    }

                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();

                // 工具调用状态条
                if (msg.ActiveToolCalls != null && msg.ActiveToolCalls.Count > 0)
                {
                    DrawToolCallStatus(msg.ActiveToolCalls);
                }

                // 消息正文（Markdown 渲染 + 自动换行，高度自适应）
                bool isAssistantMsg = msg.Role == AIChatMessage.MessageRole.Assistant;
                string contentText = msg.Content ?? "";
                string displayText = isAssistantMsg && !string.IsNullOrEmpty(contentText)
                    ? MarkdownRenderer.Convert(contentText)
                    : contentText;
                if (!string.IsNullOrEmpty(displayText))
                    GUILayout.Label(displayText, _messageLabelStyle, GUILayout.ExpandWidth(true));

                // 方案 B：操作按钮栏（仅已完成的 AI 回复显示）
                if (msg.Role == AIChatMessage.MessageRole.Assistant && !msg.IsStreaming)
                {
                    DrawMessageActions(msg, index);
                }
            }
            EditorGUILayout.EndVertical();

            // 流式消息驱动重绘
            if (msg.IsStreaming)
                HandleUtility.Repaint();
        }

        // ══════════════════════════════════════
        //  工具调用状态 UI
        // ══════════════════════════════════════

        private void DrawToolCallStatus(List<ToolCallStatus> toolCalls)
        {
            foreach (var tc in toolCalls)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    var prevColor = GUI.color;
                    GUI.color = tc.IsComplete
                        ? new Color(0.5f, 0.85f, 0.5f)
                        : new Color(0.9f, 0.8f, 0.3f);

                    string statusIcon = tc.IsComplete ? "\u2713" : "\u25B6";
                    string statusText = tc.IsComplete
                        ? $"{statusIcon} {tc.ToolName}"
                        : $"{statusIcon} {tc.ToolName}...";
                    GUILayout.Label(statusText, _toolCallStyle, GUILayout.ExpandWidth(false));

                    GUI.color = prevColor;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ══════════════════════════════════════
        //  方案 B：消息操作按钮
        // ══════════════════════════════════════

        private void DrawMessageActions(AIChatMessage msg, int index)
        {
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("复制", _actionButtonStyle, GUILayout.Width(36)))
                {
                    GUIUtility.systemCopyBuffer = msg.Content;
                    UnityEngine.Debug.Log("[AI] 已复制到剪贴板");
                }

                if (GUILayout.Button("重答", _actionButtonStyle, GUILayout.Width(36)))
                {
                    RegenerateFromMessage(index);
                }

                if (GUILayout.Button("纠错", _actionButtonStyle, GUILayout.Width(36)))
                {
                    MarkAsIncorrect(index);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>删除该 AI 回复并重新发送上一条用户消息。</summary>
        private void RegenerateFromMessage(int assistantMsgIndex)
        {
            if (_session.IsWaitingForResponse) return;

            // 找到这条 AI 回复对应的用户消息
            string? lastUserQuestion = null;
            for (int i = assistantMsgIndex - 1; i >= 0; i--)
            {
                if (_session.Messages[i].Role == AIChatMessage.MessageRole.User)
                {
                    lastUserQuestion = _session.Messages[i].Content;
                    break;
                }
            }
            if (lastUserQuestion == null) return;

            // 移除该 AI 回复
            _session.Messages.RemoveAt(assistantMsgIndex);
            _session.PendingError = null;

            // 重新发送
            DoSendQuestion(lastUserQuestion);
        }

        /// <summary>标记 AI 回复为错误，追加纠错上下文后重新生成。</summary>
        private void MarkAsIncorrect(int assistantMsgIndex)
        {
            if (_session.IsWaitingForResponse) return;

            string incorrectAnswer = _session.Messages[assistantMsgIndex].Content;

            // 找到对应的用户问题
            string? lastUserQuestion = null;
            for (int i = assistantMsgIndex - 1; i >= 0; i--)
            {
                if (_session.Messages[i].Role == AIChatMessage.MessageRole.User)
                {
                    lastUserQuestion = _session.Messages[i].Content;
                    break;
                }
            }
            if (lastUserQuestion == null) return;

            // 追加一条系统消息作为纠错上下文
            _session.AddSystemMessage(
                $"用户反馈上述回答有误。请重新回答，注意准确性。\n错误回答摘要: {Truncate(incorrectAnswer, 200)}");

            // 重新发送用户问题
            DoSendQuestion(lastUserQuestion);
        }

        // ══════════════════════════════════════
        //  输入区域
        // ══════════════════════════════════════

        private void DrawInputArea()
        {
            // 分隔线
            var lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            EditorGUI.DrawRect(lineRect, new Color(0.3f, 0.3f, 0.3f));

            EditorGUILayout.BeginVertical();
            {
                // 多行输入框
                var evt = Event.current;
                bool enterSend = evt.type == EventType.KeyDown
                              && evt.keyCode == KeyCode.Return
                              && !evt.shift
                              && GUI.GetNameOfFocusedControl() == "AIChatInput";

                GUI.SetNextControlName("AIChatInput");
                _inputText = EditorGUILayout.TextArea(_inputText, _inputAreaStyle,
                    GUILayout.MinHeight(InputAreaMinHeight), GUILayout.ExpandWidth(true));

                // Enter 发送（Shift+Enter 换行）
                bool canSend = !string.IsNullOrWhiteSpace(_inputText) && !_session.IsWaitingForResponse;
                if (enterSend && canSend)
                {
                    // 移除 Enter 插入的换行
                    _inputText = _inputText.TrimEnd('\n', '\r');
                    SendMessage();
                    evt.Use();
                }

                // 发送按钮行
                EditorGUILayout.BeginHorizontal();
                {
                    var prevColor = GUI.color;
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    GUILayout.Label("Enter 发送 / Shift+Enter 换行", EditorStyles.miniLabel);
                    GUI.color = prevColor;

                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(!canSend))
                    {
                        if (GUILayout.Button("发送", GUILayout.Width(50), GUILayout.Height(22)))
                            SendMessage();
                    }
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════
        //  发送消息（方案 C：含历史对话上下文）
        // ══════════════════════════════════════

        private void SendMessage()
        {
            string question = _inputText.Trim();
            if (string.IsNullOrEmpty(question)) return;

            // 前置检查：LLM API Key 是必须的（意图分类 + 主请求都需要）
            if (!LLMClient.HasApiKey())
            {
                _session.PendingError = "请先在设置中配置 LLM API Key，才能使用 AI 助手。";
                return;
            }

            _inputText = "";
            _session.PendingError = null;
            _session.AddUserMessage(question);
            _session.IsWaitingForResponse = true;

            DoSendQuestion(question);
        }

        /// <summary>组装 Prompt 并调用 LLM（流式 + Function Calling）</summary>
        private void DoSendQuestion(string question)
        {
            // ── 并行发起意图分类 + Embedding 检索，两者都完成后合并结果 ──
            QueryIntent? resolvedIntent = null;
            List<EmbeddingService.DocChunk>? resolvedChunks = null;
            bool intentDone = false;
            bool embeddingDone = false;

            // 两个异步任务都完成后的汇合点
            void TryMerge()
            {
                if (!intentDone || !embeddingDone) return;

                var intent = resolvedIntent ?? QueryIntent.Knowledge;
                UnityEngine.Debug.Log($"[AIChatPanel] 意图分类结果: {intent}");

                switch (intent)
                {
                    case QueryIntent.Knowledge:
                    case QueryIntent.Guide:
                        // 使用 Embedding 检索结果
                        BuildAndSend(question, resolvedChunks ?? new List<EmbeddingService.DocChunk>());
                        break;

                    case QueryIntent.Code:
                    case QueryIntent.Chat:
                        // 丢弃 Embedding 结果
                        BuildAndSend(question, new List<EmbeddingService.DocChunk>());
                        break;
                }
            }

            // 并行任务 1：意图分类
            ClassifyIntent(question, intent =>
            {
                resolvedIntent = intent;
                intentDone = true;
                TryMerge();
            });

            // 并行任务 2：Embedding 语义检索（始终以 Top-3 发起，按意图决定是否采用）
            _embeddingService.SearchAsync(question, topK: 3, chunks =>
            {
                resolvedChunks = chunks;
                embeddingDone = true;
                TryMerge();
            });
        }

        /// <summary>异步调用 LLM 做意图分类</summary>
        private void ClassifyIntent(string question, Action<QueryIntent> onResult)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage("system", IntentClassifierPrompt),
                new ChatMessage("user", question)
            };

            _llmClient.SendSimpleAsync(messages, response =>
            {
                var intent = ParseIntent(response);
                onResult(intent);
            });
        }

        /// <summary>解析 LLM 返回的意图标签</summary>
        private static QueryIntent ParseIntent(string? response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return QueryIntent.Knowledge; // 默认走知识检索（最安全）

            string lower = response!.Trim().ToLowerInvariant();
            if (lower.Contains("code")) return QueryIntent.Code;
            if (lower.Contains("guide")) return QueryIntent.Guide;
            if (lower.Contains("chat")) return QueryIntent.Chat;
            return QueryIntent.Knowledge; // 默认
        }

        /// <summary>根据检索结果组装 Prompt 并发送 LLM 请求</summary>
        private void BuildAndSend(string question, List<EmbeddingService.DocChunk> chunks)
        {
            List<string>? semanticSnippets = null;
            if (chunks.Count > 0)
            {
                semanticSnippets = new List<string>();
                foreach (var chunk in chunks)
                    semanticSnippets.Add($"[{chunk.DocTitle} · {chunk.Section}]\n{chunk.Text}");
            }

            // 组装 Prompt（系统提示 + 知识库上下文 + Few-shot + PromptRule 规则 + 语义检索）
            var assembler = new PromptAssembler(_service.Registry, _service.ContextProvider);
            string rulesSuffix = PromptRuleManager.AssembleEnabledPrompts();
            var promptMessages = assembler.Assemble(_currentRole, question, rulesSuffix, semanticSnippets);

            // 注入历史对话作为上下文（最近 N 轮）
            var chatMessages = new List<ChatMessage>();

            // 先添加 PromptAssembler 生成的 system 消息
            foreach (var pm in promptMessages)
            {
                if (pm.Role == "system")
                    chatMessages.Add(new ChatMessage(pm.Role, pm.Content));
            }

            // 注入历史对话（最近 10 条，排除当前问题本身）
            const int maxHistory = 10;
            var historyMessages = _session.Messages;
            int startIdx = Mathf.Max(0, historyMessages.Count - maxHistory - 1);
            for (int i = startIdx; i < historyMessages.Count; i++)
            {
                var hm = historyMessages[i];
                // 跳过最后一条（刚添加的当前用户问题，PromptAssembler 已包含）
                if (i == historyMessages.Count - 1 && hm.Role == AIChatMessage.MessageRole.User)
                    continue;

                string role = hm.Role switch
                {
                    AIChatMessage.MessageRole.User => "user",
                    AIChatMessage.MessageRole.Assistant => "assistant",
                    _ => "system",
                };
                chatMessages.Add(new ChatMessage(role, hm.Content));
            }

            // 添加 PromptAssembler 中的 few-shot + 用户问题
            foreach (var pm in promptMessages)
            {
                if (pm.Role != "system")
                    chatMessages.Add(new ChatMessage(pm.Role, pm.Content));
            }

            // 发送 LLM 请求
            SendLLMRequest(chatMessages);
        }

        /// <summary>创建流式消息占位并发送 LLM 请求</summary>
        private void SendLLMRequest(List<ChatMessage> chatMessages)
        {
            // 创建流式消息占位
            _streamingMessage = new AIChatMessage(AIChatMessage.MessageRole.Assistant, "") { IsStreaming = true };
            _session.Messages.Add(_streamingMessage);

            _llmClient.SendStreamAsync(chatMessages, new LLMClient.StreamCallbacks
            {
                OnContentUpdate = (content, done) =>
                {
                    if (_streamingMessage != null)
                    {
                        _streamingMessage.Content = content;
                        if (done)
                        {
                            // 兜底：如果 LLM 返回空白内容，填充引导消息
                            if (string.IsNullOrWhiteSpace(_streamingMessage.Content))
                            {
                                _streamingMessage.Content = FallbackMessage;
                            }
                            _streamingMessage.IsStreaming = false;
                            _streamingMessage.ActiveToolCalls = null;
                            _streamingMessage = null;
                            _session.IsWaitingForResponse = false;
                        }
                        _scrollPos = new Vector2(0, float.MaxValue);
                    }
                },
                OnToolCall = (toolName, args) =>
                {
                    if (_streamingMessage != null)
                    {
                        _streamingMessage.ActiveToolCalls ??= new List<ToolCallStatus>();
                        // 标记之前的工具为已完成
                        foreach (var tc in _streamingMessage.ActiveToolCalls)
                            tc.IsComplete = true;
                        _streamingMessage.ActiveToolCalls.Add(new ToolCallStatus
                        {
                            ToolName = toolName,
                            Arguments = args,
                            IsComplete = false
                        });
                    }
                },
                OnError = error =>
                {
                    _session.IsWaitingForResponse = false;
                    if (_streamingMessage != null)
                    {
                        _streamingMessage.IsStreaming = false;
                        // 如果流式消息没有内容，填充兜底消息而不是移除
                        if (string.IsNullOrEmpty(_streamingMessage.Content))
                        {
                            _streamingMessage.Content = FallbackMessage;
                        }
                        _streamingMessage = null;
                    }
                    _session.PendingError = $"请求失败：{error}\n如需帮助，请联系开发人员 zhangguoxin@17paipai.cn";
                    _scrollPos = new Vector2(0, float.MaxValue);
                },
            });

            _scrollPos = new Vector2(0, float.MaxValue);
        }

        /// <summary>快捷提问（欢迎页按钮）</summary>
        private void QuickAsk(string question)
        {
            // 前置检查：LLM API Key 是必须的
            if (!LLMClient.HasApiKey())
            {
                _session.PendingError = "请先在设置中配置 LLM API Key，才能使用 AI 助手。";
                return;
            }

            _inputText = "";
            _session.PendingError = null;
            _session.AddUserMessage(question);
            _session.IsWaitingForResponse = true;
            DoSendQuestion(question);
        }

        // ══════════════════════════════════════
        //  样式初始化
        // ══════════════════════════════════════

        private void InitStyles()
        {
            if (_stylesInited) return;
            _stylesInited = true;

            _userBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { background = MakeTex(1, 1, new Color(0.20f, 0.33f, 0.52f, 0.35f)) },
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(24, 6, 3, 3),
                border = new RectOffset(4, 4, 4, 4),
            };

            _assistantBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { background = MakeTex(1, 1, new Color(0.22f, 0.22f, 0.24f, 0.5f)) },
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(6, 24, 3, 3),
                border = new RectOffset(4, 4, 4, 4),
            };

            _systemBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { background = MakeTex(1, 1, new Color(0.38f, 0.33f, 0.18f, 0.35f)) },
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(6, 6, 3, 3),
                border = new RectOffset(4, 4, 4, 4),
            };

            _messageLabelStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true,
                fontSize = (int)MessageFontSize,
                padding = new RectOffset(2, 2, 2, 2),
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            };

            _roleLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                padding = new RectOffset(0, 0, 0, 0),
            };

            _timestampStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                padding = new RectOffset(4, 0, 3, 0),
            };

            _inputAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                fontSize = (int)MessageFontSize,
                padding = new RectOffset(8, 8, 6, 6),
            };

            _actionButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                padding = new RectOffset(4, 4, 1, 1),
                margin = new RectOffset(2, 2, 0, 2),
            };

            _welcomeStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.92f, 1f) },
            };

            _toolCallStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                richText = true,
                padding = new RectOffset(4, 4, 1, 1),
                margin = new RectOffset(2, 2, 1, 1),
            };
        }

        // ══════════════════════════════════════
        //  工具方法
        // ══════════════════════════════════════

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.SetPixels(pix);
            result.Apply();
            result.hideFlags = HideFlags.HideAndDontSave;
            return result;
        }

        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "...";
        }
    }
}
