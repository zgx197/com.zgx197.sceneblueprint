#nullable enable
using NodeGraph.Core;
using NodeGraph.Serialization;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// ActionNodeData 的 JSON 序列化器。
    /// 实现 NodeGraph 的 IUserDataSerializer 接口，用于图的持久化（保存/加载）。
    /// 内部使用 PropertyBagSerializer 进行 JSON 转换。
    /// </summary>
    public class ActionNodeDataSerializer : IUserDataSerializer
    {
        public string SerializeNodeData(INodeData data)
        {
            if (data is ActionNodeData actionData)
            {
                // 格式: { "typeId": "xxx", "description": "...", "properties": { ... } }
                var sb = new System.Text.StringBuilder();
                sb.Append("{\"typeId\":\"");
                sb.Append(EscapeJson(actionData.ActionTypeId));
                sb.Append("\"");
                if (!string.IsNullOrEmpty(actionData.Description))
                {
                    sb.Append(",\"description\":\"");
                    sb.Append(EscapeJson(actionData.Description));
                    sb.Append("\"");
                }
                sb.Append(",\"properties\":");
                sb.Append(PropertyBagSerializer.ToJson(actionData.Properties));
                sb.Append("}");
                return sb.ToString();
            }
            
            if (data is SceneObjectProxyData proxyData)
            {
                // 格式: { "objectType": "xxx", "sceneObjectId": "xxx", "displayName": "xxx", "isBroken": false }
                var sb = new System.Text.StringBuilder();
                sb.Append("{\"objectType\":\"");
                sb.Append(EscapeJson(proxyData.ObjectType));
                sb.Append("\",\"sceneObjectId\":\"");
                sb.Append(EscapeJson(proxyData.SceneObjectId));
                sb.Append("\",\"displayName\":\"");
                sb.Append(EscapeJson(proxyData.DisplayName));
                sb.Append("\",\"isBroken\":");
                sb.Append(proxyData.IsBroken ? "true" : "false");
                sb.Append("}");
                return sb.ToString();
            }
            
            return "{}";
        }

        public INodeData? DeserializeNodeData(string typeId, string json)
        {
            if (string.IsNullOrEmpty(json) || json == "{}")
                return null;

            try
            {
                // 先判断是否为 SceneObjectProxyData（通过检查 objectType 字段）
                string? objectType = ExtractStringField(json, "objectType");
                if (objectType != null)
                {
                    // 这是 SceneObjectProxyData
                    string? sceneObjectId = ExtractStringField(json, "sceneObjectId");
                    string? displayName = ExtractStringField(json, "displayName");
                    bool isBroken = ExtractBoolField(json, "isBroken");

                    return new SceneObjectProxyData
                    {
                        ObjectType = objectType,
                        SceneObjectId = sceneObjectId ?? "",
                        DisplayName = displayName ?? "",
                        IsBroken = isBroken
                    };
                }

                // 否则尝试解析为 ActionNodeData
                string? actionTypeId = ExtractStringField(json, "typeId");
                string? propertiesJson = ExtractObjectField(json, "properties");

                if (actionTypeId == null)
                    return null;

                var data = new ActionNodeData(actionTypeId);
                string? description = ExtractStringField(json, "description");
                if (!string.IsNullOrEmpty(description))
                    data.Description = description;
                if (propertiesJson != null)
                    data.Properties = PropertyBagSerializer.FromJson(propertiesJson);
                return data;
            }
            catch
            {
                return null;
            }
        }

        // ── JSON 辅助方法 ──

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>从 JSON 中提取字符串字段值</summary>
        private static string? ExtractStringField(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\":\"";
            int start = json.IndexOf(pattern);
            if (start < 0) return null;

            start += pattern.Length;
            int end = start;
            while (end < json.Length)
            {
                if (json[end] == '"' && json[end - 1] != '\\')
                    break;
                end++;
            }
            return json.Substring(start, end - start);
        }

        /// <summary>从 JSON 中提取对象字段值（匹配花括号）</summary>
        private static string? ExtractObjectField(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return null;

            start += pattern.Length;
            // 跳过空白
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;

            if (start >= json.Length || json[start] != '{')
                return null;

            int depth = 0;
            int end = start;
            while (end < json.Length)
            {
                if (json[end] == '{') depth++;
                else if (json[end] == '}') depth--;
                if (depth == 0)
                {
                    end++;
                    break;
                }
                end++;
            }
            return json.Substring(start, end - start);
        }

        /// <summary>从 JSON 中提取布尔字段值</summary>
        private static bool ExtractBoolField(string json, string fieldName)
        {
            string pattern = $"\"{fieldName}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return false;

            start += pattern.Length;
            // 跳过空白
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;

            if (start >= json.Length) return false;

            // 检查 true/false
            if (start + 4 <= json.Length && json.Substring(start, 4) == "true")
                return true;
            
            return false;
        }
    }
}
