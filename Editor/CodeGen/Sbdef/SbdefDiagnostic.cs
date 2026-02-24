#nullable enable
using System.Text.RegularExpressions;

namespace SceneBlueprint.Editor.CodeGen.Sbdef
{
    /// <summary>
    /// v0.5：.sbdef 解析与生成错误的结构化诊断输出。
    /// <para>
    /// 将异常信息中的行号提取出来，以 "文件名(行号)" 格式输出到 Unity Console，
    /// 便于策划和工程师快速定位 .sbdef 文件中的语法错误。
    /// </para>
    /// </summary>
    internal static class SbdefDiagnostic
    {
        /// <summary>
        /// 将解析/生成异常格式化为 Unity Console 错误，包含文件名和行号。
        /// </summary>
        /// <param name="assetPath">Unity 相对路径，如 "Assets/.../vfx.sbdef"</param>
        /// <param name="ex">捕获的异常</param>
        public static void LogException(string assetPath, System.Exception ex)
        {
            var fileName = System.IO.Path.GetFileName(assetPath);
            var line     = ExtractLine(ex.Message);
            var location = line.HasValue ? $"({line.Value})" : "";

            // 格式与 Unity 编译错误一致，双击可跳转（对 .sbdef 文件无法自动跳转，但格式清晰）
            UnityEngine.Debug.LogError(
                $"[.sbdef] {fileName}{location}: {ex.Message}\n" +
                $"  → {assetPath}\n{ex.StackTrace}");
        }

        /// <summary>
        /// 标明具体阶段（词法分析 / 语法解析 / 代码生成 / 文件写入）的错误。
        /// </summary>
        /// <param name="assetPath">Unity 相对路径</param>
        /// <param name="phase">失败阶段名称，如 "词法分析"、"语法解析"、"SbdefMarkerEmitter"</param>
        /// <param name="ex">捕获的异常</param>
        public static void LogPhaseError(string assetPath, string phase, System.Exception ex)
        {
            var fileName = System.IO.Path.GetFileName(assetPath);
            var line     = ExtractLine(ex.Message);
            var location = line.HasValue ? $"({line.Value})" : "";

            UnityEngine.Debug.LogError(
                $"[.sbdef] {fileName}{location} [{phase}]: {ex.Message}\n" +
                $"  → {assetPath}\n{ex.StackTrace}");
        }

        /// <summary>从异常消息中提取行号（匹配 "（行 N）" 或 "行 N" 模式）</summary>
        private static int? ExtractLine(string message)
        {
            var m = Regex.Match(message, @"[（\(]?行\s+(\d+)[）\)]?");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var line))
                return line;
            return null;
        }
    }
}
