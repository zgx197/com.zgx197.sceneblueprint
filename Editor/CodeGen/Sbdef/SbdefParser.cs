#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Editor.CodeGen.Sbdef
{
    internal static class SbdefParser
    {
        public static SbdefFile Parse(List<Token> tokens)
        {
            int pos = 0;
            var statements = new List<SbdefStatement>();

            while (Peek().Kind != TokenKind.EOF)
            {
                if (Peek().Kind == TokenKind.Action)
                    statements.Add(ParseAction());
                else if (Peek().Kind == TokenKind.Marker)
                    statements.Add(ParseMarker());
                else
                    throw new Exception($"[SbdefParser] 期望 'action' 或 'marker'，得到 '{Peek().Value}'（行 {Peek().Line}）");
            }

            return new SbdefFile(statements);

            // ── 局部辅助 ──

            Token Peek() => tokens[pos];

            Token Consume(TokenKind kind)
            {
                var t = tokens[pos];
                if (t.Kind != kind)
                    throw new Exception($"[SbdefParser] 期望 {kind}，得到 '{t.Value}'（行 {t.Line}）");
                pos++;
                return t;
            }

            Token ConsumeAny() => tokens[pos++];

            ActionDecl ParseAction()
            {
                Consume(TokenKind.Action);
                var typeId = Consume(TokenKind.Identifier).Value;
                Consume(TokenKind.LBrace);

                string? displayName = null, category = null, description = null,
                        themeColor = null, duration = null;
                var ports = new List<PortDecl>();
                var flowPorts = new List<FlowPortDecl>();

                while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                {
                    if (Peek().Kind == TokenKind.Port)
                    {
                        ports.Add(ParsePort());
                    }
                    else if (Peek().Kind == TokenKind.Flow)
                    {
                        flowPorts.Add(ParseFlowPort());
                    }
                    else if (Peek().Kind == TokenKind.Identifier)
                    {
                        // action 元数据关键字（识别 Identifier 的 Value 来区分）
                        var keyword = Peek().Value;
                        switch (keyword)
                        {
                            case "displayName":
                                ConsumeAny();
                                displayName = Consume(TokenKind.StringLiteral).Value;
                                break;
                            case "category":
                                ConsumeAny();
                                category = Consume(TokenKind.StringLiteral).Value;
                                break;
                            case "description":
                                ConsumeAny();
                                description = Consume(TokenKind.StringLiteral).Value;
                                break;
                            case "themeColor":
                                ConsumeAny();
                                var r = ConsumeAny().Value;
                                var g = ConsumeAny().Value;
                                var b = ConsumeAny().Value;
                                themeColor = $"{r} {g} {b}";
                                break;
                            case "duration":
                                ConsumeAny();
                                duration = ConsumeAny().Value; // "instant" | "duration" | "passive"
                                break;
                            default:
                                throw new Exception(
                                    $"[SbdefParser] 未知 action 元数据关键字 '{keyword}'（行 {Peek().Line}）");
                        }
                    }
                    else
                    {
                        throw new Exception(
                            $"[SbdefParser] action 块内期望 'port' 或元数据关键字，得到 '{Peek().Value}'（行 {Peek().Line}）");
                    }
                }

                Consume(TokenKind.RBrace);
                var meta = new ActionMeta(displayName, category, description, themeColor, duration);
                return new ActionDecl(typeId, meta, ports, flowPorts);
            }

            FlowPortDecl ParseFlowPort()
            {
                Consume(TokenKind.Flow);
                var portName = Consume(TokenKind.Identifier).Value;
                string? label = null;
                if (Peek().Kind == TokenKind.Label)
                {
                    ConsumeAny();
                    label = Consume(TokenKind.StringLiteral).Value;
                }
                return new FlowPortDecl(portName, label);
            }

            PortDecl ParsePort()
            {
                Consume(TokenKind.Port);
                var typeName = Consume(TokenKind.Identifier).Value;
                var portName = Consume(TokenKind.Identifier).Value;
                string? defaultVal = null;
                if (Peek().Kind == TokenKind.Equals)
                {
                    ConsumeAny(); // =
                    defaultVal = ConsumeAny().Value;
                }
                // 可选的 label / min / max（任意顺序）
                string? label = null, min = null, max = null;
                while (Peek().Kind == TokenKind.Label ||
                       Peek().Kind == TokenKind.Min   ||
                       Peek().Kind == TokenKind.Max)
                {
                    switch (Peek().Kind)
                    {
                        case TokenKind.Label:
                            ConsumeAny();
                            label = Consume(TokenKind.StringLiteral).Value;
                            break;
                        case TokenKind.Min:
                            ConsumeAny();
                            min = ConsumeAny().Value;
                            break;
                        case TokenKind.Max:
                            ConsumeAny();
                            max = ConsumeAny().Value;
                            break;
                    }
                }
                return new PortDecl(typeName, portName, defaultVal, label, min, max);
            }

            MarkerDecl ParseMarker()
            {
                Consume(TokenKind.Marker);
                var name = Consume(TokenKind.Identifier).Value;
                Consume(TokenKind.LBrace);

                string? label = null, gizmoShape = null, gizmoParam = null;

                while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                {
                    if (Peek().Kind == TokenKind.Label)
                    {
                        ConsumeAny();
                        label = Consume(TokenKind.StringLiteral).Value;
                    }
                    else if (Peek().Kind == TokenKind.Gizmo)
                    {
                        ConsumeAny();
                        gizmoShape = Consume(TokenKind.Identifier).Value; // sphere / wire_sphere / box / wire_box
                        // 可选参数：sphere(0.3)
                        if (Peek().Kind == TokenKind.LParen)
                        {
                            ConsumeAny(); // (
                            gizmoParam = ConsumeAny().Value;
                            Consume(TokenKind.RParen);
                        }
                    }
                    else
                    {
                        // 跳过未知内容，兼容未来扩展
                        ConsumeAny();
                    }
                }

                Consume(TokenKind.RBrace);
                return new MarkerDecl(name, label, gizmoShape, gizmoParam);
            }
        }
    }
}
