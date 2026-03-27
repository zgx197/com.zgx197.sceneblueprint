#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SceneBlueprint.Editor.Markers.Snapshot
{
    /// <summary>
    /// 轻量级 JSON 解析/序列化工具 — 仅供快照系统内部使用。
    /// <para>
    /// 支持：string / int / long / float / double / bool / null / array / object。
    /// 数值默认解析为 double（JSON 规范），调用方通过 SnapshotDataHelper 转换为具体类型。
    /// </para>
    /// </summary>
    internal static class MiniJson
    {
        /// <summary>将 JSON 字符串反序列化为 object（Dictionary / List / 基元类型）</summary>
        public static object? Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var parser = new Parser(json);
            return parser.ParseValue();
        }

        /// <summary>将 object 序列化为 JSON 字符串</summary>
        public static string Serialize(object? obj)
        {
            var sb = new StringBuilder();
            SerializeValue(sb, obj);
            return sb.ToString();
        }

        #region Parser

        private class Parser
        {
            private readonly string _json;
            private int _pos;

            public Parser(string json)
            {
                _json = json;
                _pos = 0;
            }

            public object? ParseValue()
            {
                SkipWhitespace();
                if (_pos >= _json.Length) return null;

                char c = _json[_pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't':
                    case 'f': return ParseBool();
                    case 'n': return ParseNull();
                    default:
                        if (c == '-' || char.IsDigit(c))
                            return ParseNumber();
                        throw new FormatException($"MiniJson: 位置 {_pos} 处遇到意外字符 '{c}'");
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                Expect('{');
                SkipWhitespace();

                if (_pos < _json.Length && _json[_pos] == '}')
                {
                    _pos++;
                    return dict;
                }

                while (true)
                {
                    SkipWhitespace();
                    string key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    var value = ParseValue();
                    dict[key] = value!;
                    SkipWhitespace();

                    if (_pos >= _json.Length) break;
                    if (_json[_pos] == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (_json[_pos] == '}')
                    {
                        _pos++;
                        break;
                    }
                    throw new FormatException($"MiniJson: 位置 {_pos} 处期望 ',' 或 '}}'");
                }

                return dict;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                Expect('[');
                SkipWhitespace();

                if (_pos < _json.Length && _json[_pos] == ']')
                {
                    _pos++;
                    return list;
                }

                while (true)
                {
                    var value = ParseValue();
                    list.Add(value!);
                    SkipWhitespace();

                    if (_pos >= _json.Length) break;
                    if (_json[_pos] == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (_json[_pos] == ']')
                    {
                        _pos++;
                        break;
                    }
                    throw new FormatException($"MiniJson: 位置 {_pos} 处期望 ',' 或 ']'");
                }

                return list;
            }

            private string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (_pos < _json.Length)
                {
                    char c = _json[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_pos >= _json.Length)
                            throw new FormatException("MiniJson: 字符串中遇到意外结尾");
                        char esc = _json[_pos++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'u':
                                if (_pos + 4 > _json.Length)
                                    throw new FormatException("MiniJson: \\u 转义不完整");
                                var hex = _json.Substring(_pos, 4);
                                sb.Append((char)Convert.ToInt32(hex, 16));
                                _pos += 4;
                                break;
                            default:
                                sb.Append(esc);
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                throw new FormatException("MiniJson: 字符串未正确结束");
            }

            private object ParseNumber()
            {
                int start = _pos;
                if (_json[_pos] == '-') _pos++;
                while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;

                bool isFloat = false;
                if (_pos < _json.Length && _json[_pos] == '.')
                {
                    isFloat = true;
                    _pos++;
                    while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;
                }
                if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E'))
                {
                    isFloat = true;
                    _pos++;
                    if (_pos < _json.Length && (_json[_pos] == '+' || _json[_pos] == '-')) _pos++;
                    while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;
                }

                string numStr = _json.Substring(start, _pos - start);
                if (isFloat)
                    return double.Parse(numStr, CultureInfo.InvariantCulture);

                if (long.TryParse(numStr, out var longVal))
                {
                    if (longVal >= int.MinValue && longVal <= int.MaxValue)
                        return (int)longVal;
                    return longVal;
                }
                return double.Parse(numStr, CultureInfo.InvariantCulture);
            }

            private bool ParseBool()
            {
                if (_json.Substring(_pos).StartsWith("true"))
                {
                    _pos += 4;
                    return true;
                }
                if (_json.Substring(_pos).StartsWith("false"))
                {
                    _pos += 5;
                    return false;
                }
                throw new FormatException($"MiniJson: 位置 {_pos} 处期望 true 或 false");
            }

            private object? ParseNull()
            {
                if (_json.Substring(_pos).StartsWith("null"))
                {
                    _pos += 4;
                    return null;
                }
                throw new FormatException($"MiniJson: 位置 {_pos} 处期望 null");
            }

            private void Expect(char c)
            {
                SkipWhitespace();
                if (_pos >= _json.Length || _json[_pos] != c)
                    throw new FormatException($"MiniJson: 位置 {_pos} 处期望 '{c}'");
                _pos++;
            }

            private void SkipWhitespace()
            {
                while (_pos < _json.Length && char.IsWhiteSpace(_json[_pos]))
                    _pos++;
            }
        }

        #endregion

        #region Serializer

        private static void SerializeValue(StringBuilder sb, object? value)
        {
            if (value == null)
            {
                sb.Append("null");
            }
            else if (value is string s)
            {
                sb.Append('"');
                sb.Append(EscapeString(s));
                sb.Append('"');
            }
            else if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else if (value is int i)
            {
                sb.Append(i);
            }
            else if (value is long l)
            {
                sb.Append(l);
            }
            else if (value is float f)
            {
                sb.Append(f.ToString(CultureInfo.InvariantCulture));
            }
            else if (value is double d)
            {
                sb.Append(d.ToString(CultureInfo.InvariantCulture));
            }
            else if (value is IDictionary<string, object> dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kvp in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"');
                    sb.Append(EscapeString(kvp.Key));
                    sb.Append("\":");
                    SerializeValue(sb, kvp.Value);
                }
                sb.Append('}');
            }
            else if (value is IList list)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    SerializeValue(sb, item);
                }
                sb.Append(']');
            }
            else
            {
                // fallback: 当作字符串
                sb.Append('"');
                sb.Append(EscapeString(value.ToString() ?? ""));
                sb.Append('"');
            }
        }

        private static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        #endregion
    }
}
