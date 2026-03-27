#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// AI 聊天消息数据模型。
    /// </summary>
    [Serializable]
    public class AIChatMessage
    {
        public enum MessageRole { User, Assistant, System }

        public MessageRole Role;
        public string Content = "";
        public DateTime Timestamp = DateTime.Now;

        /// <summary>流式输出中：标记内容是否仍在接收中。</summary>
        public bool IsStreaming;

        /// <summary>当前正在执行的工具调用列表（流式期间用于 UI 显示状态）。</summary>
        public List<ToolCallStatus>? ActiveToolCalls;

        public AIChatMessage() { }

        public AIChatMessage(MessageRole role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 工具调用状态（UI 显示用）。
    /// </summary>
    public class ToolCallStatus
    {
        public string ToolName = "";
        public string Arguments = "";
        public bool IsComplete;
    }

    /// <summary>
    /// 聊天会话（消息历史列表 + 角色设定）。
    /// </summary>
    public class AIChatSession
    {
        public List<AIChatMessage> Messages { get; } = new List<AIChatMessage>();
        public bool IsWaitingForResponse { get; set; }
        public string? PendingError { get; set; }

        public void AddUserMessage(string content)
        {
            Messages.Add(new AIChatMessage(AIChatMessage.MessageRole.User, content));
        }

        public void AddAssistantMessage(string content)
        {
            Messages.Add(new AIChatMessage(AIChatMessage.MessageRole.Assistant, content));
        }

        public void AddSystemMessage(string content)
        {
            Messages.Add(new AIChatMessage(AIChatMessage.MessageRole.System, content));
        }

        public void Clear()
        {
            Messages.Clear();
            IsWaitingForResponse = false;
            PendingError = null;
        }
    }
}
