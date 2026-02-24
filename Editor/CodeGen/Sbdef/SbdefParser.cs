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
                else if (Peek().Kind == TokenKind.Annotation)
                    statements.Add(ParseTopLevelAnnotation());
                else
                    throw new Exception($"[SbdefParser] 期望 'action'、'marker' 或 'annotation'，得到 '{Peek().Value}'（行 {Peek().Line}）");
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
                var requirements = new List<RequireDecl>();

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
                            case "outport":
                                ports.Add(ParseDataPort("out"));
                                break;
                            case "inport":
                                ports.Add(ParseDataPort("in"));
                                break;
                            case "require":
                                requirements.Add(ParseRequire());
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
                return new ActionDecl(typeId, meta, ports, flowPorts,
                    requirements.Count > 0 ? requirements : null);
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

                // 处理 sceneref(MarkerType) 类型
                string? sceneRefType = null;
                if (typeName == "sceneref" && Peek().Kind == TokenKind.LParen)
                {
                    ConsumeAny(); // (
                    sceneRefType = Consume(TokenKind.Identifier).Value; // Area, Point, …
                    Consume(TokenKind.RParen); // )
                }

                var portName = Consume(TokenKind.Identifier).Value;
                string? defaultVal = null;
                if (Peek().Kind == TokenKind.Equals)
                {
                    ConsumeAny(); // =
                    defaultVal = ConsumeAny().Value;
                }

                // 可选修饰符：label / min / max / summary / { nested fields }
                string? label = null, min = null, max = null, summary = null;
                List<FieldDecl>? nestedPortFields = null;

                bool keepParsing = true;
                while (keepParsing)
                {
                    if (Peek().Kind == TokenKind.Label)
                    {
                        ConsumeAny();
                        label = Consume(TokenKind.StringLiteral).Value;
                    }
                    else if (Peek().Kind == TokenKind.Min)
                    {
                        ConsumeAny();
                        min = ConsumeAny().Value;
                    }
                    else if (Peek().Kind == TokenKind.Max)
                    {
                        ConsumeAny();
                        max = ConsumeAny().Value;
                    }
                    else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "summary")
                    {
                        ConsumeAny();
                        summary = Consume(TokenKind.StringLiteral).Value;
                    }
                    else if (Peek().Kind == TokenKind.LBrace && typeName == "list")
                    {
                        ConsumeAny(); // {
                        nestedPortFields = new List<FieldDecl>();
                        while (Peek().Kind == TokenKind.Field)
                            nestedPortFields.Add(ParseField());
                        Consume(TokenKind.RBrace); // }
                    }
                    else
                    {
                        keepParsing = false;
                    }
                }

                return new PortDecl(typeName, portName, defaultVal, label, min, max,
                    sceneRefType, null, summary, nestedPortFields);
            }

            // outport int WaveIndex label "..."
            PortDecl ParseDataPort(string direction)
            {
                ConsumeAny(); // outport / inport
                var typeName = Consume(TokenKind.Identifier).Value; // int / float / string / bool
                var portName = Consume(TokenKind.Identifier).Value;
                string? label = null;
                if (Peek().Kind == TokenKind.Label)
                {
                    ConsumeAny();
                    label = Consume(TokenKind.StringLiteral).Value;
                }
                return new PortDecl(typeName, portName, null, label, null, null,
                    null, direction, null, null);
            }

            // require Area SpawnArea label "刷怪区域" required
            RequireDecl ParseRequire()
            {
                ConsumeAny(); // require
                var markerType = Consume(TokenKind.Identifier).Value; // Area / Point
                var portName   = Consume(TokenKind.Identifier).Value; // 对应 sceneref port 名称
                string? label  = null;
                if (Peek().Kind == TokenKind.Label)
                {
                    ConsumeAny();
                    label = Consume(TokenKind.StringLiteral).Value;
                }
                bool isRequired = false;
                if (Peek().Kind == TokenKind.Identifier && Peek().Value == "required")
                {
                    ConsumeAny();
                    isRequired = true;
                }
                return new RequireDecl(markerType, portName, label, isRequired);
            }

            MarkerDecl ParseMarker()
            {
                Consume(TokenKind.Marker);
                var name = Consume(TokenKind.Identifier).Value;
                
                // 可选的 extends BaseType
                string? baseType = null;
                if (Peek().Kind == TokenKind.Extends)
                {
                    ConsumeAny();
                    baseType = Consume(TokenKind.Identifier).Value;
                }
                
                Consume(TokenKind.LBrace);

                string? label = null, gizmoShape = null, gizmoParam = null, group = null;
                var annotations = new List<AnnotationBlockDecl>();
                var editorTools = new List<EditorToolDecl>();
                List<string>? usedAnnotations = null;

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
                        gizmoShape = Consume(TokenKind.Identifier).Value;
                        if (Peek().Kind == TokenKind.LParen)
                        {
                            ConsumeAny();
                            gizmoParam = ConsumeAny().Value;
                            Consume(TokenKind.RParen);
                        }
                    }
                    else if (Peek().Kind == TokenKind.Annotation)
                    {
                        annotations.Add(ParseAnnotationBlock());
                    }
                    else if (Peek().Kind == TokenKind.EditorTool)
                    {
                        editorTools.Add(ParseEditorTool());
                    }
                    else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "use_annotations")
                    {
                        ConsumeAny();
                        usedAnnotations = ParseIdentifierList();
                    }
                    else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "group")
                    {
                        ConsumeAny();
                        group = Consume(TokenKind.StringLiteral).Value;
                    }
                    else
                    {
                        ConsumeAny();
                    }
                }

                Consume(TokenKind.RBrace);
                return new MarkerDecl(
                    name, 
                    label, 
                    gizmoShape, 
                    gizmoParam, 
                    baseType,
                    annotations.Count > 0 ? annotations : null,
                    editorTools.Count > 0 ? editorTools : null,
                    usedAnnotations,
                    group);
            }

            AnnotationBlockDecl ParseAnnotationBlock()
            {
                Consume(TokenKind.Annotation);
                var name = Consume(TokenKind.Identifier).Value;
                Consume(TokenKind.LBrace);

                string? displayName = null;
                var fields = new List<FieldDecl>();

                while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                {
                    if (Peek().Kind == TokenKind.Identifier && Peek().Value == "display_name")
                    {
                        ConsumeAny();
                        displayName = Consume(TokenKind.StringLiteral).Value;
                    }
                    else if (Peek().Kind == TokenKind.Field)
                    {
                        fields.Add(ParseField());
                    }
                    else
                    {
                        ConsumeAny();
                    }
                }

                Consume(TokenKind.RBrace);
                return new AnnotationBlockDecl(name, displayName, fields);
            }

            FieldDecl ParseField()
            {
                Consume(TokenKind.Field);
                // typeName 允许是普通 Identifier（如 string/int/float/list）或 enum 关键字（TokenKind.Enum）
                var typeToken = tokens[pos];
                if (typeToken.Kind != TokenKind.Identifier && typeToken.Kind != TokenKind.Enum)
                    throw new Exception($"[SbdefParser] 期望字段类型，得到 '{typeToken.Value}'（行 {typeToken.Line}）");
                pos++;
                var typeName = typeToken.Value;
                var fieldName = Consume(TokenKind.Identifier).Value;

                string? label = null, defaultValue = null, min = null, max = null, tooltip = null, showIf = null;
                List<string>? enumValues = null;
                List<FieldDecl>? nestedFields = null;

                // 解析修饰符
                while (Peek().Kind != TokenKind.Field && 
                       Peek().Kind != TokenKind.RBrace && 
                       Peek().Kind != TokenKind.EOF &&
                       Peek().Kind != TokenKind.Annotation &&
                       Peek().Kind != TokenKind.EditorTool)
                {
                    if (Peek().Kind == TokenKind.Label)
                    {
                        ConsumeAny();
                        label = Consume(TokenKind.StringLiteral).Value;
                    }
                    else if (Peek().Kind == TokenKind.Default)
                    {
                        ConsumeAny();
                        Consume(TokenKind.LParen);
                        defaultValue = ConsumeAny().Value;
                        Consume(TokenKind.RParen);
                    }
                    else if (Peek().Kind == TokenKind.Range)
                    {
                        ConsumeAny();
                        Consume(TokenKind.LParen);
                        min = ConsumeAny().Value;
                        Consume(TokenKind.Comma);
                        max = ConsumeAny().Value;
                        Consume(TokenKind.RParen);
                    }
                    else if (Peek().Kind == TokenKind.Min)
                    {
                        ConsumeAny();
                        Consume(TokenKind.LParen);
                        min = ConsumeAny().Value;
                        Consume(TokenKind.RParen);
                    }
                    else if (Peek().Kind == TokenKind.Tooltip)
                    {
                        ConsumeAny();
                        Consume(TokenKind.LParen);
                        tooltip = Consume(TokenKind.StringLiteral).Value;
                        Consume(TokenKind.RParen);
                    }
                    else if (Peek().Kind == TokenKind.ShowIf)
                    {
                        ConsumeAny();
                        Consume(TokenKind.LParen);
                        showIf = Consume(TokenKind.Identifier).Value;
                        Consume(TokenKind.RParen);
                    }
                    else if (Peek().Kind == TokenKind.LBrace)
                    {
                        // enum 值列表或 list 嵌套字段
                        ConsumeAny();
                        if (typeName == "enum")
                        {
                            enumValues = new List<string>();
                            while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                            {
                                enumValues.Add(Consume(TokenKind.Identifier).Value);
                                // 跳过可选的 label "..." 属性
                                if (Peek().Kind == TokenKind.Label)
                                {
                                    ConsumeAny();
                                    ConsumeAny(); // StringLiteral
                                }
                                if (Peek().Kind == TokenKind.Comma) ConsumeAny();
                            }
                        }
                        else if (typeName == "list")
                        {
                            nestedFields = new List<FieldDecl>();
                            while (Peek().Kind == TokenKind.Field)
                            {
                                nestedFields.Add(ParseField());
                            }
                        }
                        Consume(TokenKind.RBrace);
                    }
                    else
                    {
                        break;
                    }
                }

                return new FieldDecl(typeName, fieldName, label, defaultValue, min, max, tooltip, showIf, enumValues, nestedFields);
            }

            EditorToolDecl ParseEditorTool()
            {
                Consume(TokenKind.EditorTool);
                var name = Consume(TokenKind.Identifier).Value;
                Consume(TokenKind.LBrace);

                string? displayName = null, autoAddAnnotation = null;
                var parameters = new List<ToolParameterDecl>();

                while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                {
                    // 检查关键字（可能是 Identifier 或 Token）
                    if (Peek().Kind == TokenKind.AutoAddAnnotation)
                    {
                        ConsumeAny();
                        autoAddAnnotation = Consume(TokenKind.Identifier).Value;
                    }
                    else if (Peek().Kind == TokenKind.Identifier)
                    {
                        var keyword = Peek().Value;
                        if (keyword == "display_name")
                        {
                            ConsumeAny();
                            displayName = Consume(TokenKind.StringLiteral).Value;
                        }
                        else
                        {
                            // 工具参数：name type [修饰符]
                            parameters.Add(ParseToolParameter());
                        }
                    }
                    else
                    {
                        ConsumeAny();
                    }
                }

                Consume(TokenKind.RBrace);
                return new EditorToolDecl(name, displayName, parameters, autoAddAnnotation);
            }

            ToolParameterDecl ParseToolParameter()
            {
                var paramName = Consume(TokenKind.Identifier).Value;
                
                // 类型可以是关键字（enum/int/float/bool/string）或自定义类型（Identifier）
                var typeToken = Peek();
                string typeName;
                if (typeToken.Kind == TokenKind.Enum)
                {
                    ConsumeAny();
                    typeName = "enum";
                }
                else
                {
                    typeName = Consume(TokenKind.Identifier).Value;
                }

                string? defaultValue = null, min = null, max = null;
                List<string>? enumValues = null;

                while (Peek().Kind != TokenKind.Identifier && 
                       Peek().Kind != TokenKind.RBrace && 
                       Peek().Kind != TokenKind.EOF)
                {
                    if (Peek().Kind == TokenKind.Default)
                    {
                        ConsumeAny();
                        Consume(TokenKind.LParen);
                        defaultValue = ConsumeAny().Value;
                        Consume(TokenKind.RParen);
                    }
                    else if (Peek().Kind == TokenKind.Range)
                    {
                        ConsumeAny();
                        Consume(TokenKind.LParen);
                        min = ConsumeAny().Value;
                        Consume(TokenKind.Comma);
                        max = ConsumeAny().Value;
                        Consume(TokenKind.RParen);
                    }
                    else if (Peek().Kind == TokenKind.LBrace)
                    {
                        // enum 值列表
                        ConsumeAny();
                        enumValues = new List<string>();
                        while (Peek().Kind != TokenKind.RBrace)
                        {
                            enumValues.Add(Consume(TokenKind.Identifier).Value);
                            if (Peek().Kind == TokenKind.Comma) ConsumeAny();
                        }
                        Consume(TokenKind.RBrace);
                    }
                    else
                    {
                        break;
                    }
                }

                return new ToolParameterDecl(paramName, typeName, defaultValue, min, max, enumValues);
            }

            AnnotationDecl ParseTopLevelAnnotation()
            {
                Consume(TokenKind.Annotation);
                var name = Consume(TokenKind.Identifier).Value;
                Consume(TokenKind.LBrace);

                string? displayName = null;
                var fields = new List<FieldDecl>();

                while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                {
                    if (Peek().Kind == TokenKind.Field)
                    {
                        fields.Add(ParseField());
                    }
                    else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "display_name")
                    {
                        ConsumeAny();
                        displayName = Consume(TokenKind.StringLiteral).Value;
                    }
                    else
                    {
                        ConsumeAny();
                    }
                }

                Consume(TokenKind.RBrace);
                return new AnnotationDecl(name, displayName, fields);
            }

            List<string> ParseIdentifierList()
            {
                var list = new List<string>();
                list.Add(Consume(TokenKind.Identifier).Value);

                while (Peek().Kind == TokenKind.Comma)
                {
                    ConsumeAny(); // consume comma
                    list.Add(Consume(TokenKind.Identifier).Value);
                }

                return list;
            }
        }
    }
}
