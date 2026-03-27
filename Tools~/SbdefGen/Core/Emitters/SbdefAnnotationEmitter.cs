#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SbdefGen.Core;
using SbdefGen.Core.Ast;

namespace SbdefGen.Core.Emitters;

/// <summary>
/// .sbdef Annotation 代码生成器 — 从 annotation 块生成 Annotation 类和 AnnotationDef。
/// <para>
/// 生成产物：
/// - Annotations/{Name}Annotation.cs — Runtime Annotation 类
/// - Editor/AnnotationDefs.{SourceName}.cs — Editor AnnotationDefinitionProvider
/// </para>
/// </summary>
internal static class SbdefAnnotationEmitter
{
    public static Dictionary<string, string> Emit(SbdefFile ast, string sourceName, Dictionary<string, EnumDecl>? globalEnumRegistry = null, ISbdefLogger? logger = null, HashSet<string>? globalAnnotationUsage = null)
    {
        var output = new Dictionary<string, string>();
        var annotationDefs = new List<string>();
        
        // 0. 合并全局枚举注册表 + 本文件内的枚举声明
        var enumRegistry = globalEnumRegistry != null
            ? new Dictionary<string, EnumDecl>(globalEnumRegistry)
            : new Dictionary<string, EnumDecl>();
        foreach (var stmt in ast.Statements)
        {
            if (stmt is EnumDecl enumDecl)
            {
                enumRegistry[enumDecl.Name] = enumDecl;
                output[$"Enums/{enumDecl.Name}.cs"] = GenerateEnumFile(enumDecl);
            }
        }

        // 阶段4：构建 Annotation → List<MarkerTypeId> 映射
        var annotationToMarkers = new Dictionary<string, List<string>>();

        // 1. 处理顶层独立 Annotation（阶段4）
        foreach (var stmt in ast.Statements)
        {
            if (stmt is AnnotationDecl annotation)
            {
                var className = $"{annotation.Name}Annotation";
                var runtimeCode = GenerateAnnotationClassFromDecl(annotation, className, enumRegistry);
                output[$"Annotations/{className}.cs"] = runtimeCode;
                
                // 初始化映射（后续收集使用该 Annotation 的 Marker）
                annotationToMarkers[annotation.Name] = new List<string>();
            }
        }

        // 2. 处理嵌套式 Annotation（阶段3，兼容旧代码）
        foreach (var stmt in ast.Statements)
        {
            if (stmt is MarkerDecl marker && marker.Annotations != null)
            {
                foreach (var annotation in marker.Annotations)
                {
                    var className = $"{annotation.Name}Annotation";
                    var runtimeCode = GenerateAnnotationClassFromBlock(annotation, className, enumRegistry);
                    output[$"Annotations/{className}.cs"] = runtimeCode;
                    
                    // 嵌套式 Annotation 自动关联到当前 Marker
                    if (!annotationToMarkers.ContainsKey(annotation.Name))
                        annotationToMarkers[annotation.Name] = new List<string>();
                    annotationToMarkers[annotation.Name].Add(marker.Name);
                }
            }
        }

        // 3. 收集 use_annotations 声明（阶段4）
        foreach (var stmt in ast.Statements)
        {
            if (stmt is MarkerDecl marker && marker.UsedAnnotations != null)
            {
                foreach (var annotationName in marker.UsedAnnotations)
                {
                    // 如果 Annotation 不在当前文件中，可能定义在其他 .sbdef 文件（如 annotations.sbdef）
                    // 先添加到映射表，如果最终没有生成对应的 Annotation 类，AnnotationDef 会失败
                    if (!annotationToMarkers.ContainsKey(annotationName))
                        annotationToMarkers[annotationName] = new List<string>();
                    
                    annotationToMarkers[annotationName].Add(marker.Name);
                }
            }
        }

        // 4. 生成 AnnotationDef（使用收集的 ApplicableMarkerTypes）
        foreach (var (annotationName, markerNames) in annotationToMarkers)
        {
            var className = $"{annotationName}Annotation";
            var displayName = annotationName; // 简化版，实际应从 AST 读取
            if (markerNames.Count > 0)
            {
                annotationDefs.Add(GenerateAnnotationDefWithMarkers(annotationName, className, displayName, markerNames));
            }
            else
            {
                // 跨文件去重：如果该 Annotation 已被其他 .sbdef 文件的 Marker 通过 use_annotations 引用，
                // 则由那个文件生成带 ApplicableMarkerTypes 的 AnnotationDef，此处跳过
                if (globalAnnotationUsage != null && globalAnnotationUsage.Contains(annotationName))
                {
                    // 其他文件会生成带 Marker 限制的 Def，此处不重复生成
                    continue;
                }

                // 安全网：独立 Annotation 未被任何 Marker 的 use_annotations 引用
                // 仍生成 AnnotationDef（ApplicableMarkerTypes = null，适用所有 Marker），
                // 防止快照恢复时因找不到定义而丢失数据
                logger?.Warn($"[SbdefAnnotationEmitter] Annotation '{annotationName}' 未被任何 Marker 的 use_annotations 引用，" +
                    $"将生成无限制的 AnnotationDef（适用所有 Marker）。建议在对应 Marker 中添加 use_annotations {annotationName}");
                annotationDefs.Add(GenerateAnnotationDefWithoutMarkers(annotationName, className, displayName));
            }
        }

        // 5. 生成 AnnotationDefs 文件
        if (annotationDefs.Count > 0)
        {
            var defsCode = GenerateAnnotationDefsFile(annotationDefs, sourceName);
            output[$"Editor/AnnotationDefs.{SbdefNameUtility.ToPascal(sourceName)}.cs"] = defsCode;
        }

        // 日志（替代原来的 UnityEngine.Debug.Log）
        logger?.Info($"  [SbdefAnnotationEmitter] {sourceName}.sbdef → {output.Count} 个文件: {string.Join(", ", output.Keys)}");

        return output;
    }

    private static string GenerateAnnotationClassFromDecl(AnnotationDecl annotation, string className, Dictionary<string, EnumDecl> enumRegistry)
    {
        return GenerateAnnotationClassCore(annotation.Name, annotation.DisplayName, annotation.Fields, className, enumRegistry);
    }

    private static string GenerateAnnotationClassFromBlock(AnnotationBlockDecl annotation, string className, Dictionary<string, EnumDecl> enumRegistry)
    {
        return GenerateAnnotationClassCore(annotation.Name, annotation.DisplayName, annotation.Fields, className, enumRegistry);
    }

    private static string GenerateAnnotationClassCore(string name, string? displayName, List<FieldDecl> fields, string className, Dictionary<string, EnumDecl> enumRegistry)
    {
        // 动态部分：辅助类型 + 字段 + CollectExportData + RestoreFromExportData
        var bodySb = new StringBuilder();

        GenerateHelperTypes(bodySb, fields, prefix: "", enumRegistry: enumRegistry);

        bodySb.AppendLine($"    public partial class {className} : MarkerAnnotation");
        bodySb.AppendLine( "    {");
        bodySb.AppendLine($"        public override string AnnotationTypeId => \"{name}\";");
        bodySb.AppendLine();

        foreach (var field in fields)
            GenerateField(bodySb, field, 2, prefix: "", enumRegistry: enumRegistry);

        bodySb.AppendLine();
        bodySb.AppendLine("        public override void CollectExportData(IDictionary<string, object> data)");
        bodySb.AppendLine("        {");
        foreach (var field in fields)
            GenerateExportStatement(bodySb, field, indent: 3, prefix: "", enumRegistry: enumRegistry);
        bodySb.AppendLine("        }");

        bodySb.AppendLine();
        bodySb.AppendLine("        public override void RestoreFromExportData(IDictionary<string, object> data)");
        bodySb.AppendLine("        {");
        var restoreVarIndex = 0;
        foreach (var field in fields)
            GenerateRestoreStatement(bodySb, field, indent: 3, prefix: "", enumRegistry: enumRegistry, ref restoreVarIndex);
        bodySb.AppendLine("        }");

        bodySb.AppendLine("    }");

        var body = bodySb.ToString();
        return $$"""
// <auto-generated>
// 由 SbdefCodegen 从 {{name}}.sbdef 生成，请勿手动修改。
// </auto-generated>
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SceneBlueprint.Runtime.Markers.Annotations;
using SceneBlueprint.Runtime.Snapshot;

namespace SceneBlueprintUser.Annotations
{
{{body}}}

""";
    }

    /// <summary>在命名空间级别生成辅助类型（内联 enum + [Serializable] 数据类），跳过引用公共枚举</summary>
    private static void GenerateHelperTypes(StringBuilder sb, List<FieldDecl> fields, string prefix, Dictionary<string, EnumDecl> enumRegistry)
    {
        foreach (var field in fields)
        {
            var pascalName = SbdefNameUtility.ToPascal(field.Name);

            if (field.TypeName == "enum" && field.EnumRef != null)
            {
                // 引用公共枚举，不生成内联类型（已由独立枚举文件处理）
                continue;
            }
            else if (field.TypeName == "enum" && field.EnumValues != null)
            {
                // 内联枚举：照常生成
                sb.AppendLine($"    public enum {prefix}{pascalName}Type");
                sb.AppendLine("    {");
                foreach (var ev in field.EnumValues)
                    sb.AppendLine($"        {ev},");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
            else if (field.TypeName == "list" && field.NestedFields != null)
            {
                var itemPrefix = $"{prefix}{SbdefNameUtility.BuildListItemBaseName(pascalName)}";
                // 先递归生成嵌套字段的辅助类型
                GenerateHelperTypes(sb, field.NestedFields, prefix: itemPrefix, enumRegistry: enumRegistry);
                // 生成数据类
                sb.AppendLine("    [Serializable]");
                sb.AppendLine($"    public partial class {itemPrefix}Item");
                sb.AppendLine("    {");
                foreach (var nested in field.NestedFields)
                    GenerateField(sb, nested, 2, prefix: itemPrefix, enumRegistry: enumRegistry);
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }
    }

    private static void GenerateExportStatement(StringBuilder sb, FieldDecl field, int indent, string prefix, Dictionary<string, EnumDecl>? enumRegistry = null)
    {
        var indentStr = new string(' ', indent * 4);
        var camelName = SbdefNameUtility.ToCamel(field.Name);
        var pascalName = SbdefNameUtility.ToPascal(field.Name);

        if (field.TypeName == "list" && field.NestedFields != null)
        {
            sb.AppendLine($"{indentStr}data[\"{camelName}\"] = {pascalName}.Select(e => (object)new Dictionary<string, object>");
            sb.AppendLine($"{indentStr}{{");
            foreach (var nested in field.NestedFields)
            {
                var nestedCamel = SbdefNameUtility.ToCamel(nested.Name);
                var nestedPascal = SbdefNameUtility.ToPascal(nested.Name);
                // 内联枚举和引用枚举都需要 ToString()
                var valueExpr = nested.TypeName == "enum" ? $"e.{nestedPascal}.ToString()" : $"e.{nestedPascal}";
                sb.AppendLine($"{indentStr}    [\"{nestedCamel}\"] = {valueExpr},");
            }
            sb.AppendLine($"{indentStr}}}).ToList<object>();");
        }
        else if (field.TypeName == "enum")
        {
            // 内联枚举和引用枚举都导出为字符串
            sb.AppendLine($"{indentStr}data[\"{camelName}\"] = {pascalName}.ToString();");
        }
        else
        {
            sb.AppendLine($"{indentStr}data[\"{camelName}\"] = {pascalName};");
        }
    }

    private static void GenerateRestoreStatement(StringBuilder sb, FieldDecl field, int indent, string prefix, Dictionary<string, EnumDecl>? enumRegistry, ref int varIndex)
    {
        var indentStr = new string(' ', indent * 4);
        var camelName = SbdefNameUtility.ToCamel(field.Name);
        var pascalName = SbdefNameUtility.ToPascal(field.Name);

        if (field.TypeName == "list" && field.NestedFields != null)
        {
            // list 类型：Clear + 遍历 IList 中的每个 dict，逐字段恢复
            var idx = varIndex;
            var listVar = $"_list{idx}";
            var rawListVar = $"_rawList{idx}";
            var itemVar = $"_item{idx}";
            var dictVar = $"_dict{idx}";
            var entryVar = $"_entry{idx}";
            varIndex++;
            sb.AppendLine($"{indentStr}{pascalName}.Clear();");
            sb.AppendLine($"{indentStr}if (data.TryGetValue(\"{camelName}\", out var {listVar}) && {listVar} is IList {rawListVar})");
            sb.AppendLine($"{indentStr}{{");
            sb.AppendLine($"{indentStr}    foreach (var {itemVar} in {rawListVar})");
            sb.AppendLine($"{indentStr}    {{");
            sb.AppendLine($"{indentStr}        if ({itemVar} is not IDictionary<string, object> {dictVar}) continue;");

            var itemPrefix = $"{prefix}{SbdefNameUtility.BuildListItemBaseName(pascalName)}";
            sb.AppendLine($"{indentStr}        var {entryVar} = new {itemPrefix}Item();");
            foreach (var nested in field.NestedFields)
            {
                var nestedCamel = SbdefNameUtility.ToCamel(nested.Name);
                var nestedPascal = SbdefNameUtility.ToPascal(nested.Name);
                var nestedVar = $"_nv{varIndex}";
                varIndex++;
                sb.AppendLine($"{indentStr}        if ({dictVar}.TryGetValue(\"{nestedCamel}\", out var {nestedVar}))");
                sb.AppendLine($"{indentStr}            {entryVar}.{nestedPascal} = {GetRestoreExpression(nested, nestedVar, itemPrefix, enumRegistry)};");
            }
            sb.AppendLine($"{indentStr}        {pascalName}.Add({entryVar});");
            sb.AppendLine($"{indentStr}    }}");
            sb.AppendLine($"{indentStr}}}");
        }
        else
        {
            // 简单类型：TryGetValue + 类型转换
            var v = $"_v{varIndex}";
            varIndex++;
            sb.AppendLine($"{indentStr}if (data.TryGetValue(\"{camelName}\", out var {v}))");
            sb.AppendLine($"{indentStr}    {pascalName} = {GetRestoreExpression(field, v, prefix, enumRegistry)};");
        }
    }

    /// <summary>根据 field 类型生成从 object 变量恢复值的表达式</summary>
    private static string GetRestoreExpression(FieldDecl field, string varName, string prefix, Dictionary<string, EnumDecl>? enumRegistry)
    {
        if (field.TypeName == "enum")
        {
            string enumType;
            if (field.EnumRef != null)
                enumType = field.EnumRef;
            else
                enumType = $"{prefix}{SbdefNameUtility.ToPascal(field.Name)}Type";
            return $"SnapshotDataHelper.ToEnum<{enumType}>({varName})";
        }

        return field.TypeName switch
        {
            "int" => $"SnapshotDataHelper.ToInt({varName})",
            "float" => $"SnapshotDataHelper.ToFloat({varName})",
            "bool" => $"SnapshotDataHelper.ToBool({varName})",
            "string" => $"SnapshotDataHelper.ToStr({varName})",
            _ => $"SnapshotDataHelper.ToStr({varName})"
        };
    }

    private static void GenerateField(StringBuilder sb, FieldDecl field, int indent, string prefix, Dictionary<string, EnumDecl>? enumRegistry = null)
    {
        var indentStr = new string(' ', indent * 4);
        var fieldName = SbdefNameUtility.ToPascal(field.Name);

        if (!string.IsNullOrEmpty(field.Tooltip))
            sb.AppendLine($"{indentStr}[Tooltip(\"{field.Tooltip}\")]");

        if (!string.IsNullOrEmpty(field.Min) && !string.IsNullOrEmpty(field.Max))
        {
            var min = field.TypeName == "float" ? EnsureFloatSuffix(field.Min) : field.Min;
            var max = field.TypeName == "float" ? EnsureFloatSuffix(field.Max) : field.Max;
            sb.AppendLine($"{indentStr}[Range({min}, {max})]");
        }
        else if (!string.IsNullOrEmpty(field.Min))
        {
            var min = field.TypeName == "float" ? EnsureFloatSuffix(field.Min) : field.Min;
            sb.AppendLine($"{indentStr}[Min({min})]");
        }

        string csType = MapType(field, prefix, enumRegistry);
        string defaultValue = GetDefaultValue(field, prefix, enumRegistry);

        sb.AppendLine($"{indentStr}public {csType} {fieldName} = {defaultValue};");
        sb.AppendLine();
    }

    private static string MapType(FieldDecl field, string prefix, Dictionary<string, EnumDecl>? enumRegistry = null)
    {
        var pascalName = SbdefNameUtility.ToPascal(field.Name);
        if (field.TypeName == "enum" && field.EnumRef != null)
            return field.EnumRef; // 引用公共枚举：直接使用枚举类名
        return field.TypeName switch
        {
            "string" => "string",
            "int" => "int",
            "float" => "float",
            "bool" => "bool",
            "enum" => $"{prefix}{pascalName}Type", // 内联枚举
            "list" => $"List<{prefix}{SbdefNameUtility.BuildListItemTypeName(pascalName)}>",
            _ => "object"
        };
    }

    private static string EnsureFloatSuffix(string value)
    {
        return value.EndsWith("f") || value.EndsWith("F") ? value : value + "f";
    }

    private static string GetDefaultValue(FieldDecl field, string prefix, Dictionary<string, EnumDecl>? enumRegistry = null)
    {
        var pascalName = SbdefNameUtility.ToPascal(field.Name);

        if (!string.IsNullOrEmpty(field.DefaultValue))
        {
            if (field.TypeName == "string") return $"\"{field.DefaultValue}\"";
            if (field.TypeName == "float") return EnsureFloatSuffix(field.DefaultValue);
            // 枚举类型的显式默认值（如 default(Elite)）
            if (field.TypeName == "enum")
            {
                if (field.EnumRef != null)
                    return $"{field.EnumRef}.{field.DefaultValue}";
                return $"{prefix}{pascalName}Type.{field.DefaultValue}";
            }
            return field.DefaultValue;
        }

        if (field.TypeName == "enum")
        {
            // 引用枚举：从 EnumDecl 取第一个值
            if (field.EnumRef != null && enumRegistry != null && enumRegistry.TryGetValue(field.EnumRef, out var decl) && decl.Values.Count > 0)
                return $"{field.EnumRef}.{decl.Values[0].Name}";
            // 内联枚举：取第一个值
            if (field.EnumValues?.Count > 0)
                return $"{prefix}{pascalName}Type.{field.EnumValues[0]}";
            return "default";
        }

        return field.TypeName switch
        {
            "string" => "\"\"",
            "int" => "0",
            "float" => "0f",
            "bool" => "false",
            "list" => "new()",
            _ => "default!"
        };
    }

    private static string GenerateAnnotationDefWithMarkers(string annotationName, string className, string displayName, List<string> markerNames)
    {
        var markerTypeIds = string.Join(", ", markerNames.Select(m => $"UMarkerTypeIds.{m}"));
        return $@"
    [AnnotationDef(""{annotationName}"")]
    public class {annotationName}AnnotationDef : IAnnotationDefinitionProvider
    {{
        public AnnotationDefinition Define() => new AnnotationDefinition
        {{
            TypeId = ""{annotationName}"",
            DisplayName = ""{displayName}"",
            ComponentType = typeof({className}),
            ApplicableMarkerTypes = new[] {{ {markerTypeIds} }},
            AllowMultiple = false
        }};
    }}";
    }

    private static string GenerateAnnotationDefWithoutMarkers(string annotationName, string className, string displayName)
    {
        return $@"
    [AnnotationDef(""{annotationName}"")]
    public class {annotationName}AnnotationDef : IAnnotationDefinitionProvider
    {{
        public AnnotationDefinition Define() => new AnnotationDefinition
        {{
            TypeId = ""{annotationName}"",
            DisplayName = ""{displayName}"",
            ComponentType = typeof({className}),
            ApplicableMarkerTypes = null,
            AllowMultiple = false
        }};
    }}";
    }

    private static string GenerateAnnotationDefsFile(List<string> defs, string sourceName)
    {
        var defsBody = string.Join("\n", defs);
        return $$"""
// <auto-generated>
// 由 SbdefCodegen 生成
// </auto-generated>
#nullable enable
using SceneBlueprint.Editor.Markers.Annotations;
using SceneBlueprintUser.Annotations;
using SceneBlueprintUser.Generated;

namespace SceneBlueprintUser.Editor
{
{{defsBody}}
}

""";
    }

    /// <summary>生成独立公共枚举文件（Enums/{Name}.cs）</summary>
    private static string GenerateEnumFile(EnumDecl enumDecl)
    {
        var members = string.Join("\n", enumDecl.Values.Select(v =>
            v.ExplicitValue.HasValue
                ? $"        {v.Name} = {v.ExplicitValue.Value},"
                : $"        {v.Name},"));

        return $$"""
// <auto-generated>
// 由 SbdefCodegen 生成的公共枚举，请勿手动修改。
// </auto-generated>

namespace SceneBlueprintUser.Annotations
{
    public enum {{enumDecl.Name}}
    {
{{members}}
    }
}

""";
    }

}
