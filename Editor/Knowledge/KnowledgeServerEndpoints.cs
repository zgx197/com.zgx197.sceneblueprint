#nullable enable
using System.Collections.Generic;
using UnityEngine;
using SceneBlueprint.Contract.Knowledge;
using SceneBlueprint.Runtime.Knowledge;

namespace SceneBlueprint.Editor.Knowledge
{
    /// <summary>
    /// MCP Server 端点路由和处理。
    /// 所有端点返回 JSON 字符串，线程安全（通过 Unity 主线程缓存读取）。
    /// </summary>
    public class KnowledgeServerEndpoints
    {
        private readonly KnowledgeRegistry _registry;
        private readonly IBlueprintContextProvider _contextProvider;

        // 主线程缓存的上下文快照（由 EditorUpdate 定期刷新）
        private string _cachedContextJson = "{}";
        private readonly object _cacheLock = new object();

        public KnowledgeServerEndpoints(KnowledgeRegistry registry, IBlueprintContextProvider contextProvider)
        {
            _registry = registry;
            _contextProvider = contextProvider;
        }

        /// <summary>
        /// 由 EditorApplication.update 在主线程调用，刷新上下文缓存。
        /// </summary>
        public void RefreshContextCache()
        {
            if (!_contextProvider.HasActiveSession) return;

            try
            {
                var ctx = _contextProvider.GetCurrentContext();
                string json = JsonUtility.ToJson(ctx, true);
                lock (_cacheLock) { _cachedContextJson = json; }
            }
            catch { /* 忽略刷新异常 */ }
        }

        // ══════════════════════════════════════
        //  路由
        // ══════════════════════════════════════

        public string HandleRequest(string path, string? requestBody)
        {
            return path switch
            {
                "/"               => HandleRoot(),
                "/health"         => HandleHealth(),
                "/context"        => HandleGetContext(),
                "/search"         => HandleSearch(requestBody),
                "/doc"            => HandleGetDoc(requestBody),
                "/action-doc"     => HandleGetActionDoc(requestBody),
                "/layers"         => HandleListLayers(),
                "/docs"           => HandleListDocs(requestBody),
                "/prompt-config"  => HandleGetPromptConfig(),
                "/assemble"       => HandleAssemble(requestBody),
                _                 => $"{{\"error\":\"Unknown endpoint: {path}\"}}"
            };
        }

        // ══════════════════════════════════════
        //  端点实现
        // ══════════════════════════════════════

        private string HandleRoot()
        {
            return "{\"name\":\"SceneBlueprint Knowledge Server\"," +
                   "\"version\":\"1.0\"," +
                   "\"endpoints\":[\"/health\",\"/context\",\"/search\",\"/doc\",\"/action-doc\"," +
                   "\"/layers\",\"/docs\",\"/prompt-config\",\"/assemble\"]}";
        }

        private string HandleHealth()
        {
            bool hasSession = _contextProvider.HasActiveSession;
            return $"{{\"status\":\"ok\",\"hasActiveSession\":{(hasSession ? "true" : "false")}}}";
        }

        private string HandleGetContext()
        {
            lock (_cacheLock) { return _cachedContextJson; }
        }

        private string HandleSearch(string? body)
        {
            if (string.IsNullOrEmpty(body))
                return "{\"error\":\"Missing request body\"}";

            var req = JsonUtility.FromJson<SearchRequest>(body);
            if (string.IsNullOrEmpty(req.query))
                return "{\"error\":\"Missing 'query' field\"}";

            var role = ParseRole(req.role);
            var results = _registry.Search(req.query, role, req.maxResults > 0 ? req.maxResults : 3);

            var items = new List<string>();
            foreach (var r in results)
            {
                string title = EscapeJson(r.DocRef.Entry.Title);
                string desc = EscapeJson(r.DocRef.Entry.Description);
                string layer = r.DocRef.Entry.Layer.ToString();
                items.Add($"{{\"title\":\"{title}\",\"description\":\"{desc}\"," +
                          $"\"layer\":\"{layer}\",\"score\":{r.Score}}}");
            }
            return $"{{\"results\":[{string.Join(",", items)}]}}";
        }

        private string HandleGetDoc(string? body)
        {
            if (string.IsNullOrEmpty(body))
                return "{\"error\":\"Missing request body\"}";

            var req = JsonUtility.FromJson<DocRequest>(body);
            if (string.IsNullOrEmpty(req.title))
                return "{\"error\":\"Missing 'title' field\"}";

            var role = ParseRole(req.role);
            var docs = _registry.GetByRole(role);
            foreach (var doc in docs)
            {
                if (doc.Entry.Title == req.title)
                {
                    string? content = doc.ReadContent();
                    if (content == null)
                        return "{\"error\":\"Document file not found\"}";
                    return $"{{\"title\":\"{EscapeJson(doc.Entry.Title)}\"," +
                           $"\"content\":\"{EscapeJson(content)}\"}}";
                }
            }
            return "{\"error\":\"Document not found\"}";
        }

        private string HandleGetActionDoc(string? body)
        {
            if (string.IsNullOrEmpty(body))
                return "{\"error\":\"Missing request body\"}";

            var req = JsonUtility.FromJson<ActionDocRequest>(body);
            if (string.IsNullOrEmpty(req.actionTypeId))
                return "{\"error\":\"Missing 'actionTypeId' field\"}";

            var doc = _registry.FindActionDoc(req.actionTypeId);
            if (doc == null)
                return $"{{\"error\":\"No doc found for action '{EscapeJson(req.actionTypeId)}'\"}}";

            string? content = doc.ReadContent();
            if (content == null)
                return "{\"error\":\"Document file not found\"}";

            return $"{{\"title\":\"{EscapeJson(doc.Entry.Title)}\"," +
                   $"\"content\":\"{EscapeJson(content)}\"}}";
        }

        private string HandleListLayers()
        {
            var names = System.Enum.GetNames(typeof(KnowledgeLayer));
            var items = new List<string>();
            foreach (var n in names)
                items.Add($"\"{n}\"");
            return $"{{\"layers\":[{string.Join(",", items)}]}}";
        }

        private string HandleListDocs(string? body)
        {
            var role = PromptRole.Designer;
            if (!string.IsNullOrEmpty(body))
            {
                var req = JsonUtility.FromJson<RoleRequest>(body);
                role = ParseRole(req.role);
            }

            var docs = _registry.GetByRole(role);
            var items = new List<string>();
            foreach (var doc in docs)
            {
                string title = EscapeJson(doc.Entry.Title);
                string desc = EscapeJson(doc.Entry.Description);
                string layer = doc.Entry.Layer.ToString();
                items.Add($"{{\"title\":\"{title}\",\"description\":\"{desc}\",\"layer\":\"{layer}\"}}");
            }
            return $"{{\"docs\":[{string.Join(",", items)}]}}";
        }

        private string HandleGetPromptConfig()
        {
            var config = _registry.LoadPromptConfig();
            if (config == null)
                return "{\"error\":\"Prompt config not loaded\"}";
            return JsonUtility.ToJson(config, true);
        }

        private string HandleAssemble(string? body)
        {
            if (string.IsNullOrEmpty(body))
                return "{\"error\":\"Missing request body\"}";

            var req = JsonUtility.FromJson<AssembleRequest>(body);
            if (string.IsNullOrEmpty(req.question))
                return "{\"error\":\"Missing 'question' field\"}";

            var role = ParseRole(req.role);
            var assembler = new PromptAssembler(_registry, _contextProvider);
            var messages = assembler.Assemble(role, req.question);

            var items = new List<string>();
            foreach (var msg in messages)
            {
                items.Add($"{{\"role\":\"{EscapeJson(msg.Role)}\"," +
                          $"\"content\":\"{EscapeJson(msg.Content)}\"}}");
            }
            return $"{{\"messages\":[{string.Join(",", items)}]}}";
        }

        // ══════════════════════════════════════
        //  辅助
        // ══════════════════════════════════════

        private static PromptRole ParseRole(string? roleStr)
        {
            if (string.IsNullOrEmpty(roleStr)) return PromptRole.Designer;
            return roleStr!.ToLowerInvariant() switch
            {
                "developer" => PromptRole.Developer,
                "dev"       => PromptRole.Developer,
                _           => PromptRole.Designer,
            };
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        // ── 请求 DTO ──

        [System.Serializable]
        private class SearchRequest
        {
            public string query = "";
            public string role = "";
            public int maxResults = 3;
        }

        [System.Serializable]
        private class DocRequest
        {
            public string title = "";
            public string role = "";
        }

        [System.Serializable]
        private class ActionDocRequest
        {
            public string actionTypeId = "";
        }

        [System.Serializable]
        private class RoleRequest
        {
            public string role = "";
        }

        [System.Serializable]
        private class AssembleRequest
        {
            public string question = "";
            public string role = "";
        }
    }
}
