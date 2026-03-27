#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// OpenAI Function Calling 兼容的工具定义。
    /// </summary>
    public class ToolDefinition
    {
        public string Name;
        public string Description;
        public List<ToolParameter> Parameters;

        public ToolDefinition(string name, string description, List<ToolParameter>? parameters = null)
        {
            Name = name;
            Description = description;
            Parameters = parameters ?? new List<ToolParameter>();
        }

        /// <summary>
        /// 序列化为 OpenAI tools 数组中的一个元素 JSON。
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"function\",\"function\":{");
            sb.Append($"\"name\":\"{JsonEscape(Name)}\",");
            sb.Append($"\"description\":\"{JsonEscape(Description)}\",");
            sb.Append("\"parameters\":{\"type\":\"object\",\"properties\":{");

            var required = new List<string>();
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var p = Parameters[i];
                sb.Append($"\"{JsonEscape(p.Name)}\":{{\"type\":\"{p.Type}\"");
                if (!string.IsNullOrEmpty(p.Description))
                    sb.Append($",\"description\":\"{JsonEscape(p.Description)}\"");
                sb.Append("}");
                if (p.Required) required.Add(p.Name);
            }

            sb.Append("}");
            if (required.Count > 0)
            {
                sb.Append(",\"required\":[");
                for (int i = 0; i < required.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append($"\"{JsonEscape(required[i])}\"");
                }
                sb.Append("]");
            }
            sb.Append("}}}");
            return sb.ToString();
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }

    /// <summary>
    /// 工具参数定义。
    /// </summary>
    public class ToolParameter
    {
        public string Name;
        public string Type;       // "string", "number", "integer", "boolean"
        public string Description;
        public bool Required;

        public ToolParameter(string name, string type, string description = "", bool required = false)
        {
            Name = name;
            Type = type;
            Description = description;
            Required = required;
        }
    }

}
