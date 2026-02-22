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
                var ports = new List<PortDecl>();
                while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                    ports.Add(ParsePort());
                Consume(TokenKind.RBrace);
                return new ActionDecl(typeId, ports);
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
                return new PortDecl(typeName, portName, defaultVal);
            }

            MarkerDecl ParseMarker()
            {
                Consume(TokenKind.Marker);
                var name = Consume(TokenKind.Identifier).Value;
                Consume(TokenKind.LBrace);
                // v0.1: 跳过 marker 块内容，不生成代码
                int depth = 1;
                while (depth > 0 && Peek().Kind != TokenKind.EOF)
                {
                    var t = ConsumeAny();
                    if (t.Kind == TokenKind.LBrace) depth++;
                    else if (t.Kind == TokenKind.RBrace) depth--;
                }
                return new MarkerDecl(name);
            }
        }
    }
}
