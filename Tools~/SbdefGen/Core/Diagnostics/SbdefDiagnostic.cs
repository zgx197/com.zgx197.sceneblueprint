#nullable enable
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SbdefGen.Core.Diagnostics;

/// <summary>
/// 结构化诊断输出 — 替代原来依赖 UnityEngine.Debug.LogError 的实现。
/// 通过 ISbdefLogger 输出，CLI 环境下输出到 stderr，Unity 环境下输出到 Console。
/// </summary>
public static class SbdefDiagnostic
{
    public static void LogException(string assetPath, Exception ex, ISbdefLogger logger)
    {
        var fileName = Path.GetFileName(assetPath);
        var line     = ExtractLine(ex.Message);
        var location = line.HasValue ? $"({line.Value})" : "";

        logger.Error(
            $"[.sbdef] {fileName}{location}: {ex.Message}\n" +
            $"  → {assetPath}\n{ex.StackTrace}");
    }

    public static void LogPhaseError(string assetPath, string phase, Exception ex, ISbdefLogger logger)
    {
        var fileName = Path.GetFileName(assetPath);
        logger.Error(
            $"[.sbdef] {fileName} [{phase}]: {ex.Message}\n" +
            $"  → {assetPath}\n{ex.StackTrace}");
    }

    private static int? ExtractLine(string message)
    {
        var match = Regex.Match(message, @"行\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var line))
            return line;
        return null;
    }
}
