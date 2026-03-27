#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace SceneBlueprint.Editor.CodeGen.Sbdef
{
    /// <summary>
    /// .sbdef 代码生成入口 — 通过外部 CLI 工具 sbdef-gen 生成 C# 代码。
    /// <para>
    /// 扫描 Assets/ 下所有 .sbdef 文件，调用 CLI 生成 .cs 到
    /// 与 .sbdef 文件同级的 <c>Generated/</c> 目录（用户项目内）。
    /// 例：Definitions/vfx.sbdef → Generated/UAT.Vfx.cs
    /// </para>
    /// <para>
    /// 触发方式：
    /// <list type="bullet">
    /// <item>菜单：SceneBlueprint → 重新生成 .sbdef 代码</item>
    /// <item>自动：.sbdef 文件保存/导入时由 <see cref="SbdefAssetImporter"/> 触发</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class SbdefCodeGen
    {
        // ── 公开入口 ─────────────────────────────────────────────

        [MenuItem("SceneBlueprint/重新生成 .sbdef 代码", priority = 100)]
        public static void RegenerateFromMenu()
        {
            int written = Run(verbose: true);
            if (written > 0)
                UnityEngine.Debug.Log($"[SbdefCodeGen] ✓ 重新生成完成，共写入 {written} 个文件");
            else if (written == 0)
                UnityEngine.Debug.Log("[SbdefCodeGen] 所有文件内容均无变化，无需写入");
            else
                UnityEngine.Debug.LogWarning("[SbdefCodeGen] ⚠ 生成失败，请检查 Console 输出");
        }

        /// <summary>
        /// 运行 sbdef-gen CLI 生成代码，返回写入文件数。
        /// 找不到已编译的 CLI 时会自动尝试 dotnet build。
        /// 失败时返回 -1。
        /// </summary>
        public static int Run(bool verbose = false)
        {
            // 1. 计算输入/输出路径
            var (inputDir, outputDir) = ResolvePaths();
            if (inputDir == null || outputDir == null)
            {
                if (verbose) UnityEngine.Debug.Log("[SbdefCodeGen] 未找到 Definitions 目录，跳过。");
                return 0;
            }

            // 2. 查找 CLI 工具（已编译的 exe）
            var cliPath = FindCliTool();

            // 3. 找不到 exe → 自动 dotnet build
            if (cliPath == null)
            {
                var projectDir = FindSbdefGenProjectDir();
                if (projectDir == null)
                {
                    UnityEngine.Debug.LogError(
                        "[SbdefCodeGen] 未找到 SbdefGen 项目目录。\n" +
                        "请确认 package 中包含 Tools~/SbdefGen/ 目录。");
                    return -1;
                }

                if (verbose)
                    UnityEngine.Debug.Log($"[SbdefCodeGen] 首次运行，正在自动构建 CLI 工具...");

                if (!AutoBuild(projectDir, verbose))
                    return -1;

                cliPath = FindCliTool();
                if (cliPath == null)
                {
                    UnityEngine.Debug.LogError("[SbdefCodeGen] 自动构建完成但仍未找到 sbdef-gen.exe");
                    return -1;
                }
            }

            if (verbose)
                UnityEngine.Debug.Log($"[SbdefCodeGen] CLI: {cliPath}");

            // 4. 调用 CLI
            return RunViaCli(cliPath, inputDir, outputDir, verbose);
        }

        // ── CLI 调用 ─────────────────────────────────────────────

        private static int RunViaCli(string cliPath, string inputDir, string outputDir, bool verbose)
        {
            try
            {
                var cliArgs = $"--input \"{inputDir}\" --output \"{outputDir}\"";
                if (verbose) cliArgs += " --verbose";

                var psi = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = cliArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    UnityEngine.Debug.LogError("[SbdefCodeGen] 无法启动 CLI 进程");
                    return -1;
                }

                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(30_000);

                if (verbose && !string.IsNullOrWhiteSpace(stdout))
                    UnityEngine.Debug.Log($"[SbdefCodeGen CLI]\n{stdout.TrimEnd()}");
                if (!string.IsNullOrWhiteSpace(stderr))
                    UnityEngine.Debug.LogWarning($"[SbdefCodeGen CLI stderr]\n{stderr.TrimEnd()}");

                if (proc.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"[SbdefCodeGen] CLI 退出码: {proc.ExitCode}");
                    return -1;
                }

                var written = ParseWrittenCount(stdout);
                if (written > 0)
                    AssetDatabase.Refresh();

                return written;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[SbdefCodeGen] CLI 调用异常: {e.Message}");
                return -1;
            }
        }

        // ── 辅助方法 ─────────────────────────────────────────────

        /// <summary>从 CLI stdout 解析写入文件数</summary>
        private static int ParseWrittenCount(string stdout)
        {
            var match = System.Text.RegularExpressions.Regex.Match(stdout, @"共写入\s+(\d+)\s+个文件");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                return count;
            return 0;
        }

        /// <summary>搜索已编译的 sbdef-gen CLI 工具路径（按优先级）</summary>
        private static string? FindCliTool()
        {
            var projectDir = FindSbdefGenProjectDir();
            return projectDir == null ? null : FindCliToolInProject(projectDir);
        }

        private static string? FindCliToolInProject(string projectDir)
        {
            foreach (var candidate in EnumerateCliCandidates(projectDir))
                if (File.Exists(candidate))
                    return candidate;

            return null;
        }

        private static string[] EnumerateCliCandidates(string projectDir)
        {
            return new[]
            {
                Path.Combine(projectDir, "bin", "Debug", "net8.0", "sbdef-gen.exe"),
                Path.Combine(projectDir, "bin", "Release", "net8.0", "sbdef-gen.exe"),
                Path.Combine(projectDir, "bin", "sbdef-gen.exe"),
            };
        }

        /// <summary>查找当前工程应使用的 SbdefGen .csproj 所在目录（用于定位 CLI 与自动构建）</summary>
        private static string? FindSbdefGenProjectDir()
        {
            // 优先：package 内 Tools~/SbdefGen/
            var pkgDir = FindPackageToolsDir();
            if (pkgDir != null && File.Exists(Path.Combine(pkgDir, "SbdefGen.csproj")))
                return pkgDir;

            // 兼容：仓库根目录 Tools/SbdefGen/
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var repoDir = Path.Combine(Path.GetFullPath(Path.Combine(projectRoot, "..")), "Tools", "SbdefGen");
            if (File.Exists(Path.Combine(repoDir, "SbdefGen.csproj")))
                return repoDir;

            return null;
        }

        /// <summary>定位 package 中 Tools~/SbdefGen/ 的绝对路径</summary>
        private static string? FindPackageToolsDir()
        {
            // 通过 package.json 定位 package 根目录
            var packageJsonPath = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..",
                    "Packages", "com.zgx197.sceneblueprint", "package.json"));

            if (!File.Exists(packageJsonPath))
                return null;

            var packageRoot = Path.GetDirectoryName(packageJsonPath)!;
            var toolsDir = Path.Combine(packageRoot, "Tools~", "SbdefGen");
            return Directory.Exists(toolsDir) ? toolsDir : null;
        }

        /// <summary>
        /// 自动执行 dotnet build 构建 sbdef-gen CLI 工具。
        /// 成功返回 true，失败返回 false。
        /// </summary>
        private static bool AutoBuild(string sbdefGenDir, bool verbose)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build --nologo -c Debug",
                    WorkingDirectory = sbdefGenDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                UnityEngine.Debug.Log($"[SbdefCodeGen] 正在构建 CLI 工具: dotnet build (in {sbdefGenDir})");

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    UnityEngine.Debug.LogError("[SbdefCodeGen] 无法启动 dotnet 进程。请确认已安装 .NET 8 SDK。");
                    return false;
                }

                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(60_000);

                if (proc.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError(
                        $"[SbdefCodeGen] dotnet build 失败 (exit {proc.ExitCode}):\n{stderr}\n{stdout}");
                    return false;
                }

                UnityEngine.Debug.Log("[SbdefCodeGen] ✓ CLI 工具构建成功");
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(
                    $"[SbdefCodeGen] 自动构建异常: {e.Message}\n" +
                    "请确认已安装 .NET 8 SDK，或手动执行:\n" +
                    $"  cd \"{sbdefGenDir}\" && dotnet build");
                return false;
            }
        }

        /// <summary>计算 Definitions/ 和 Generated/ 目录的绝对路径</summary>
        private static (string? inputDir, string? outputDir) ResolvePaths()
        {
            var guids = AssetDatabase.FindAssets("t:SbdefAsset");
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                             .Concat(FindSbdefPathsFallback())
                             .Distinct()
                             .Where(p => p.EndsWith(".sbdef", StringComparison.OrdinalIgnoreCase))
                             .ToList();

            if (paths.Count == 0) return (null, null);

            var firstPath = paths[0];
            var sbdefDir  = Path.GetDirectoryName(firstPath)!;
            var parentDir = Path.GetDirectoryName(sbdefDir) ?? sbdefDir;

            var projectRoot  = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var inputAbsDir  = Path.GetFullPath(Path.Combine(projectRoot, sbdefDir));
            var outputAbsDir = Path.GetFullPath(Path.Combine(projectRoot, parentDir, "Generated"));

            return (inputAbsDir, outputAbsDir);
        }

        private static System.Collections.Generic.IEnumerable<string> FindSbdefPathsFallback()
        {
            var all = AssetDatabase.GetAllAssetPaths();
            foreach (var p in all)
                if (p.EndsWith(".sbdef", StringComparison.OrdinalIgnoreCase))
                    yield return p;
        }
    }
}
