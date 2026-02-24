#nullable enable
using System;
using System.Collections.Generic;
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

        [MenuItem("SceneBlueprint/重新生成 .sbdef 代码", priority = 100)]
        public static void RegenerateFromMenu()
        {
            UnityEngine.Debug.Log("[SbdefCodeGen] 开始重新生成...");
            
            // 1. 清除旧文件（保留空桩）
            int cleaned = CleanGeneratedFiles(verbose: false);
            UnityEngine.Debug.Log($"[SbdefCodeGen] 已清除 {cleaned} 个旧文件");
            
            // 2. 刷新 AssetDatabase
            AssetDatabase.Refresh();
            
            // 3. 立即重新生成
            int written = Run(verbose: true);
            
            if (written > 0)
            {
                UnityEngine.Debug.Log($"[SbdefCodeGen] ✓ 重新生成完成，共写入 {written} 个文件");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[SbdefCodeGen] ⚠ 未生成任何文件，请检查 .sbdef 文件是否存在语法错误");
            }
        }

        [MenuItem("SceneBlueprint/清除 .sbdef 生成代码", priority = 101)]
        public static void CleanGeneratedFilesFromMenu()
        {
            if (EditorUtility.DisplayDialog(
                "清除 .sbdef 生成代码",
                "确定要删除所有由 .sbdef 自动生成的代码文件（.g.cs）吗？\n\n注意：空桩文件（_Stub.g.cs）会被保留以避免编译失败。\n\n建议使用【重新生成】按钮代替。",
                "确定",
                "取消"))
            {
                CleanGeneratedFiles(verbose: true);
            }
        }

        /// <summary>扫描所有 .sbdef 并生成 .g.cs，返回写入文件数。</summary>
        public static int Run(bool verbose = false)
        {
            // 0. 首先确保空桩文件存在（防止清除后编译失败）
            EnsureStubFileExists(verbose);
            
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

            if (verbose) UnityEngine.Debug.Log($"[SbdefCodeGen] 找到 {paths.Count} 个 .sbdef 文件，开始处理...");

            int written = 0, failed = 0;
            foreach (var assetPath in paths)
            {
                if (verbose) UnityEngine.Debug.Log($"[SbdefCodeGen] → 处理: {assetPath}");
                try
                {
                    written += ProcessFile(assetPath, verbose);
                }
                catch (Exception e)
                {
                    SbdefDiagnostic.LogException(assetPath, e);
                    failed++;
                }
            }

            if (written > 0)
                AssetDatabase.Refresh();

            if (failed > 0)
                UnityEngine.Debug.LogWarning($"[SbdefCodeGen] 完成 — {paths.Count} 个文件，写入 {written} 个，失败 {failed} 个（详见上方错误）");
            else if (verbose)
                UnityEngine.Debug.Log($"[SbdefCodeGen] 完成 — {paths.Count} 个文件，写入 {written} 个，全部成功");

            return written;
        }

        /// <summary>清除所有由 .sbdef 生成的 .g.cs 文件，返回删除文件数。</summary>
        public static int CleanGeneratedFiles(bool verbose = false)
        {
            // 查找 Assets/ 下所有 .sbdef 文件
            var guids = AssetDatabase.FindAssets("t:SbdefAsset");
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                             .Concat(FindSbdefPathsFallback())
                             .Distinct()
                             .Where(p => p.EndsWith(".sbdef", StringComparison.OrdinalIgnoreCase))
                             .ToList();

            int deleted = 0;
            var deletedDirs = new System.Collections.Generic.HashSet<string>();

            foreach (var assetPath in paths)
            {
                // 计算对应的 Generated/ 目录
                var sbdefDir  = Path.GetDirectoryName(assetPath)!;
                var parentDir = Path.GetDirectoryName(sbdefDir) ?? sbdefDir;
                var generatedAssetDir = parentDir + "/Generated";
                var generatedAbsDir = Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..", generatedAssetDir));

                if (!Directory.Exists(generatedAbsDir))
                    continue;

                // 删除 Generated/ 目录下所有生成文件（含 Annotations/ 子目录）
                deleted += DeleteGeneratedFilesInDir(generatedAbsDir, verbose);
                deletedDirs.Add(generatedAbsDir);
            }

            if (deleted > 0)
            {
                AssetDatabase.Refresh();
                if (verbose) UnityEngine.Debug.Log($"[SbdefCodeGen] 已删除 {deleted} 个生成文件。");
            }
            else if (verbose)
            {
                UnityEngine.Debug.Log("[SbdefCodeGen] 未找到需要删除的生成文件。");
            }

            return deleted;
        }

        private static int DeleteGeneratedFilesInDir(string directory, bool verbose)
        {
            int deleted = 0;
            
            // Generated/ 目录下所有 .cs 文件均为生成文件（按目录识别，而非按后缀）
            var generatedFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            
            // 同时检查 asmdef 文件（不删除）
            var asmdefFiles = Directory.GetFiles(directory, "*.asmdef", SearchOption.AllDirectories);

            foreach (var file in generatedFiles)
            {
                // 保留空桩文件（Core/_Stub.g.cs），避免删除后编译失败
                if (file.Contains("Core" + Path.DirectorySeparatorChar + "_Stub.g.cs") ||
                    file.Contains("Core" + Path.AltDirectorySeparatorChar + "_Stub.g.cs"))
                {
                    if (verbose)
                        UnityEngine.Debug.Log($"[SbdefCodeGen] 保留空桩文件: {file}");
                    continue;
                }

                try
                {
                    File.Delete(file);
                    deleted++;
                    
                    // 同时删除 .meta 文件
                    var metaFile = file + ".meta";
                    if (File.Exists(metaFile))
                    {
                        File.Delete(metaFile);
                        deleted++;
                    }

                    if (verbose)
                        UnityEngine.Debug.Log($"[SbdefCodeGen] 已删除: {file}");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[SbdefCodeGen] 删除文件失败: {file}\n{e}");
                }
            }

            // 不删除 asmdef 文件，仅记录
            if (verbose && asmdefFiles.Length > 0)
            {
                UnityEngine.Debug.Log($"[SbdefCodeGen] 保留程序集定义文件: {asmdefFiles.Length} 个");
            }

            return deleted;
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

            // ── 词法分析 ──────────────────────────────────────────
            List<Token> tokens;
            try
            {
                tokens = SbdefLexer.Tokenize(source);
            }
            catch (Exception e)
            {
                SbdefDiagnostic.LogPhaseError(assetPath, "词法分析", e);
                return 0;
            }

            // ── 语法解析 ──────────────────────────────────────────
            SbdefFile ast;
            try
            {
                ast = SbdefParser.Parse(tokens);
            }
            catch (Exception e)
            {
                SbdefDiagnostic.LogPhaseError(assetPath, "语法解析", e);
                return 0;
            }

            if (verbose)
            {
                var markerCount     = ast.Statements.OfType<MarkerDecl>().Count();
                var annotationCount = ast.Statements.OfType<AnnotationDecl>().Count();
                var actionCount     = ast.Statements.OfType<ActionDecl>().Count();
                UnityEngine.Debug.Log(
                    $"[SbdefCodeGen]   AST: {name}.sbdef → {markerCount} Marker, {annotationCount} Annotation, {actionCount} Action");
            }

            // v0.1: UAT.*.g.cs（类型 ID 常量）
            var outputs = SbdefActionEmitter.Emit(ast, name);

            // v0.2: UActionPortIds.*.g.cs + ActionDefs.*.g.cs
            TryMerge(outputs, () => SbdefDefEmitter.Emit(ast, name), assetPath, "SbdefDefEmitter", verbose);

            // v0.3: UMarkerTypeIds.cs + Markers/{Name}Marker.cs + Editor/UMarkerDefs.cs
            TryMerge(outputs, () => SbdefMarkerEmitter.Emit(ast, name), assetPath, "SbdefMarkerEmitter", verbose);

            // v0.4: Annotations/*.g.cs + Editor/AnnotationDefs.*.g.cs
            TryMerge(outputs, () => SbdefAnnotationEmitter.Emit(ast, name), assetPath, "SbdefAnnotationEmitter", verbose);

            // v0.4: Editor/EditorTools/*.g.cs
            TryMerge(outputs, () => SbdefEditorToolEmitter.Emit(ast, name), assetPath, "SbdefEditorToolEmitter", verbose);

            // ── 文件写入 ──────────────────────────────────────────
            int written = 0;
            foreach (var (fileName, content) in outputs)
            {
                var dest = Path.Combine(outputAbsDir, fileName);

                // 确保目标目录存在（包括所有子目录：Annotations/、Editor/、Editor/EditorTools/ 等）
                var destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                // 仅在内容变化时写入，避免触发不必要的重编译
                if (File.Exists(dest) && File.ReadAllText(dest, Encoding.UTF8) == content)
                {
                    if (verbose) UnityEngine.Debug.Log($"[SbdefCodeGen]   跳过（内容相同）: {fileName}");
                    continue;
                }

                try
                {
                    File.WriteAllText(dest, content, Encoding.UTF8);
                    written++;
                    if (verbose) UnityEngine.Debug.Log($"[SbdefCodeGen]   写入: {fileName}");
                }
                catch (Exception e)
                {
                    SbdefDiagnostic.LogPhaseError(assetPath, "文件写入", e);
                }
            }
            return written;
        }

        private static void TryMerge(
            Dictionary<string, string> outputs,
            Func<Dictionary<string, string>> emitter,
            string assetPath,
            string emitterName,
            bool verbose = false)
        {
            try
            {
                var result = emitter();
                if (verbose && result.Count > 0)
                    UnityEngine.Debug.Log($"[SbdefCodeGen]   {emitterName}: {result.Count} 个输出文件");
                foreach (var kv in result)
                    outputs[kv.Key] = kv.Value;
            }
            catch (Exception e)
            {
                SbdefDiagnostic.LogPhaseError(assetPath, emitterName, e);
            }
        }

        private static System.Collections.Generic.IEnumerable<string> FindSbdefPathsFallback()
        {
            var all = AssetDatabase.GetAllAssetPaths();
            foreach (var p in all)
                if (p.EndsWith(".sbdef", StringComparison.OrdinalIgnoreCase))
                    yield return p;
        }

        /// <summary>
        /// 确保空桩文件存在 - 防止清除生成代码后编译失败
        /// </summary>
        private static void EnsureStubFileExists(bool verbose = false)
        {
            // 空桩文件路径（相对于项目根目录）
            const string StubFileRelativePath = "Assets/Extensions/SceneBlueprintUser/Generated/Core/_Stub.g.cs";
            var stubFileFullPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", StubFileRelativePath));

            // 如果文件已存在，无需操作
            if (File.Exists(stubFileFullPath))
            {
                if (verbose)
                    UnityEngine.Debug.Log($"[SbdefCodeGen] 空桩文件已存在: {StubFileRelativePath}");
                return;
            }

            // 确保目录存在
            var stubFileDir = Path.GetDirectoryName(stubFileFullPath);
            if (!string.IsNullOrEmpty(stubFileDir) && !Directory.Exists(stubFileDir))
            {
                Directory.CreateDirectory(stubFileDir);
                if (verbose)
                    UnityEngine.Debug.Log($"[SbdefCodeGen] 创建目录: {stubFileDir}");
            }

            // 创建空桩文件
            var stubContent = @"// <auto-generated>
// 空桩文件 - 确保 SceneBlueprintUser.Generated 程序集始终可编译
// 由 SbdefCodeGen 自动维护，请勿手动修改或删除
// 作用：防止清除生成代码后出现编译失败
// </auto-generated>
#nullable enable

namespace SceneBlueprintUser.Generated
{
    /// <summary>空桩类 - 确保 UAT 命名空间存在</summary>
    /// <remarks>实际的 Action 类型常量由各个 .sbdef 文件生成（如 UAT.Spawn.g.cs）</remarks>
    public static partial class UAT
    {
        // 空类体 - partial 关键字允许其他文件扩展此类
    }

    /// <summary>空桩类 - 确保 UActionPortIds 命名空间存在</summary>
    /// <remarks>实际的端口 ID 由各个 .sbdef 文件生成（如 UActionPortIds.Spawn.g.cs）</remarks>
    public static partial class UActionPortIds
    {
        // 空类体 - partial 关键字允许其他文件扩展此类
    }

    /// <summary>空桩类 - 确保 UMarkerTypeIds 命名空间存在</summary>
    /// <remarks>实际的 Marker 类型 ID 由各个 .sbdef 文件生成（如 UMarkerTypeIds.Markers.g.cs）</remarks>
    public static partial class UMarkerTypeIds
    {
        // 空类体 - partial 关键字允许其他文件扩展此类
    }
}
";

            File.WriteAllText(stubFileFullPath, stubContent, Encoding.UTF8);
            
            if (verbose)
                UnityEngine.Debug.Log($"[SbdefCodeGen] ✓ 已创建空桩文件: {StubFileRelativePath}");
        }
    }
}
