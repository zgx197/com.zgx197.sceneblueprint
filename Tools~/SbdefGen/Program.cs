using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SbdefGen;
using SbdefGen.Core.Pipeline;

// ── CLI 入口：sbdef-gen ──────────────────────────────────────
//
// 用法：
//   sbdef-gen --input <dir> --output <dir> [--verbose]
//   sbdef-gen --input <file.sbdef> --output <dir> [--verbose]
//
// 参数：
//   --input   输入目录（扫描所有 .sbdef 文件）或单个 .sbdef 文件路径
//   --output  生成文件的输出根目录
//   --verbose 输出详细日志
//
// 返回码：
//   0 = 成功
//   1 = 参数错误
//   2 = 处理过程中有错误

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding  = Encoding.UTF8;

var logger = new ConsoleLogger();
string? inputPath = null;
string? outputPath = null;

// ── 解析命令行参数 ──
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--input" or "-i":
            if (i + 1 < args.Length) inputPath = args[++i];
            break;
        case "--output" or "-o":
            if (i + 1 < args.Length) outputPath = args[++i];
            break;
        case "--verbose" or "-v":
            logger.Verbose = true;
            break;
        case "--help" or "-h":
            PrintUsage();
            return 0;
    }
}

if (string.IsNullOrEmpty(inputPath) || string.IsNullOrEmpty(outputPath))
{
    logger.Error("缺少必要参数 --input 和 --output");
    PrintUsage();
    return 1;
}

// ── 收集 .sbdef 文件 ──
var sbdefFiles = new Dictionary<string, string>();

if (File.Exists(inputPath))
{
    // 单文件模式
    var name = Path.GetFileName(inputPath);
    sbdefFiles[name] = File.ReadAllText(inputPath, Encoding.UTF8);
    logger.Info($"单文件模式: {name}");
}
else if (Directory.Exists(inputPath))
{
    // 目录扫描模式
    var files = Directory.GetFiles(inputPath, "*.sbdef", SearchOption.AllDirectories);
    foreach (var file in files)
    {
        var name = Path.GetFileName(file);
        sbdefFiles[name] = File.ReadAllText(file, Encoding.UTF8);
    }
    logger.Info($"目录扫描模式: 找到 {sbdefFiles.Count} 个 .sbdef 文件");
}
else
{
    logger.Error($"输入路径不存在: {inputPath}");
    return 1;
}

if (sbdefFiles.Count == 0)
{
    logger.Warn("未找到任何 .sbdef 文件");
    return 0;
}

// ── 运行管线 ──
var results = SbdefPipeline.Run(sbdefFiles, logger);

// ── 写入输出文件 ──
int totalWritten = 0;
bool hasErrors = false;

foreach (var result in results)
{
    for (var index = 0; index < result.ObsoleteOutputs.Count; index++)
    {
        var obsoletePath = Path.Combine(outputPath, result.ObsoleteOutputs[index]);
        var obsoleteMetaPath = obsoletePath + ".meta";

        if (File.Exists(obsoletePath))
        {
            try
            {
                File.Delete(obsoletePath);
                totalWritten++;
                logger.Info($"  删除陈旧输出: {result.ObsoleteOutputs[index]}");
            }
            catch (Exception e)
            {
                logger.Error($"删除失败: {obsoletePath} — {e.Message}");
                hasErrors = true;
            }
        }

        if (File.Exists(obsoleteMetaPath))
        {
            try
            {
                File.Delete(obsoleteMetaPath);
                totalWritten++;
                logger.Info($"  删除陈旧输出: {result.ObsoleteOutputs[index]}.meta");
            }
            catch (Exception e)
            {
                logger.Error($"删除失败: {obsoleteMetaPath} — {e.Message}");
                hasErrors = true;
            }
        }
    }

    foreach (var (fileName, content) in result.Outputs)
    {
        var destPath = Path.Combine(outputPath, fileName);
        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        // 安全网：仅在内容变化时写入（比较时统一换行符为 LF）
        if (File.Exists(destPath))
        {
            var existing = File.ReadAllText(destPath, Encoding.UTF8).Replace("\r\n", "\n");
            if (existing == content)
            {
                logger.Info($"  跳过（内容相同）: {fileName}");
                continue;
            }
        }

        try
        {
            File.WriteAllText(destPath, content, new UTF8Encoding(false));
            totalWritten++;
            logger.Info($"  写入: {fileName}");
        }
        catch (Exception e)
        {
            logger.Error($"写入失败: {destPath} — {e.Message}");
            hasErrors = true;
        }
    }
}

logger.Info($"完成 — 共写入 {totalWritten} 个文件");

if (hasErrors)
{
    logger.Warn("处理过程中存在错误，请检查上方日志");
    return 2;
}

return 0;

// ── 辅助方法 ──

static void PrintUsage()
{
    Console.WriteLine("sbdef-gen — .sbdef 代码生成 CLI 工具");
    Console.WriteLine();
    Console.WriteLine("用法:");
    Console.WriteLine("  sbdef-gen --input <dir|file> --output <dir> [--verbose]");
    Console.WriteLine();
    Console.WriteLine("参数:");
    Console.WriteLine("  --input, -i    输入目录或单个 .sbdef 文件路径");
    Console.WriteLine("  --output, -o   生成文件的输出目录");
    Console.WriteLine("  --verbose, -v  输出详细日志");
    Console.WriteLine("  --help, -h     显示帮助");
}
