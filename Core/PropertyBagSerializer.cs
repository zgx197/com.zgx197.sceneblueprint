#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  PropertyBag JSON 序列化器
    //
    //  为什么不用 Newtonsoft.Json 或 System.Text.Json？
    //  因为 SceneBlueprint.Core 标记了 noEngineReferences: true，
    //  且不希望引入外部 NuGet 依赖。简易 JSON 已足够处理
    //  PropertyBag 中的基本类型（int/float/bool/string）。
    //
    //  序列化规则：
    //  - int     → 无小数点的数字，如 42
    //  - float   → 带小数点的数字，如 2.5 或 3.0（强制带 .0）
    //  - bool    → true / false
    //  - string  → 双引号包裹，转义特殊字符
    //
    //  反序列化规则：
    //  - 无小数点的数字 → int（超出范围时 long）
    //  - 有小数点的数字 → float
    //  - true/false → bool
    //  - 双引号字符串 → string
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// PropertyBag 的 JSON 序列化/反序列化工具。
    /// <para>使用简易 JSON 实现，不依赖外部库。支持 int/float/bool/string 基本类型。</para>
    /// <para>
    /// 使用示例：
    /// <code>
    /// string json = PropertyBagSerializer.ToJson(bag);     // 序列化
    /// PropertyBag bag2 = PropertyBagSerializer.FromJson(json); // 反序列化
    /// </code>
    /// </para>
    /// </summary>
    public static class PropertyBagSerializer
    {
        /// <summary>
        /// 将 PropertyBag 序列化为 JSON 字符串。
        /// <para>输出格式：{"key1":value1,"key2":value2,...}</para>
        /// </summary>
        /// <param name="bag">要序列化的 PropertyBag</param>
        /// <returns>JSON 字符串</returns>
        public static string ToJson(PropertyBag bag)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            // 遍历所有键值对，逐个写入 JSON
            foreach (var kvp in bag.All)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(EscapeString(kvp.Key)).Append("\":");
                WriteValue(sb, kvp.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// 从 JSON 字符串反序列化为 PropertyBag。
        /// <para>如果输入为 null/空/非法 JSON，返回空的 PropertyBag（不抛异常）。</para>
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>反序列化后的 PropertyBag</returns>
        public static PropertyBag FromJson(string json)
        {
            var bag = new PropertyBag();
            if (string.IsNullOrWhiteSpace(json))
                return bag;

            int index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '{')
                return bag;
            index++; // skip '{'

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] == '}')
                    break;

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                // 读取 key
                string key = ReadString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ':')
                    index++;
                SkipWhitespace(json, ref index);

                // 读取 value
                object? value = ReadValue(json, ref index);
                if (value != null)
                    bag.Set(key, value);
            }

            return bag;
        }

        // ─── 写入辅助方法 ───

        /// <summary>将单个值写入 JSON，根据类型自动选择格式</summary>
        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case float f:
                    // 关键：确保浮点数总是带小数点（如 3.0 而不是 3）
                    // 这样反序列化时才能正确区分 int 和 float
                    string fs = f.ToString("R", CultureInfo.InvariantCulture);
                    sb.Append(fs);
                    if (!fs.Contains('.') && !fs.Contains('E') && !fs.Contains('e'))
                        sb.Append(".0");
                    break;
                case double d:
                    string ds = d.ToString("R", CultureInfo.InvariantCulture);
                    sb.Append(ds);
                    if (!ds.Contains('.') && !ds.Contains('E') && !ds.Contains('e'))
                        sb.Append(".0");
                    break;
                case string s:
                    sb.Append('"').Append(EscapeString(s)).Append('"');
                    break;
                default:
                    // 其他类型转为字符串
                    sb.Append('"').Append(EscapeString(value.ToString() ?? "")).Append('"');
                    break;
            }
        }

        /// <summary>JSON 字符串转义：处理双引号、反斜杠、换行等特殊字符</summary>
        private static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        // ─── 读取辅助方法（简易 JSON 解析器） ───

        /// <summary>跳过空白字符</summary>
        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
        }

        /// <summary>读取一个 JSON 字符串值（处理转义字符）</summary>
        private static string ReadString(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '"')
                return "";

            index++; // 跳过开头的双引号
            var sb = new StringBuilder();
            while (index < json.Length)
            {
                char c = json[index];
                if (c == '\\' && index + 1 < json.Length)
                {
                    // 处理转义序列
                    index++;
                    char next = json[index];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(next); break;
                    }
                }
                else if (c == '"')
                {
                    index++; // 跳过结尾的双引号
                    break;
                }
                else
                {
                    sb.Append(c);
                }
                index++;
            }
            return sb.ToString();
        }

        /// <summary>读取一个 JSON 值（根据首字符判断类型）</summary>
        private static object? ReadValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
                return null;

            char c = json[index];

            // 双引号开头 → 字符串
            if (c == '"')
                return ReadString(json, ref index);

            // t/f 开头 → 布尔
            if (c == 't' && index + 3 < json.Length && json.Substring(index, 4) == "true")
            {
                index += 4;
                return true;
            }
            if (c == 'f' && index + 4 < json.Length && json.Substring(index, 5) == "false")
            {
                index += 5;
                return false;
            }

            // null
            if (c == 'n' && index + 3 < json.Length && json.Substring(index, 4) == "null")
            {
                index += 4;
                return null;
            }

            // 数字
            if (c == '-' || char.IsDigit(c))
                return ReadNumber(json, ref index);

            return null;
        }

        /// <summary>
        /// 读取一个 JSON 数字。
        /// <para>根据是否包含小数点决定返回 int 还是 float：
        /// - 无小数点 → int（超出范围则 long）
        /// - 有小数点或科学计数法 → float
        /// </para>
        /// </summary>
        private static object ReadNumber(string json, ref int index)
        {
            int start = index;
            bool hasDecimal = false;

            // 处理负号
            if (json[index] == '-') index++;

            // 扫描数字字符
            while (index < json.Length)
            {
                char c = json[index];
                if (c == '.')
                {
                    hasDecimal = true;
                    index++;
                }
                else if (c == 'e' || c == 'E' || c == '+' || c == '-')
                {
                    hasDecimal = true; // 科学计数法视为浮点数
                    index++;
                }
                else if (char.IsDigit(c))
                {
                    index++;
                }
                else
                {
                    break; // 非数字字符，结束扫描
                }
            }

            string numStr = json.Substring(start, index - start);

            // 根据是否有小数点决定类型
            if (hasDecimal)
            {
                // 有小数点 → 解析为 float
                if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    return f;
                return 0f;
            }
            else
            {
                // 无小数点 → 优先解析为 int，超出范围则解析为 long
                if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                    return i;
                if (long.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    return l;
                return 0;
            }
        }
    }
}
