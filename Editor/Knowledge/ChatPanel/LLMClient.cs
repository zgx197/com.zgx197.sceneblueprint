#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SceneBlueprint.Runtime.Knowledge;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// LLM API 客户端（Moonshot/OpenAI 兼容）。
    /// 支持流式 SSE 输出、Function Calling（tool_calls）、递归工具调用。
    /// 使用 EditorApplication.update 驱动异步轮询。
    /// </summary>
    public class LLMClient
    {
        private const int MaxToolRecursion = 3;

        private string _apiUrl;
        private string _model;
        private UnityWebRequest? _activeRequest;
        private bool _cancelled;

        public bool IsBusy => _activeRequest != null && !_activeRequest.isDone;

        /// <summary>工具执行器（设置后启用 Function Calling）。</summary>
        public ToolExecutor? Tools { get; set; }

        public LLMClient()
        {
            ReloadConfig();
        }

        /// <summary>
        /// 从 AiModelManager 重新加载活跃模型配置。
        /// 切换模型后调用此方法。
        /// </summary>
        public void ReloadConfig()
        {
            var config = AiModelManager.GetActiveConfig();
            _apiUrl = config.ApiUrl;
            _model = config.Model;
        }

        // ══════════════════════════════════════
        //  API Key 管理（委托给 AiModelManager，存 UserConfig，本地私有不进版本控制）
        // ══════════════════════════════════════

        public static string GetApiKey()
        {
            return AiModelManager.GetActiveApiKey();
        }

        public static void SetApiKey(string key)
        {
            AiModelManager.SetApiKey(AiModelManager.GetActiveId(), key);
        }

        public static bool HasApiKey()
        {
            return AiModelManager.HasActiveApiKey();
        }

        // ══════════════════════════════════════
        //  流式请求回调
        // ══════════════════════════════════════

        /// <summary>
        /// 流式聊天回调集合。
        /// </summary>
        public class StreamCallbacks
        {
            /// <summary>流式内容增量更新（累积文本, 是否结束）</summary>
            public Action<string, bool>? OnContentUpdate;
            /// <summary>AI 正在调用工具（工具名, 参数 JSON）</summary>
            public Action<string, string>? OnToolCall;
            /// <summary>发生错误</summary>
            public Action<string>? OnError;
        }

        // ══════════════════════════════════════
        //  轻量非流式请求（意图分类等）
        // ══════════════════════════════════════

        /// <summary>
        /// 异步发送非流式聊天请求，返回完整响应文本。
        /// 适用于意图分类等不需要流式输出的轻量调用。
        /// 使用独立的 UnityWebRequest，不阻塞主流式请求。
        /// </summary>
        public void SendSimpleAsync(List<ChatMessage> messages, Action<string?> onResult)
        {
            string apiKey = GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                onResult(null);
                return;
            }

            string requestJson = BuildRequestJson(messages, stream: false, includeTools: false);

            var request = new UnityWebRequest(_apiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.timeout = 15; // 意图分类应快速完成

            var operation = request.SendWebRequest();

            void PollResult()
            {
                if (!operation.isDone) return;
                UnityEditor.EditorApplication.update -= PollResult;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.LogWarning($"[LLMClient] SimpleAsync 失败: {request.error}");
                    request.Dispose();
                    onResult(null);
                    return;
                }

                string responseText = request.downloadHandler?.text ?? "";
                request.Dispose();

                // 提取 choices[0].message.content
                string? content = ExtractJsonString(responseText, "content");
                onResult(content);
            }

            UnityEditor.EditorApplication.update += PollResult;
        }

        // ══════════════════════════════════════
        //  发送请求（流式 + Function Calling）
        // ══════════════════════════════════════

        /// <summary>
        /// 异步发送流式聊天请求，支持 Function Calling 递归。
        /// </summary>
        public void SendStreamAsync(List<ChatMessage> messages, StreamCallbacks callbacks)
        {
            if (IsBusy)
            {
                callbacks.OnError?.Invoke("请求正在进行中，请等待完成");
                return;
            }

            string apiKey = GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                callbacks.OnError?.Invoke("API Key 未设置。请在 SceneBlueprint → Knowledge → AI Settings 中配置。");
                return;
            }

            _cancelled = false;
            SendStreamInternal(messages, callbacks, 0);
        }

        private void SendStreamInternal(List<ChatMessage> messages, StreamCallbacks callbacks, int recursionDepth)
        {
            if (_cancelled) return;

            string apiKey = GetApiKey();
            string requestJson = BuildRequestJson(messages, stream: true);

            var request = new UnityWebRequest(_apiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.timeout = 120;

            _activeRequest = request;
            var operation = request.SendWebRequest();

            // 流式轮询状态
            int lastParsedLength = 0;
            string fullContent = "";
            var pendingToolCalls = new List<ToolCallData>();
            string sseBuffer = "";

            void PollStream()
            {
                if (_cancelled)
                {
                    UnityEditor.EditorApplication.update -= PollStream;
                    request.Abort();
                    request.Dispose();
                    _activeRequest = null;
                    return;
                }

                // 增量解析已下载的数据
                string? downloadedText = request.downloadHandler?.text;
                if (downloadedText != null && downloadedText.Length > lastParsedLength)
                {
                    string newChunk = downloadedText.Substring(lastParsedLength);
                    lastParsedLength = downloadedText.Length;

                    sseBuffer += newChunk;
                    var lines = sseBuffer.Split('\n');
                    sseBuffer = lines[lines.Length - 1]; // 保留未完成的行

                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        string line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line) || line == "data: [DONE]") continue;
                        if (!line.StartsWith("data: ")) continue;

                        string jsonStr = line.Substring(6);
                        ParseSSEDelta(jsonStr, ref fullContent, pendingToolCalls, callbacks);
                    }
                }

                if (!operation.isDone) return;

                // 请求完成
                UnityEditor.EditorApplication.update -= PollStream;
                _activeRequest = null;

                // 处理最后的 buffer
                if (!string.IsNullOrEmpty(sseBuffer))
                {
                    string lastLine = sseBuffer.Trim();
                    if (lastLine.StartsWith("data: ") && lastLine != "data: [DONE]")
                    {
                        ParseSSEDelta(lastLine.Substring(6), ref fullContent, pendingToolCalls, callbacks);
                    }
                    sseBuffer = "";
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"API 请求失败: {request.error}";
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    {
                        string respText = request.downloadHandler.text;
                        if (respText.Length > 300) respText = respText.Substring(0, 300);
                        errorMsg += $"\n响应: {respText}";
                    }
                    callbacks.OnError?.Invoke(errorMsg);
                    request.Dispose();
                    return;
                }

                request.Dispose();

                // 如果有 tool_calls，执行并递归
                if (pendingToolCalls.Count > 0 && recursionDepth < MaxToolRecursion && Tools != null)
                {
                    HandleToolCalls(messages, fullContent, pendingToolCalls, callbacks, recursionDepth);
                }
                else
                {
                    // 正常完成
                    callbacks.OnContentUpdate?.Invoke(fullContent, true);
                }
            }

            UnityEditor.EditorApplication.update += PollStream;
        }

        /// <summary>
        /// 执行 tool_calls，将结果追加到消息列表后递归调用。
        /// </summary>
        private void HandleToolCalls(List<ChatMessage> originalMessages, string assistantContent,
            List<ToolCallData> toolCalls, StreamCallbacks callbacks, int recursionDepth)
        {
            if (Tools == null) return;

            // 构建带 tool_calls 的 assistant 消息
            var newMessages = new List<ChatMessage>(originalMessages);
            var assistantMsg = new ChatMessage("assistant", assistantContent ?? "");
            assistantMsg.ToolCalls = new List<ToolCallData>(toolCalls);
            newMessages.Add(assistantMsg);

            // 执行每个 tool call
            foreach (var tc in toolCalls)
            {
                callbacks.OnToolCall?.Invoke(tc.FunctionName, tc.Arguments);

                string result = Tools.Execute(tc.FunctionName, tc.Arguments);

                var toolMsg = new ChatMessage("tool", result);
                toolMsg.ToolCallId = tc.Id;
                newMessages.Add(toolMsg);
            }

            // 递归发送（带 tool 结果继续对话）
            SendStreamInternal(newMessages, callbacks, recursionDepth + 1);
        }

        /// <summary>
        /// 取消当前进行中的请求。
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
            if (_activeRequest != null && !_activeRequest.isDone)
            {
                _activeRequest.Abort();
                _activeRequest.Dispose();
                _activeRequest = null;
            }
        }

        // ══════════════════════════════════════
        //  SSE 增量解析
        // ══════════════════════════════════════

        private void ParseSSEDelta(string jsonStr, ref string fullContent,
            List<ToolCallData> toolCalls, StreamCallbacks callbacks)
        {
            // 解析 delta.content
            string? deltaContent = ExtractJsonString(jsonStr, "content");
            if (deltaContent != null)
            {
                fullContent += deltaContent;
                callbacks.OnContentUpdate?.Invoke(fullContent, false);
            }

            // 解析 delta.tool_calls
            ParseToolCallsDelta(jsonStr, toolCalls);
        }

        /// <summary>
        /// 增量解析流式 tool_calls。
        /// 流式中 tool_calls 以增量方式到达：先是 id + function.name，后续是 function.arguments 分段。
        /// </summary>
        private void ParseToolCallsDelta(string jsonStr, List<ToolCallData> toolCalls)
        {
            // 检查 json 中是否包含 tool_calls
            if (!jsonStr.Contains("tool_calls")) return;

            // 提取 tool_calls 数组区域
            int tcStart = jsonStr.IndexOf("\"tool_calls\"", StringComparison.Ordinal);
            if (tcStart < 0) return;

            // 找到 [ 开始
            int arrStart = jsonStr.IndexOf('[', tcStart);
            if (arrStart < 0) return;

            // 找到匹配的 ]
            int depth = 0;
            int arrEnd = -1;
            for (int i = arrStart; i < jsonStr.Length; i++)
            {
                if (jsonStr[i] == '[') depth++;
                else if (jsonStr[i] == ']') { depth--; if (depth == 0) { arrEnd = i; break; } }
            }
            if (arrEnd < 0) return;

            string arrJson = jsonStr.Substring(arrStart, arrEnd - arrStart + 1);

            // 解析每个 tool_call 元素（简易解析）
            // 查找 index
            int? index = ExtractJsonInt(arrJson, "index");
            int idx = index ?? 0;

            // 确保列表足够长
            while (toolCalls.Count <= idx)
                toolCalls.Add(new ToolCallData());

            var tc = toolCalls[idx];

            // id
            string? id = ExtractJsonString(arrJson, "id");
            if (!string.IsNullOrEmpty(id)) tc.Id = id!;

            // function.name
            string? name = ExtractJsonString(arrJson, "name");
            if (!string.IsNullOrEmpty(name)) tc.FunctionName += name;

            // function.arguments (增量拼接)
            string? args = ExtractJsonString(arrJson, "arguments");
            if (args != null) tc.Arguments += args;
        }

        // ══════════════════════════════════════
        //  JSON 构建
        // ══════════════════════════════════════

        private string BuildRequestJson(List<ChatMessage> messages, bool stream = false, bool includeTools = true)
        {
            var sb = new StringBuilder();
            sb.Append("{\"model\":\"").Append(_model).Append("\",\"messages\":[");

            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var msg = messages[i];
                sb.Append("{\"role\":\"").Append(EscapeJson(msg.Role)).Append("\"");

                // content（可以为 null，对于 assistant + tool_calls 的情况）
                if (msg.Content != null)
                    sb.Append(",\"content\":\"").Append(EscapeJson(msg.Content)).Append("\"");
                else
                    sb.Append(",\"content\":null");

                // tool_call_id（tool 角色消息必须有）
                if (msg.Role == "tool" && !string.IsNullOrEmpty(msg.ToolCallId))
                    sb.Append(",\"tool_call_id\":\"").Append(EscapeJson(msg.ToolCallId)).Append("\"");

                // tool_calls（assistant 消息可能包含）
                if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    sb.Append(",\"tool_calls\":[");
                    for (int j = 0; j < msg.ToolCalls.Count; j++)
                    {
                        if (j > 0) sb.Append(",");
                        var tc = msg.ToolCalls[j];
                        sb.Append("{\"id\":\"").Append(EscapeJson(tc.Id))
                          .Append("\",\"type\":\"function\",\"function\":{\"name\":\"")
                          .Append(EscapeJson(tc.FunctionName))
                          .Append("\",\"arguments\":\"")
                          .Append(EscapeJson(tc.Arguments))
                          .Append("\"}}");
                    }
                    sb.Append("]");
                }

                sb.Append("}");
            }

            sb.Append("],\"temperature\":0.3,\"top_p\":0.85");

            if (stream)
                sb.Append(",\"stream\":true");

            // 注入工具定义
            if (includeTools && Tools != null)
            {
                var defs = Tools.GetAllDefinitions();
                if (defs.Count > 0)
                    sb.Append(",\"tools\":").Append(Tools.ToToolsJsonArray());
            }

            sb.Append("}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  简易 JSON 字段提取
        // ══════════════════════════════════════

        /// <summary>
        /// 从 JSON 字符串中提取指定 key 的 string 值（简易解析）。
        /// </summary>
        private static string? ExtractJsonString(string json, string key)
        {
            string pattern1 = $"\"{key}\":\"";
            string pattern2 = $"\"{key}\": \"";
            int idx = json.IndexOf(pattern1, StringComparison.Ordinal);
            if (idx >= 0)
            {
                idx += pattern1.Length;
            }
            else
            {
                idx = json.IndexOf(pattern2, StringComparison.Ordinal);
                if (idx >= 0) idx += pattern2.Length;
            }

            if (idx < 0) return null;

            var sb = new StringBuilder();
            for (int i = idx; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"':  sb.Append('"');  i++; continue;
                        case '\\': sb.Append('\\'); i++; continue;
                        case 'n':  sb.Append('\n'); i++; continue;
                        case 'r':  sb.Append('\r'); i++; continue;
                        case 't':  sb.Append('\t'); i++; continue;
                        case '/':  sb.Append('/');  i++; continue;
                        default:   sb.Append('\\'); continue;
                    }
                }
                if (c == '"') break;
                sb.Append(c);
            }

            return sb.Length > 0 ? sb.ToString() : "";
        }

        /// <summary>
        /// 从 JSON 字符串中提取指定 key 的 int 值。
        /// </summary>
        private static int? ExtractJsonInt(string json, string key)
        {
            string pattern1 = $"\"{key}\":";
            string pattern2 = $"\"{key}\": ";
            int idx = json.IndexOf(pattern1, StringComparison.Ordinal);
            if (idx >= 0)
            {
                idx += pattern1.Length;
            }
            else
            {
                idx = json.IndexOf(pattern2, StringComparison.Ordinal);
                if (idx >= 0) idx += pattern2.Length;
            }

            if (idx < 0) return null;

            // 跳过空格
            while (idx < json.Length && json[idx] == ' ') idx++;

            var numSb = new StringBuilder();
            while (idx < json.Length && char.IsDigit(json[idx]))
            {
                numSb.Append(json[idx]);
                idx++;
            }

            if (numSb.Length > 0 && int.TryParse(numSb.ToString(), out int val))
                return val;
            return null;
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
