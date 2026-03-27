#nullable enable
using System.Text;
using SceneBlueprint.Runtime.Knowledge;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// 知识检索工具集——供 LLM Function Calling 调用的 6 个知识查询工具。
    /// 通过 KnowledgeService 获取知识库文档、源文件和 sbdef 定义内容。
    /// </summary>
    public static class KnowledgeTools
    {
        /// <summary>
        /// 将所有知识检索工具注册到 ToolExecutor。
        /// </summary>
        public static void RegisterAll(ToolExecutor executor)
        {
            executor.Register(Def_SearchKnowledge(), HandleSearchKnowledge);
            executor.Register(Def_GetActionDoc(), HandleGetActionDoc);
            executor.Register(Def_GetMarkerDoc(), HandleGetMarkerDoc);
            executor.Register(Def_ReadSourceFile(), HandleReadSourceFile);
            executor.Register(Def_ListDefinitions(), HandleListDefinitions);
            executor.Register(Def_GetSbdefContent(), HandleGetSbdefContent);
        }

        // ══════════════════════════════════════
        //  1. search_knowledge — 搜索知识库文档
        // ══════════════════════════════════════

        private static ToolDefinition Def_SearchKnowledge()
        {
            return new ToolDefinition(
                "search_knowledge",
                "搜索知识库文档。按关键词在所有知识文档（核心概念、架构设计、节点手册、Marker手册、FAQ等）中检索，返回最相关的文档摘要和内容片段。",
                new System.Collections.Generic.List<ToolParameter>
                {
                    new ToolParameter("query", "string", "搜索关键词，如\"刷怪 波次\"、\"Marker 区域\"、\"架构设计\"", true),
                    new ToolParameter("top_k", "integer", "返回结果数量上限，默认 3")
                }
            );
        }

        private static string HandleSearchKnowledge(string argsJson)
        {
            var query = SimpleJsonExtract(argsJson, "query");
            if (string.IsNullOrEmpty(query))
                return "{\"error\":\"缺少参数 query\"}";

            int topK = 3;
            var topKStr = SimpleJsonExtract(argsJson, "top_k");
            if (!string.IsNullOrEmpty(topKStr) && int.TryParse(topKStr, out int k) && k > 0)
                topK = System.Math.Min(k, 10);

            var registry = KnowledgeService.Instance.Registry;
            var results = registry.SearchAll(query, topK);

            if (results.Count == 0)
                return "{\"results\":[],\"message\":\"未找到与查询相关的知识文档\"}";

            var sb = new StringBuilder();
            sb.Append("{\"results\":[");
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var r = results[i];
                var entry = r.DocRef.Entry;
                string? content = r.DocRef.ReadContent();

                // 截断过长文档
                if (content != null && content.Length > 1500)
                    content = content.Substring(0, 1500) + "\n...(已截断)";

                sb.Append($"{{\"title\":\"{Esc(entry.Title)}\",\"layer\":\"{entry.Layer}\",\"score\":{r.Score},\"description\":\"{Esc(entry.Description)}\"");
                if (!string.IsNullOrEmpty(content))
                    sb.Append($",\"content\":\"{Esc(content)}\"");
                sb.Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  2. get_action_doc — 获取指定 Action 的完整文档
        // ══════════════════════════════════════

        private static ToolDefinition Def_GetActionDoc()
        {
            return new ToolDefinition(
                "get_action_doc",
                "获取指定 Action 类型的完整使用文档。返回 P1 层节点手册内容，包含使用说明、配置方法、注意事项等。",
                new System.Collections.Generic.List<ToolParameter>
                {
                    new ToolParameter("action_type_id", "string", "Action 类型 ID，如 \"Combat.Spawn\"、\"Trigger.EnterArea\"", true)
                }
            );
        }

        private static string HandleGetActionDoc(string argsJson)
        {
            var typeId = SimpleJsonExtract(argsJson, "action_type_id");
            if (string.IsNullOrEmpty(typeId))
                return "{\"error\":\"缺少参数 action_type_id\"}";

            var registry = KnowledgeService.Instance.Registry;
            var docRef = registry.FindActionDoc(typeId);
            if (docRef == null)
                return $"{{\"error\":\"未找到 Action '{Esc(typeId)}' 的文档\"}}";

            string? content = docRef.ReadContent();
            if (string.IsNullOrEmpty(content))
                return $"{{\"error\":\"Action '{Esc(typeId)}' 文档为空\"}}";

            var sb = new StringBuilder();
            sb.Append($"{{\"actionTypeId\":\"{Esc(typeId)}\",\"title\":\"{Esc(docRef.Entry.Title)}\",");
            sb.Append($"\"content\":\"{Esc(content)}\"}}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  3. get_marker_doc — 获取指定 Marker 的文档
        // ══════════════════════════════════════

        private static ToolDefinition Def_GetMarkerDoc()
        {
            return new ToolDefinition(
                "get_marker_doc",
                "获取指定 Marker 类型的完整使用文档。返回 P2 层 Marker 手册内容，包含用途、配置方法、与节点的绑定关系等。",
                new System.Collections.Generic.List<ToolParameter>
                {
                    new ToolParameter("marker_type_id", "string", "Marker 类型 ID，如 \"Area\"、\"SpawnPoint\"", true)
                }
            );
        }

        private static string HandleGetMarkerDoc(string argsJson)
        {
            var typeId = SimpleJsonExtract(argsJson, "marker_type_id");
            if (string.IsNullOrEmpty(typeId))
                return "{\"error\":\"缺少参数 marker_type_id\"}";

            var registry = KnowledgeService.Instance.Registry;
            var docRef = registry.FindMarkerDoc(typeId);
            if (docRef == null)
                return $"{{\"error\":\"未找到 Marker '{Esc(typeId)}' 的文档\"}}";

            string? content = docRef.ReadContent();
            if (string.IsNullOrEmpty(content))
                return $"{{\"error\":\"Marker '{Esc(typeId)}' 文档为空\"}}";

            var sb = new StringBuilder();
            sb.Append($"{{\"markerTypeId\":\"{Esc(typeId)}\",\"title\":\"{Esc(docRef.Entry.Title)}\",");
            sb.Append($"\"content\":\"{Esc(content)}\"}}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  4. read_source_file — 读取限定范围内的源文件
        // ══════════════════════════════════════

        // 允许读取的目录白名单（相对于 Unity 项目根目录）
        private static readonly string[] AllowedPrefixes = new[]
        {
            "Assets/Extensions/SceneBlueprintUser/",
            "Packages/com.zgx197.sceneblueprint/",
        };

        private static ToolDefinition Def_ReadSourceFile()
        {
            return new ToolDefinition(
                "read_source_file",
                "读取框架层或业务层的源文件内容。仅可访问 SceneBlueprint 相关目录（Assets/Extensions/SceneBlueprintUser/ 和 Packages/com.zgx197.sceneblueprint/）。" +
                "适合查看 .cs 源代码、.sbdef 定义文件、.md 文档等。",
                new System.Collections.Generic.List<ToolParameter>
                {
                    new ToolParameter("path", "string",
                        "文件路径（相对于 Unity 项目根目录），如 \"Packages/com.zgx197.sceneblueprint/Core/ActionDefinition.cs\" 或 \"Assets/Extensions/SceneBlueprintUser/Sbdef/spawn.sbdef\"",
                        true),
                    new ToolParameter("max_lines", "integer", "最大读取行数，默认 200，最大 500")
                }
            );
        }

        private static string HandleReadSourceFile(string argsJson)
        {
            var path = SimpleJsonExtract(argsJson, "path");
            if (string.IsNullOrEmpty(path))
                return "{\"error\":\"缺少参数 path\"}";

            // 规范化路径分隔符
            path = path.Replace("\\", "/");

            // 安全检查：防止路径遍历
            if (path.Contains(".."))
                return "{\"error\":\"路径不允许包含 '..' \"}";

            // 白名单检查
            bool allowed = false;
            foreach (var prefix in AllowedPrefixes)
            {
                if (path.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed)
                return $"{{\"error\":\"路径不在允许范围内。仅可访问: {string.Join(", ", AllowedPrefixes)}\"}}";

            // 解析 max_lines
            int maxLines = 200;
            var maxLinesStr = SimpleJsonExtract(argsJson, "max_lines");
            if (!string.IsNullOrEmpty(maxLinesStr) && int.TryParse(maxLinesStr, out int ml) && ml > 0)
                maxLines = System.Math.Min(ml, 500);

            // 构建绝对路径（Unity 项目根 = Application.dataPath 的父目录）
            string projectRoot = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath)!;
            string absolutePath = System.IO.Path.Combine(projectRoot, path.Replace("/", System.IO.Path.DirectorySeparatorChar.ToString()));

            if (!System.IO.File.Exists(absolutePath))
                return $"{{\"error\":\"文件不存在: {Esc(path)}\"}}";

            try
            {
                var lines = System.IO.File.ReadAllLines(absolutePath);
                int totalLines = lines.Length;
                int readLines = System.Math.Min(totalLines, maxLines);
                bool truncated = readLines < totalLines;

                var sb = new StringBuilder();
                sb.Append($"{{\"path\":\"{Esc(path)}\",\"totalLines\":{totalLines},\"readLines\":{readLines},\"truncated\":{BoolStr(truncated)},\"content\":\"");
                for (int i = 0; i < readLines; i++)
                {
                    if (i > 0) sb.Append("\\n");
                    sb.Append(Esc(lines[i]));
                }
                if (truncated)
                    sb.Append($"\\n...(已截断，共 {totalLines} 行)");
                sb.Append("\"}");
                return sb.ToString();
            }
            catch (System.Exception ex)
            {
                return $"{{\"error\":\"读取文件失败: {Esc(ex.Message)}\"}}";
            }
        }

        // ══════════════════════════════════════
        //  5. list_definitions — 列出所有 sbdef 定义的类型
        // ══════════════════════════════════════

        // sbdef 文件目录（相对于项目根）
        private const string SbdefDir = "Assets/Extensions/SceneBlueprintUser/Definitions";

        private static ToolDefinition Def_ListDefinitions()
        {
            return new ToolDefinition(
                "list_definitions",
                "列出项目中所有通过 sbdef DSL 定义的类型。" +
                "返回所有 sbdef 文件名和其中定义的 Action、Marker、Annotation、Enum 类型列表。" +
                "如需查看某个类型的完整定义，请用 get_sbdef_content 工具。"
            );
        }

        private static string HandleListDefinitions(string argsJson)
        {
            string projectRoot = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath)!;
            string absDir = System.IO.Path.Combine(projectRoot, SbdefDir.Replace("/", System.IO.Path.DirectorySeparatorChar.ToString()));

            if (!System.IO.Directory.Exists(absDir))
                return "{\"error\":\"sbdef 目录不存在\"}";

            var sb = new StringBuilder();
            sb.Append("{\"definitions\":[");
            var files = System.IO.Directory.GetFiles(absDir, "*.sbdef");
            System.Array.Sort(files);

            bool first = true;
            foreach (var file in files)
            {
                string fileName = System.IO.Path.GetFileName(file);
                string content = System.IO.File.ReadAllText(file, System.Text.Encoding.UTF8);

                // 提取类型名：匹配 action/marker/annotation/enum 后面的标识符
                var types = new System.Collections.Generic.List<string>();
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//")) continue;

                    string? typeKind = null;
                    if (trimmed.StartsWith("action ")) typeKind = "action";
                    else if (trimmed.StartsWith("marker ")) typeKind = "marker";
                    else if (trimmed.StartsWith("annotation ")) typeKind = "annotation";
                    else if (trimmed.StartsWith("enum ")) typeKind = "enum";

                    if (typeKind != null)
                    {
                        var parts = trimmed.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                            types.Add($"{typeKind} {parts[1]}");
                    }
                }

                if (!first) sb.Append(",");
                first = false;
                sb.Append($"{{\"file\":\"{Esc(fileName)}\",\"types\":[");
                for (int i = 0; i < types.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append($"\"{Esc(types[i])}\"");
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  6. get_sbdef_content — 读取指定 sbdef 文件的完整内容
        // ══════════════════════════════════════

        private static ToolDefinition Def_GetSbdefContent()
        {
            return new ToolDefinition(
                "get_sbdef_content",
                "读取指定 sbdef 定义文件的完整内容。" +
                "sbdef 是 SceneBlueprint 的 DSL 定义文件，包含 Action、Marker、Annotation、Enum 的类型定义。" +
                "可用文件：markers.sbdef, spawn.sbdef, trigger.sbdef, vfx.sbdef, annotations.sbdef, enums.sbdef",
                new System.Collections.Generic.List<ToolParameter>
                {
                    new ToolParameter("file_name", "string",
                        "sbdef 文件名（如 \"markers.sbdef\"、\"spawn.sbdef\"、\"annotations.sbdef\"、\"enums.sbdef\"）",
                        true)
                }
            );
        }

        private static string HandleGetSbdefContent(string argsJson)
        {
            var fileName = SimpleJsonExtract(argsJson, "file_name");
            if (string.IsNullOrEmpty(fileName))
                return "{\"error\":\"缺少参数 file_name\"}";

            // 安全检查
            if (fileName!.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
                return "{\"error\":\"无效的文件名\"}";

            if (!fileName.EndsWith(".sbdef"))
                fileName += ".sbdef";

            string projectRoot = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath)!;
            string absPath = System.IO.Path.Combine(projectRoot,
                SbdefDir.Replace("/", System.IO.Path.DirectorySeparatorChar.ToString()),
                fileName);

            if (!System.IO.File.Exists(absPath))
                return $"{{\"error\":\"sbdef 文件不存在: {Esc(fileName)}\"}}";

            try
            {
                string content = System.IO.File.ReadAllText(absPath, System.Text.Encoding.UTF8);
                return $"{{\"file\":\"{Esc(fileName)}\",\"content\":\"{Esc(content)}\"}}";
            }
            catch (System.Exception ex)
            {
                return $"{{\"error\":\"读取失败: {Esc(ex.Message)}\"}}";
            }
        }

        // ══════════════════════════════════════
        //  工具方法
        // ══════════════════════════════════════

        private static string? SimpleJsonExtract(string json, string key)
        {
            // 尝试匹配字符串值
            string p1 = $"\"{key}\":\"";
            string p2 = $"\"{key}\": \"";
            int idx = json.IndexOf(p1, System.StringComparison.Ordinal);
            if (idx >= 0) idx += p1.Length;
            else
            {
                idx = json.IndexOf(p2, System.StringComparison.Ordinal);
                if (idx >= 0) idx += p2.Length;
            }

            if (idx >= 0)
            {
                var sb = new StringBuilder();
                for (int i = idx; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '\\' && i + 1 < json.Length) { sb.Append(json[++i]); continue; }
                    if (c == '"') break;
                    sb.Append(c);
                }
                return sb.Length > 0 ? sb.ToString() : null;
            }

            // 尝试匹配数字值
            string np1 = $"\"{key}\":";
            string np2 = $"\"{key}\": ";
            idx = json.IndexOf(np1, System.StringComparison.Ordinal);
            if (idx >= 0) idx += np1.Length;
            else
            {
                idx = json.IndexOf(np2, System.StringComparison.Ordinal);
                if (idx >= 0) idx += np2.Length;
            }
            if (idx >= 0)
            {
                // 跳过空格
                while (idx < json.Length && json[idx] == ' ') idx++;
                var sb = new StringBuilder();
                for (int i = idx; i < json.Length; i++)
                {
                    char c = json[i];
                    if (char.IsDigit(c) || c == '-' || c == '.') sb.Append(c);
                    else break;
                }
                return sb.Length > 0 ? sb.ToString() : null;
            }

            return null;
        }

        private static string Esc(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s!.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static string BoolStr(bool b) => b ? "true" : "false";
    }
}
