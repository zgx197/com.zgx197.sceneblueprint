#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SbdefGen.Core.Ast;
using SbdefGen.Core.Lexing;
using SbdefGen.Core.Parsing;
using SbdefGen.Core.Emitters;

namespace SbdefGen.Core.Pipeline;

/// <summary>
/// 纯管线编排器 — 零 Unity 依赖。
/// 接收 .sbdef 源文件列表，输出生成的 C# 文件内容。
/// 失败时返回 null（安全网：调用方不覆盖旧文件）。
/// </summary>
public static class SbdefPipeline
{
    /// <summary>单个 .sbdef 文件的处理结果</summary>
    public record FileResult(string SourceName, Dictionary<string, string> Outputs, IReadOnlyList<string> ObsoleteOutputs);

    /// <summary>
    /// 运行完整管线：解析所有 .sbdef → 收集全局枚举 → 逐文件生成 C#。
    /// </summary>
    /// <param name="sbdefFiles">键=文件名（不含路径），值=文件源码</param>
    /// <param name="logger">日志实例</param>
    /// <returns>每个文件的生成结果列表；任何文件解析失败仍会继续处理其余文件</returns>
    public static List<FileResult> Run(Dictionary<string, string> sbdefFiles, ISbdefLogger logger)
    {
        // ── Phase 1：解析所有 sbdef，收集全局枚举注册表 + Marker 基类注册表 + use_annotations 映射 ──
        var globalEnumRegistry = new Dictionary<string, EnumDecl>();
        // markerName → baseType（如 "WaveSpawnArea" → "Area"，"SpawnPoint" → "Point"）
        // 无 extends 的 marker 映射到自身名称（如 "CameraTarget" → "CameraTarget"）
        var markerBaseTypeRegistry = new Dictionary<string, string>();
        // 全局 use_annotations 映射：annotationName → 被引用的 Marker 列表（跨文件去重用）
        var globalAnnotationUsage = new HashSet<string>();
        var parsedFiles = new List<(string name, SbdefFile ast)>();

        foreach (var (fileName, source) in sbdefFiles)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            try
            {
                var tokens = SbdefLexer.Tokenize(source);
                var ast = SbdefParser.Parse(tokens);
                parsedFiles.Add((name, ast));

                // 收集枚举到全局注册表
                foreach (var stmt in ast.Statements)
                {
                    if (stmt is EnumDecl enumDecl)
                        globalEnumRegistry[enumDecl.Name] = enumDecl;
                    // 收集 Marker 基类到注册表
                    if (stmt is MarkerDecl markerDecl)
                    {
                        markerBaseTypeRegistry[markerDecl.Name] = markerDecl.BaseType ?? markerDecl.Name;
                        // 收集 use_annotations 声明到全局映射
                        if (markerDecl.UsedAnnotations != null)
                            foreach (var ann in markerDecl.UsedAnnotations)
                                globalAnnotationUsage.Add(ann);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error($"[{fileName}] 解析失败: {e.Message}");
            }
        }

        if (globalEnumRegistry.Count > 0)
            logger.Info($"全局枚举注册表: {string.Join(", ", globalEnumRegistry.Keys)}");

        // ── Phase 2：带全局枚举注册表处理各文件 ──
        var results = new List<FileResult>();
        foreach (var (name, ast) in parsedFiles)
        {
            try
            {
                var contractsOnly = ast.Statements
                    .OfType<CodeGenDecl>()
                    .Any(static statement => statement.Option == CodeGenOptionKind.ContractsOnly);
                var outputs = ProcessFile(name, ast, globalEnumRegistry, markerBaseTypeRegistry, logger, globalAnnotationUsage, contractsOnly);
                results.Add(new FileResult(name, outputs, GetObsoleteOutputs(ast, contractsOnly)));
            }
            catch (Exception e)
            {
                logger.Error($"[{name}.sbdef] 生成失败: {e.Message}");
            }
        }

        return results;
    }

    private static Dictionary<string, string> ProcessFile(
        string name, SbdefFile ast,
        Dictionary<string, EnumDecl> globalEnumRegistry,
        Dictionary<string, string> markerBaseTypeRegistry,
        ISbdefLogger logger,
        HashSet<string> globalAnnotationUsage,
        bool contractsOnly)
    {
        var markerCount = ast.Statements.OfType<MarkerDecl>().Count();
        var annotationCount = ast.Statements.OfType<AnnotationDecl>().Count();
        var actionCount = ast.Statements.OfType<ActionDecl>().Count();
        var enumCount = ast.Statements.OfType<EnumDecl>().Count();
        var signalCount = ast.Statements.OfType<SignalDecl>().Count();
        var tagDimCount = ast.Statements.OfType<TagDimensionDecl>().Count();
        logger.Info($"  AST: {name}.sbdef → {markerCount} Marker, {annotationCount} Annotation, {actionCount} Action, {enumCount} Enum, {signalCount} Signal, {tagDimCount} TagDimension");

        // v0.1: UAT.*.cs
        var outputs = SbdefActionEmitter.Emit(ast, name);

        // v0.2: UActionPortIds.*.cs + ActionDefs.*.cs
        // contracts-only 文件只保留 UActionPortIds，不再生成 ActionDefs。
        TryMerge(
            outputs,
            () => SbdefDefEmitter.Emit(ast, name, globalEnumRegistry, markerBaseTypeRegistry, emitActionDefinitions: !contractsOnly),
            name,
            "SbdefDefEmitter",
            logger);

        // v0.3: UMarkerTypeIds.cs + Markers/*.cs + Editor/UMarkerDefs.cs
        TryMerge(outputs, () => SbdefMarkerEmitter.Emit(ast, name), name, "SbdefMarkerEmitter", logger);

        // v0.4: Annotations/*.cs + Editor/AnnotationDefs.*.cs + Enums/*.cs
        TryMerge(outputs, () => SbdefAnnotationEmitter.Emit(ast, name, globalEnumRegistry, logger, globalAnnotationUsage), name, "SbdefAnnotationEmitter", logger);

        // v0.4: Editor/EditorTools/*.cs
        TryMerge(outputs, () => SbdefEditorToolEmitter.Emit(ast, name), name, "SbdefEditorToolEmitter", logger);

        // v0.5: USignalTags.*.cs + USignalPayloads.*.cs
        TryMerge(outputs, () => SbdefSignalEmitter.Emit(ast, name), name, "SbdefSignalEmitter", logger);

        // v0.6: UTagDimensions.*.cs + TagDimensionDefs.*.cs
        TryMerge(outputs, () => SbdefTagDimensionEmitter.Emit(ast, name), name, "SbdefTagDimensionEmitter", logger);

        // 统一换行符为 LF
        var normalized = new Dictionary<string, string>();
        foreach (var (fileName, rawContent) in outputs)
            normalized[fileName] = rawContent.Replace("\r\n", "\n");

        return normalized;
    }

    private static IReadOnlyList<string> GetObsoleteOutputs(SbdefFile ast, bool contractsOnly)
    {
        if (!contractsOnly)
        {
            return Array.Empty<string>();
        }

        var obsolete = new List<string>();
        var actionSegments = ast.Statements
            .OfType<ActionDecl>()
            .Select(static action => FirstSegment(action.TypeId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < actionSegments.Count; index++)
        {
            var segmentPascal = SbdefActionEmitter.ToPascalSegment(actionSegments[index]);
            obsolete.Add($"ActionDefs.{segmentPascal}.cs");
        }

        return obsolete;
    }

    private static string FirstSegment(string typeId)
    {
        var dot = typeId.IndexOf('.');
        return dot > 0 ? typeId.Substring(0, dot) : typeId;
    }

    private static void TryMerge(
        Dictionary<string, string> outputs,
        Func<Dictionary<string, string>> emitter,
        string sourceName,
        string emitterName,
        ISbdefLogger logger)
    {
        try
        {
            var result = emitter();
            if (result.Count > 0)
                logger.Info($"  {emitterName}: {result.Count} 个输出文件");
            foreach (var kv in result)
                outputs[kv.Key] = kv.Value;
        }
        catch (Exception e)
        {
            logger.Error($"[{sourceName}.sbdef] [{emitterName}]: {e.Message}");
        }
    }
}
