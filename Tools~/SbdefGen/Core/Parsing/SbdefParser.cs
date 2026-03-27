#nullable enable
using System;
using System.Collections.Generic;
using SbdefGen.Core.Ast;
using SbdefGen.Core.Lexing;

namespace SbdefGen.Core.Parsing;

internal static class SbdefParser
{
    public static SbdefFile Parse(List<Token> tokens)
    {
        int pos = 0;
        var statements = new List<SbdefStatement>();

        while (Peek().Kind != TokenKind.EOF)
        {
            if (Peek().Kind == TokenKind.CodeGen)
                statements.Add(ParseCodeGen());
            else if (Peek().Kind == TokenKind.Action)
                statements.Add(ParseAction());
            else if (Peek().Kind == TokenKind.Marker)
                statements.Add(ParseMarker());
            else if (Peek().Kind == TokenKind.Annotation)
                statements.Add(ParseTopLevelAnnotation());
            else if (Peek().Kind == TokenKind.Enum)
                statements.Add(ParseEnumDecl());
            else if (Peek().Kind == TokenKind.Signal)
                statements.Add(ParseSignal());
            else if (Peek().Kind == TokenKind.TagDimension)
                statements.Add(ParseTagDimension());
            else
                throw new Exception($"[SbdefParser] 期望 'codegen'、'action'、'marker'、'annotation'、'enum'、'signal' 或 'tagdimension'，得到 '{Peek().Value}'（行 {Peek().Line}）");
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

        CodeGenDecl ParseCodeGen()
        {
            Consume(TokenKind.CodeGen);
            var optionToken = Consume(TokenKind.Identifier);
            return optionToken.Value switch
            {
                "contracts_only" => new CodeGenDecl(CodeGenOptionKind.ContractsOnly),
                _ => throw new Exception(
                    $"[SbdefParser] 未知 codegen 选项 '{optionToken.Value}'（行 {optionToken.Line}）。当前仅支持 'contracts_only'。"),
            };
        }

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
            var fields = new List<FieldDecl>();

            while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
            {
                if (Peek().Kind == TokenKind.Port)
                {
                    throw new Exception(
                        $"[SbdefParser] action 块内不再支持 'port' 声明配置属性，请使用 'field' 代替（行 {Peek().Line}）。" +
                        "数据端口请使用 'outport' / 'inport'。");
                }
                else if (Peek().Kind == TokenKind.Field)
                {
                    fields.Add(ParseField());
                }
                else if (Peek().Kind == TokenKind.Flow)
                {
                    flowPorts.Add(ParseFlowPort(isInput: false));
                }
                else if (Peek().Kind == TokenKind.InFlow)
                {
                    flowPorts.Add(ParseFlowPort(isInput: true));
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
                            throw new Exception(
                                $"[SbdefParser] 'require' 语句已废弃（行 {Peek().Line}）。" +
                                "请将绑定约束直接写在 'field sceneref(...)' 上。" +
                                " 示例: field sceneref(Area) SpawnArea label \"...\" required exclusive");
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
                requirements.Count > 0 ? requirements : null,
                fields.Count > 0 ? fields : null);
        }

        FlowPortDecl ParseFlowPort(bool isInput = false)
        {
            if (isInput) Consume(TokenKind.InFlow);
            else Consume(TokenKind.Flow);
            var portName = Consume(TokenKind.Identifier).Value;
            string? label = null;
            if (Peek().Kind == TokenKind.Label)
            {
                ConsumeAny();
                label = Consume(TokenKind.StringLiteral).Value;
            }
            return new FlowPortDecl(portName, label, IsInput: isInput);
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
            // typeName 允许是普通 Identifier（如 string/int/float/list/sceneref）或 enum 关键字（TokenKind.Enum）
            var typeToken = tokens[pos];
            if (typeToken.Kind != TokenKind.Identifier && typeToken.Kind != TokenKind.Enum)
                throw new Exception($"[SbdefParser] 期望字段类型，得到 '{typeToken.Value}'（行 {typeToken.Line}）");
            pos++;
            var typeName = typeToken.Value;

            // 检测引用型公共枚举语法：enum(EnumName) 或 sceneref(Area)
            string? enumRef = null;
            string? sceneRefType = null;
            if (typeName == "enum" && Peek().Kind == TokenKind.LParen)
            {
                ConsumeAny(); // (
                enumRef = Consume(TokenKind.Identifier).Value;
                Consume(TokenKind.RParen); // )
            }
            else if (typeName == "sceneref" && Peek().Kind == TokenKind.LParen)
            {
                ConsumeAny(); // (
                sceneRefType = Consume(TokenKind.Identifier).Value;
                Consume(TokenKind.RParen); // )
            }

            var fieldName = Consume(TokenKind.Identifier).Value;

            // 支持 = value 默认值语法（与 ParsePort 一致）
            string? defaultValue = null;
            if (Peek().Kind == TokenKind.Equals)
            {
                ConsumeAny(); // =
                defaultValue = ConsumeAny().Value;
            }

            string? label = null, min = null, max = null, tooltip = null, showIf = null, summary = null;
            List<string>? enumValues = null;
            List<FieldDecl>? nestedFields = null;
            // 绑定约束修饰符（仅 sceneref 类型有效）
            bool isRequired = false, isExclusive = false;
            List<string>? requiredAnnotations = null;

            // 解析修饰符（遇到下一个声明关键字时终止）
            while (Peek().Kind != TokenKind.Field && 
                   Peek().Kind != TokenKind.RBrace && 
                   Peek().Kind != TokenKind.EOF &&
                   Peek().Kind != TokenKind.Annotation &&
                   Peek().Kind != TokenKind.EditorTool &&
                   Peek().Kind != TokenKind.Flow &&
                   Peek().Kind != TokenKind.Port)
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
                    // 支持两种语法：min(value) 和 min value
                    if (Peek().Kind == TokenKind.LParen)
                    {
                        ConsumeAny();
                        min = ConsumeAny().Value;
                        Consume(TokenKind.RParen);
                    }
                    else
                    {
                        min = ConsumeAny().Value;
                    }
                }
                else if (Peek().Kind == TokenKind.Max)
                {
                    ConsumeAny();
                    // 支持两种语法：max(value) 和 max value
                    if (Peek().Kind == TokenKind.LParen)
                    {
                        ConsumeAny();
                        max = ConsumeAny().Value;
                        Consume(TokenKind.RParen);
                    }
                    else
                    {
                        max = ConsumeAny().Value;
                    }
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
                else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "summary")
                {
                    ConsumeAny();
                    summary = Consume(TokenKind.StringLiteral).Value;
                }
                else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "required")
                {
                    ConsumeAny();
                    isRequired = true;
                }
                else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "exclusive")
                {
                    ConsumeAny();
                    isExclusive = true;
                }
                else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "annotation")
                {
                    ConsumeAny();
                    Consume(TokenKind.LParen);
                    var annoTypeId = Consume(TokenKind.StringLiteral).Value;
                    Consume(TokenKind.RParen);
                    requiredAnnotations ??= new List<string>();
                    requiredAnnotations.Add(annoTypeId);
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

            return new FieldDecl(typeName, fieldName, label, defaultValue, min, max, tooltip, showIf,
                enumValues, nestedFields, enumRef, sceneRefType, summary,
                isRequired, isExclusive, requiredAnnotations);
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

        EnumDecl ParseEnumDecl()
        {
            Consume(TokenKind.Enum);
            var name = Consume(TokenKind.Identifier).Value;
            Consume(TokenKind.LBrace);

            var values = new List<EnumValueDecl>();
            while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
            {
                var valueName = Consume(TokenKind.Identifier).Value;
                int? explicitValue = null;
                if (Peek().Kind == TokenKind.Equals)
                {
                    ConsumeAny(); // consume '='
                    var numToken = Consume(TokenKind.NumberLiteral);
                    explicitValue = int.Parse(numToken.Value);
                }
                string? valueLabel = null;
                if (Peek().Kind == TokenKind.Label)
                {
                    ConsumeAny();
                    valueLabel = Consume(TokenKind.StringLiteral).Value;
                }
                if (Peek().Kind == TokenKind.Comma) ConsumeAny();
                values.Add(new EnumValueDecl(valueName, valueLabel, explicitValue));
            }

            Consume(TokenKind.RBrace);
            return new EnumDecl(name, values);
        }

        /// <summary>
        /// 解析 signal 声明：
        /// signal Combat.Monster.Died label "怪物死亡" description "..." { param string EntityId label "实体ID" }
        /// 或无参数形式：
        /// signal Phase.Started label "阶段开始"
        /// </summary>
        SignalDecl ParseSignal()
        {
            Consume(TokenKind.Signal);
            var tagPath = Consume(TokenKind.Identifier).Value;

            string? label = null, description = null;
            List<SignalParamDecl>? signalParams = null;

            // 解析可选修饰符和参数块
            while (Peek().Kind != TokenKind.EOF)
            {
                if (Peek().Kind == TokenKind.Label)
                {
                    ConsumeAny();
                    label = Consume(TokenKind.StringLiteral).Value;
                }
                else if (Peek().Kind == TokenKind.Identifier && Peek().Value == "description")
                {
                    ConsumeAny();
                    description = Consume(TokenKind.StringLiteral).Value;
                }
                else if (Peek().Kind == TokenKind.LBrace)
                {
                    // 参数块
                    ConsumeAny();
                    signalParams = new List<SignalParamDecl>();
                    while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                    {
                        if (Peek().Kind == TokenKind.Identifier && Peek().Value == "param")
                        {
                            ConsumeAny(); // param
                            var typeName = Consume(TokenKind.Identifier).Value;
                            var paramName = Consume(TokenKind.Identifier).Value;
                            string? paramLabel = null;
                            if (Peek().Kind == TokenKind.Label)
                            {
                                ConsumeAny();
                                paramLabel = Consume(TokenKind.StringLiteral).Value;
                            }
                            signalParams.Add(new SignalParamDecl(typeName, paramName, paramLabel));
                        }
                        else
                        {
                            ConsumeAny(); // 跳过未知内容
                        }
                    }
                    Consume(TokenKind.RBrace);
                    break; // 参数块后结束
                }
                else
                {
                    break; // 遇到非修饰符 token，结束
                }
            }

            return new SignalDecl(tagPath, label, description, signalParams);
        }

        /// <summary>
        /// 解析 tagdimension 声明：
        /// tagdimension CombatRole { displayName "战斗角色" exclusive values { Frontline label "前锋" ... } }
        /// </summary>
        TagDimensionDecl ParseTagDimension()
        {
            Consume(TokenKind.TagDimension);
            var name = Consume(TokenKind.Identifier).Value;
            Consume(TokenKind.LBrace);

            string? displayName = null;
            bool isExclusive = false; // 默认 multiple
            var values = new List<TagDimensionValueDecl>();

            while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
            {
                if (Peek().Kind == TokenKind.Identifier)
                {
                    var keyword = Peek().Value;
                    switch (keyword)
                    {
                        case "displayName":
                            ConsumeAny();
                            displayName = Consume(TokenKind.StringLiteral).Value;
                            break;
                        case "exclusive":
                            ConsumeAny();
                            isExclusive = true;
                            break;
                        case "multiple":
                            ConsumeAny();
                            isExclusive = false;
                            break;
                        case "values":
                            ConsumeAny();
                            Consume(TokenKind.LBrace);
                            while (Peek().Kind != TokenKind.RBrace && Peek().Kind != TokenKind.EOF)
                            {
                                var valueName = Consume(TokenKind.Identifier).Value;
                                string? valueLabel = null;
                                if (Peek().Kind == TokenKind.Label)
                                {
                                    ConsumeAny();
                                    valueLabel = Consume(TokenKind.StringLiteral).Value;
                                }
                                if (Peek().Kind == TokenKind.Comma) ConsumeAny();
                                values.Add(new TagDimensionValueDecl(valueName, valueLabel));
                            }
                            Consume(TokenKind.RBrace);
                            break;
                        default:
                            throw new Exception(
                                $"[SbdefParser] tagdimension 块内未知关键字 '{keyword}'（行 {Peek().Line}）");
                    }
                }
                else
                {
                    throw new Exception(
                        $"[SbdefParser] tagdimension 块内期望关键字，得到 '{Peek().Value}'（行 {Peek().Line}）");
                }
            }

            Consume(TokenKind.RBrace);
            return new TagDimensionDecl(name, displayName, isExclusive, values);
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
