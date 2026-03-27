#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// StructList 属性值的跨层编解码辅助。
    /// 这层放在 Core，而不是散在 Editor/业务 compiler 中，
    /// 让“蓝图定义层的结构”可以被 Inspector、编译器和其他工具共享消费。
    /// </summary>
    public static class StructListValueCodec
    {
        public static List<Dictionary<string, object>> Deserialize(
            string? json,
            PropertyDefinition[]? fields)
        {
            var result = new List<Dictionary<string, object>>();
            if (string.IsNullOrWhiteSpace(json) || string.Equals(json.Trim(), "[]", StringComparison.Ordinal))
            {
                return result;
            }

            var safeFields = fields ?? Array.Empty<PropertyDefinition>();

            try
            {
                var trimmed = json.Trim();
                if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    return result;
                }

                var inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
                if (string.IsNullOrEmpty(inner))
                {
                    return result;
                }

                var objects = SplitTopLevel(inner);
                for (var index = 0; index < objects.Count; index++)
                {
                    var item = ParseObject(objects[index].Trim(), safeFields);
                    if (item != null)
                    {
                        result.Add(item);
                    }
                }
            }
            catch
            {
                // 结构化值读取失败时返回空列表，保持 authoring/preview 容错。
            }

            return result;
        }

        public static string Serialize(
            List<Dictionary<string, object>>? items,
            PropertyDefinition[]? fields)
        {
            if (items == null || items.Count == 0)
            {
                return "[]";
            }

            var safeFields = fields ?? Array.Empty<PropertyDefinition>();
            var sb = new StringBuilder();
            sb.Append('[');
            for (var index = 0; index < items.Count; index++)
            {
                if (index > 0)
                {
                    sb.Append(',');
                }

                SerializeObject(sb, items[index], safeFields);
            }

            sb.Append(']');
            return sb.ToString();
        }

        public static Dictionary<string, object> CreateDefaultItem(PropertyDefinition[]? fields)
        {
            return PropertyDefinitionValueUtility.CreateDefaultStructItem(fields);
        }

        public static int GetItemCount(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || string.Equals(json.Trim(), "[]", StringComparison.Ordinal))
            {
                return 0;
            }

            var count = 0;
            var depth = 0;
            var inString = false;
            for (var index = 0; index < json.Length; index++)
            {
                var c = json[index];
                if (c == '"' && (index == 0 || json[index - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '[' || c == '{')
                {
                    depth++;
                    if (c == '{' && depth == 2)
                    {
                        count++;
                    }
                }
                else if (c == ']' || c == '}')
                {
                    depth--;
                }
            }

            return count;
        }

        private static Dictionary<string, object>? ParseObject(string json, PropertyDefinition[] fields)
        {
            if (!json.StartsWith("{", StringComparison.Ordinal) || !json.EndsWith("}", StringComparison.Ordinal))
            {
                return null;
            }

            var item = new Dictionary<string, object>(StringComparer.Ordinal);
            var content = json.Substring(1, json.Length - 2).Trim();
            if (!string.IsNullOrEmpty(content))
            {
                var definitions = BuildDefinitionLookup(fields);
                var pairs = SplitTopLevel(content);
                for (var index = 0; index < pairs.Count; index++)
                {
                    var pair = pairs[index];
                    var colonIndex = pair.IndexOf(':');
                    if (colonIndex < 0)
                    {
                        continue;
                    }

                    var key = pair.Substring(0, colonIndex).Trim().Trim('"');
                    var value = pair.Substring(colonIndex + 1).Trim();
                    if (definitions.TryGetValue(key, out var field))
                    {
                        item[key] = ParseValue(value, field);
                    }
                    else
                    {
                        item[key] = ParseValueAuto(value);
                    }
                }
            }

            for (var index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                if (item.ContainsKey(field.Key))
                {
                    continue;
                }

                item[field.Key] = PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field);
            }

            return item;
        }

        private static object ParseValue(string value, PropertyDefinition field)
        {
            return field.Type switch
            {
                PropertyType.Int => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
                    ? intValue
                    : 0,
                PropertyType.Float => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue)
                    ? floatValue
                    : 0f,
                PropertyType.Bool => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
                PropertyType.String or PropertyType.Enum or PropertyType.SignalTagSelector
                    or PropertyType.EntityRefSelector or PropertyType.ConditionParams
                    => TrimQuotedString(value),
                PropertyType.StructList => Deserialize(value, field.StructFields),
                _ => ParseValueAuto(value)
            };
        }

        private static object ParseValueAuto(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
            {
                return TrimQuotedString(trimmed);
            }

            if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            {
                return intValue;
            }

            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            {
                return floatValue;
            }

            return trimmed;
        }

        private static void SerializeObject(StringBuilder sb, Dictionary<string, object> item, PropertyDefinition[] fields)
        {
            sb.Append('{');
            var first = true;
            for (var index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                if (!item.TryGetValue(field.Key, out var value))
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append('"').Append(field.Key).Append('"').Append(':');
                SerializeValue(sb, value, field);
            }

            sb.Append('}');
        }

        private static void SerializeValue(StringBuilder sb, object value, PropertyDefinition field)
        {
            switch (field.Type)
            {
                case PropertyType.Int:
                    sb.Append(Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                    break;
                case PropertyType.Float:
                    sb.Append(Convert.ToSingle(value, CultureInfo.InvariantCulture).ToString("G", CultureInfo.InvariantCulture));
                    break;
                case PropertyType.Bool:
                    sb.Append(Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? "true" : "false");
                    break;
                case PropertyType.StructList:
                    if (value is List<Dictionary<string, object>> nested)
                    {
                        sb.Append(Serialize(nested, field.StructFields));
                    }
                    else
                    {
                        sb.Append("[]");
                    }
                    break;
                default:
                    sb.Append('"').Append(EscapeString(value?.ToString() ?? string.Empty)).Append('"');
                    break;
            }
        }

        private static Dictionary<string, PropertyDefinition> BuildDefinitionLookup(PropertyDefinition[] fields)
        {
            var lookup = new Dictionary<string, PropertyDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < fields.Length; index++)
            {
                lookup[fields[index].Key] = fields[index];
            }

            return lookup;
        }

        private static List<string> SplitTopLevel(string input)
        {
            var result = new List<string>();
            var depth = 0;
            var start = 0;
            var inString = false;
            for (var index = 0; index < input.Length; index++)
            {
                var c = input[index];
                if (c == '"' && (index == 0 || input[index - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    depth++;
                }
                else if (c == '}' || c == ']')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    result.Add(input.Substring(start, index - start));
                    start = index + 1;
                }
            }

            if (start < input.Length)
            {
                result.Add(input.Substring(start));
            }

            return result;
        }

        private static string TrimQuotedString(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed.Replace("\\\"", "\"");
        }

        private static string EscapeString(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
