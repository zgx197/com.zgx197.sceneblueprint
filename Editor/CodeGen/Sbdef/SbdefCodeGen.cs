#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace SceneBlueprint.Editor.CodeGen.Sbdef
{
    /// <summary>
    /// .sbdef 代码生成管道入口。
    /// <para>
    /// 扫描 Assets/ 下所有 .sbdef 文件，解析后将生成的 .g.cs 写入
    /// 与 .sbdef 文件同级的 <c>Generated/</c> 目录（用户项目内）。
    /// 例：Definitions/vfx.sbdef → Generated/UAT.Vfx.g.cs
    /// </para>
    /// </summary>
    public static class SbdefCodeGen
    {
        // ── 公开入口（供 ScriptedImporter 和菜单调用）────────────

        [MenuItem("SceneBlueprint/生成 .sbdef 常量 (Codegen)")]
        public static void RunFromMenu() => Run(verbose: true);

        /// <summary>扫描所有 .sbdef 并生成 .g.cs，返回写入文件数。</summary>
        public static int Run(bool verbose = false)
        {
            // 查找 Assets/ 下所有 .sbdef 文件
            var guids = AssetDatabase.FindAssets("t:SbdefAsset");
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                             .Concat(FindSbdefPathsFallback())
                             .Distinct()
                             .Where(p => p.EndsWith(".sbdef", StringComparison.OrdinalIgnoreCase))
                             .ToList();

            if (paths.Count == 0)
            {
                if (verbose) UnityEngine.Debug.Log("[SbdefCodeGen] 未找到任何 .sbdef 文件。");
                return 0;
            }

            int written = 0;
            foreach (var assetPath in paths)
            {
                try
                {
                    written += ProcessFile(assetPath, verbose);
                }
                catch (Exception e)
                {
                    SbdefDiagnostic.LogException(assetPath, e);
                }
            }

            if (written > 0)
            {
                AssetDatabase.Refresh();
                if (verbose) UnityEngine.Debug.Log($"[SbdefCodeGen] 完成，共写入 {written} 个 .g.cs 文件。");
            }
            else if (verbose)
            {
                UnityEngine.Debug.Log("[SbdefCodeGen] 所有生成文件均已是最新，无需写入。");
            }

            return written;
        }

        // ── 内部实现 ─────────────────────────────────────────────

        private static int ProcessFile(string assetPath, bool verbose)
        {
            var fullPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", assetPath));
            var source   = File.ReadAllText(fullPath, Encoding.UTF8);
            var name     = Path.GetFileNameWithoutExtension(assetPath); // e.g. "vfx"

            // 输出到 .sbdef 文件的同级 Generated/ 目录
            // 例: Assets/Extensions/SceneBlueprintUser/Definitions/vfx.sbdef
            //      → Assets/Extensions/SceneBlueprintUser/Generated/
            var sbdefDir  = Path.GetDirectoryName(assetPath)!;     // Definitions/
            var parentDir = Path.GetDirectoryName(sbdefDir) ?? sbdefDir; // SceneBlueprintUser/
            var outputAssetDir = parentDir + "/Generated";
            var outputAbsDir   = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", outputAssetDir));

            if (!Directory.Exists(outputAbsDir))
                Directory.CreateDirectory(outputAbsDir);

            var tokens  = SbdefLexer.Tokenize(source);
            var ast     = SbdefParser.Parse(tokens);

            // v0.1: UAT.*.g.cs（类型 ID 常量）
            var outputs = SbdefActionEmitter.Emit(ast, name);

            // v0.2: UActionPortIds.*.g.cs + ActionDefs.*.g.cs
            foreach (var kv in SbdefDefEmitter.Emit(ast, name))
                outputs[kv.Key] = kv.Value;

            // v0.3: UMarkerTypeIds.g.cs + UMarkers.g.cs + Editor/UMarkerDefs.g.cs
            foreach (var kv in SbdefMarkerEmitter.Emit(ast, name))
                outputs[kv.Key] = kv.Value;

            // 确保 Editor 子目录存在
            var editorAbsDir = Path.Combine(outputAbsDir, "Editor");

            int written = 0;
            foreach (var (fileName, content) in outputs)
            {
                // 键以 "Editor/" 开头的文件输出到 Generated/Editor/ 子目录
                string dest;
                if (fileName.StartsWith("Editor/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Directory.Exists(editorAbsDir))
                        Directory.CreateDirectory(editorAbsDir);
                    dest = Path.Combine(editorAbsDir, fileName.Substring("Editor/".Length));
                }
                else
                {
                    dest = Path.Combine(outputAbsDir, fileName);
                }

                // 仅在内容变化时写入，避免触发不必要的重编译
                if (File.Exists(dest) && File.ReadAllText(dest, Encoding.UTF8) == content)
                    continue;

                File.WriteAllText(dest, content, Encoding.UTF8);
                written++;
                if (verbose) UnityEngine.Debug.Log($"[SbdefCodeGen] 写入 {dest}");
            }
            return written;
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
