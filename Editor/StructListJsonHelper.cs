#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// StructList 属性的 JSON 序列化/反序列化辅助类。
    /// <para>
    /// 负责在 Editor 层将 PropertyBag 中的 JSON 字符串与
    /// List&lt;Dictionary&lt;string, object&gt;&gt; 之间互相转换。
    /// </para>
    /// <para>
    /// 使用轻量级手写解析器，避免引入额外 JSON 库依赖。
    /// 格式约定：[{"key1":value1,"key2":value2}, ...]
    /// 支持的值类型：int, float, bool, string（与 PropertyType 对应）
    /// </para>
    /// </summary>
    public static class StructListJsonHelper
    {
        /// <summary>
        /// 将 JSON 数组字符串反序列化为列表。
        /// 每个元素是一个 Dictionary，key 对应 StructFields 的 Key。
        /// </summary>
        public static List<Dictionary<string, object>> Deserialize(
            string json, PropertyDefinition[] fields)
        {
            var result = new List<Dictionary<string, object>>();
            if (string.IsNullOrEmpty(json) || json.Trim() == "[]")
                return result;

            try
            {
                // 使用 Unity 的 JsonUtility 需要包装器，这里用简单的手动解析
                json = json.Trim();
                if (!json.StartsWith("[") || !json.EndsWith("]"))
                    return result;

                // 去掉外层 []
                var inner = json.Substring(1, json.Length - 2).Trim();
                if (string.IsNullOrEmpty(inner))
                    return result;

                // 按顶层逗号分割对象（处理嵌套大括号）
                var objects = SplitTopLevelObjects(inner);
                foreach (var objStr in objects)
                {
                    var item = ParseObject(objStr.Trim(), fields);
                    if (item != null)
                        result.Add(item);
                }
            }
            catch (Exception)
            {
                // 解析失败时返回空列表，不中断编辑器
            }

            return result;
        }

        /// <summary>
        /// 将列表序列化为 JSON 数组字符串。
        /// </summary>
        public static string Serialize(
            List<Dictionary<string, object>> items, PropertyDefinition[] fields)
        {
            if (items == null || items.Count == 0)
                return "[]";

            var sb = new StringBuilder();
            sb.Append('[');

            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                SerializeObject(sb, items[i], fields);
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// 根据 StructFields 定义创建一个带默认值的新元素。
        /// </summary>
        public static Dictionary<string, object> CreateDefaultItem(PropertyDefinition[] fields)
        {
            var item = new Dictionary<string, object>();
            foreach (var field in fields)
            {
                if (field.DefaultValue != null)
                    item[field.Key] = field.DefaultValue;
                else
                {
                    // 根据类型给默认值
                    item[field.Key] = field.Type switch
                    {
                        PropertyType.Int => 0,
                        PropertyType.Float => 0f,
                        PropertyType.Bool => false,
                        PropertyType.Enum => field.EnumOptions is { Length: > 0 } ? field.EnumOptions[0] : "",
                        _ => ""
                    };
                }
            }
            return item;
        }

        /// <summary>
        /// 获取 StructList 的元素数量（从 JSON 字符串快速解析，不做完整反序列化）。
        /// 用于节点画布摘要显示。
        /// </summary>
        public static int GetItemCount(string? json)
        {
            if (string.IsNullOrEmpty(json) || json!.Trim() == "[]")
                return 0;

            // 简单计数：统计顶层 '{' 的数量
            int count = 0;
            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{' && depth == 1) count++; // depth==1 表示在数组内部
                if (c == '[' || c == '{') depth++;
                if (c == ']' || c == '}') depth--;
            }
            return count;
        }

        // ── 内部解析方法 ──

        /// <summary>按顶层逗号分割 JSON 对象（处理嵌套大括号）</summary>
        private static List<string> SplitTopLevelObjects(string inner)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            bool inString = false;

            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];

                if (c == '"' && (i == 0 || inner[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }
                if (inString) continue;

                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(inner.Substring(start, i - start));
                    start = i + 1;
                }
            }

            // 最后一段
            if (start < inner.Length)
                result.Add(inner.Substring(start));

            return result;
        }

        /// <summary>解析单个 JSON 对象为 Dictionary</summary>
        private static Dictionary<string, object>? ParseObject(string objStr, PropertyDefinition[] fields)
        {
            objStr = objStr.Trim();
            if (!objStr.StartsWith("{") || !objStr.EndsWith("}"))
                return null;

            var item = new Dictionary<string, object>();
            var content = objStr.Substring(1, objStr.Length - 2).Trim();
            if (string.IsNullOrEmpty(content))
                return item;

            // 建立字段类型索引
            var fieldTypes = new Dictionary<string, PropertyType>();
            foreach (var f in fields)
                fieldTypes[f.Key] = f.Type;

            // 按顶层逗号分割键值对
            var pairs = SplitTopLevelObjects(content);
            foreach (var pair in pairs)
            {
                var colonIdx = pair.IndexOf(':');
                if (colonIdx < 0) continue;

                var key = pair.Substring(0, colonIdx).Trim().Trim('"');
                var valueStr = pair.Substring(colonIdx + 1).Trim();

                // 根据字段类型解析值
                if (fieldTypes.TryGetValue(key, out var type))
                {
                    item[key] = ParseValue(valueStr, type);
                }
                else
                {
                    // 未知字段，尝试自动推断
                    item[key] = ParseValueAuto(valueStr);
                }
            }

            // 补充缺失字段的默认值
            foreach (var field in fields)
            {
                if (!item.ContainsKey(field.Key))
                {
                    item[field.Key] = field.DefaultValue ?? (field.Type switch
                    {
                        PropertyType.Int => (object)0,
                        PropertyType.Float => 0f,
                        PropertyType.Bool => false,
                        _ => ""
                    });
                }
            }

            return item;
        }

        /// <summary>根据已知类型解析 JSON 值</summary>
        private static object ParseValue(string valueStr, PropertyType type)
        {
            valueStr = valueStr.Trim();

            return type switch
            {
                PropertyType.Int => int.TryParse(valueStr, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var i) ? i : 0,
                PropertyType.Float => float.TryParse(valueStr, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var f) ? f : 0f,
                PropertyType.Bool => valueStr == "true",
                PropertyType.String or PropertyType.Enum => valueStr.Trim('"'),
                _ => ParseValueAuto(valueStr)
            };
        }

        /// <summary>自动推断 JSON 值类型</summary>
        private static object ParseValueAuto(string valueStr)
        {
            valueStr = valueStr.Trim();
            if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
                return valueStr.Substring(1, valueStr.Length - 2);
            if (valueStr == "true") return true;
            if (valueStr == "false") return false;
            if (int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return i;
            if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return f;
            return valueStr;
        }

        /// <summary>序列化单个对象</summary>
        private static void SerializeObject(StringBuilder sb, Dictionary<string, object> item, PropertyDefinition[] fields)
        {
            sb.Append('{');
            bool first = true;

            foreach (var field in fields)
            {
                if (!item.TryGetValue(field.Key, out var value))
                    continue;

                if (!first) sb.Append(',');
                first = false;

                sb.Append('"').Append(field.Key).Append('"').Append(':');
                SerializeValue(sb, value, field.Type);
            }

            sb.Append('}');
        }

        /// <summary>序列化单个值</summary>
        private static void SerializeValue(StringBuilder sb, object value, PropertyType type)
        {
            switch (type)
            {
                case PropertyType.Int:
                    sb.Append(Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture));
                    break;
                case PropertyType.Float:
                    sb.Append(Convert.ToSingle(value).ToString("G", CultureInfo.InvariantCulture));
                    break;
                case PropertyType.Bool:
                    sb.Append(Convert.ToBoolean(value) ? "true" : "false");
                    break;
                case PropertyType.String:
                case PropertyType.Enum:
                    sb.Append('"').Append(EscapeString(value?.ToString() ?? "")).Append('"');
                    break;
                default:
                    sb.Append('"').Append(EscapeString(value?.ToString() ?? "")).Append('"');
                    break;
            }
        }

        /// <summary>转义 JSON 字符串中的特殊字符</summary>
        private static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
