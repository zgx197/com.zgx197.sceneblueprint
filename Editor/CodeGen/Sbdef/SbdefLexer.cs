#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace SceneBlueprint.Editor.CodeGen.Sbdef
{
    internal enum TokenKind
    {
        Action, Port, Flow, Marker, Label, Gizmo, Min, Max,
        Extends, Annotation, EditorTool, Enum, Field,
        Default, Range, ShowIf, Tooltip, AutoAddAnnotation,
        Identifier,
        StringLiteral,
        NumberLiteral,
        BoolLiteral,
        LBrace, RBrace,
        Equals,
        LParen, RParen,
        Comma,
        EOF,
    }

    internal record Token(TokenKind Kind, string Value, int Line);

    internal static class SbdefLexer
    {
        public static List<Token> Tokenize(string source)
        {
            var tokens = new List<Token>();
            int i = 0, line = 1;

            while (i < source.Length)
            {
                char c = source[i];

                if (c == '\n') { line++; i++; continue; }
                if (char.IsWhiteSpace(c)) { i++; continue; }

                // 行注释
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    continue;
                }

                switch (c)
                {
                    case '{': tokens.Add(new Token(TokenKind.LBrace, "{", line)); i++; continue;
                    case '}': tokens.Add(new Token(TokenKind.RBrace, "}", line)); i++; continue;
                    case '=': tokens.Add(new Token(TokenKind.Equals, "=", line)); i++; continue;
                    case '(': tokens.Add(new Token(TokenKind.LParen, "(", line)); i++; continue;
                    case ')': tokens.Add(new Token(TokenKind.RParen, ")", line)); i++; continue;
                    case ',': tokens.Add(new Token(TokenKind.Comma, ",", line)); i++; continue;
                }

                // 字符串字面量
                if (c == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < source.Length && source[i] != '"')
                    {
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            i++;
                            sb.Append(source[i] == 'n' ? '\n' : source[i]);
                        }
                        else sb.Append(source[i]);
                        i++;
                    }
                    if (i < source.Length) i++; // 跳过结尾 "
                    tokens.Add(new Token(TokenKind.StringLiteral, sb.ToString(), line));
                    continue;
                }

                // 数字字面量（含负号）
                if (char.IsDigit(c) || (c == '-' && i + 1 < source.Length && char.IsDigit(source[i + 1])))
                {
                    var sb = new StringBuilder();
                    if (c == '-') { sb.Append(c); i++; }
                    while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.'))
                    {
                        sb.Append(source[i]); i++;
                    }
                    if (i < source.Length && (source[i] == 'f' || source[i] == 'F')) i++; // 跳过 f 后缀
                    tokens.Add(new Token(TokenKind.NumberLiteral, sb.ToString(), line));
                    continue;
                }

                // 标识符 / 关键字（允许 . 连接，例如 VFX.CameraShake）
                if (char.IsLetter(c) || c == '_')
                {
                    var sb = new StringBuilder();
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_' || source[i] == '.'))
                    {
                        sb.Append(source[i]); i++;
                    }
                    var word = sb.ToString();
                    var kind = word switch
                    {
                        "action" => TokenKind.Action,
                        "port"   => TokenKind.Port,
                        "flow"   => TokenKind.Flow,
                        "marker" => TokenKind.Marker,
                        "label"  => TokenKind.Label,
                        "gizmo"  => TokenKind.Gizmo,
                        "min"    => TokenKind.Min,
                        "max"    => TokenKind.Max,
                        "extends" => TokenKind.Extends,
                        "annotation" => TokenKind.Annotation,
                        "editor_tool" => TokenKind.EditorTool,
                        "enum"   => TokenKind.Enum,
                        "field"  => TokenKind.Field,
                        "default" => TokenKind.Default,
                        "range"  => TokenKind.Range,
                        "show_if" => TokenKind.ShowIf,
                        "tooltip" => TokenKind.Tooltip,
                        "auto_add_annotation" => TokenKind.AutoAddAnnotation,
                        "true"   => TokenKind.BoolLiteral,
                        "false"  => TokenKind.BoolLiteral,
                        _        => TokenKind.Identifier,
                    };
                    tokens.Add(new Token(kind, word, line));
                    continue;
                }

                throw new Exception($"[SbdefLexer] 未识别字符 '{c}'（行 {line}）");
            }

            tokens.Add(new Token(TokenKind.EOF, "", line));
            return tokens;
        }
    }
}
