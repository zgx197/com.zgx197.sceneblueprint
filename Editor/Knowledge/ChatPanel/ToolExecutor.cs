#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// 工具注册与执行器。
    /// 管理所有可供 LLM Function Calling 调用的工具。
    /// </summary>
    public class ToolExecutor
    {
        private readonly Dictionary<string, ToolDefinition> _definitions = new();
        private readonly Dictionary<string, Func<string, string>> _handlers = new();

        /// <summary>
        /// 注册一个工具。
        /// </summary>
        /// <param name="definition">工具定义</param>
        /// <param name="handler">执行函数：接收 arguments JSON 字符串，返回结果 JSON 字符串</param>
        public void Register(ToolDefinition definition, Func<string, string> handler)
        {
            _definitions[definition.Name] = definition;
            _handlers[definition.Name] = handler;
        }

        /// <summary>
        /// 获取所有已注册工具的定义列表。
        /// </summary>
        public List<ToolDefinition> GetAllDefinitions()
        {
            return new List<ToolDefinition>(_definitions.Values);
        }

        /// <summary>
        /// 执行指定工具。
        /// </summary>
        /// <param name="functionName">工具名称</param>
        /// <param name="argumentsJson">参数 JSON 字符串</param>
        /// <returns>执行结果 JSON 字符串</returns>
        public string Execute(string functionName, string argumentsJson)
        {
            if (!_handlers.TryGetValue(functionName, out var handler))
            {
                return $"{{\"error\":\"Unknown tool: {functionName}\"}}";
            }

            try
            {
                return handler(argumentsJson);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ToolExecutor] 工具 {functionName} 执行异常: {ex.Message}");
                return $"{{\"error\":\"{EscapeJson(ex.Message)}\"}}";
            }
        }

        /// <summary>
        /// 检查指定工具是否已注册。
        /// </summary>
        public bool HasTool(string functionName) => _handlers.ContainsKey(functionName);

        /// <summary>
        /// 序列化所有工具定义为 tools JSON 数组字符串。
        /// </summary>
        public string ToToolsJsonArray()
        {
            var defs = GetAllDefinitions();
            if (defs.Count == 0) return "[]";

            var parts = new string[defs.Count];
            for (int i = 0; i < defs.Count; i++)
                parts[i] = defs[i].ToJson();

            return "[" + string.Join(",", parts) + "]";
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
