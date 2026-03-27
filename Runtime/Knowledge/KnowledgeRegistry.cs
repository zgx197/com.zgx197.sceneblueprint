#nullable enable
using System.Collections.Generic;
using System.Linq;
using SceneBlueprint.Contract.Knowledge;

namespace SceneBlueprint.Runtime.Knowledge
{
    /// <summary>
    /// 知识库合并注册表。
    /// 加载 KnowledgeManifest，提供按层级、标签检索知识文档的能力。
    /// </summary>
    public class KnowledgeRegistry
    {
        private KnowledgeManifest? _manifest;
        private readonly List<KnowledgeDocRef> _allDocs = new();

        // ══════════════════════════════════════
        //  初始化
        // ══════════════════════════════════════

        /// <summary>
        /// 绑定 KnowledgeManifest 并建立内部索引。
        /// </summary>
        public void Initialize(KnowledgeManifest manifest)
        {
            _manifest = manifest;
            _allDocs.Clear();
            if (manifest == null) return;

            // 收集所有文档引用
            AddIfValid(manifest.CoreConcepts);
            AddIfValid(manifest.Definitions);
            AddIfValid(manifest.Architecture);
            AddIfValid(manifest.CoreLogic);
            AddIfValid(manifest.Decisions);
            AddIfValid(manifest.Workflow);
            if (manifest.ActionGuides != null)
            {
                foreach (var guide in manifest.ActionGuides)
                    AddIfValid(guide);
            }
            AddIfValid(manifest.MarkerGuide);
            AddIfValid(manifest.FAQ);
        }

        private void AddIfValid(KnowledgeDocRef? docRef)
        {
            if (docRef != null && !string.IsNullOrEmpty(docRef.Entry.Title))
                _allDocs.Add(docRef);
        }

        // ══════════════════════════════════════
        //  查询接口
        // ══════════════════════════════════════

        /// <summary>
        /// 获取指定层级的所有文档引用。
        /// </summary>
        public IReadOnlyList<KnowledgeDocRef> GetByLayer(KnowledgeLayer layer)
        {
            return _allDocs.Where(d => d.Entry.Layer == layer).ToList();
        }

        /// <summary>
        /// 获取指定角色可访问的所有文档引用。
        /// </summary>
        public IReadOnlyList<KnowledgeDocRef> GetByRole(PromptRole role)
        {
            var allowedLayers = GetAllowedLayers(role);
            return _allDocs.Where(d => allowedLayers.Contains(d.Entry.Layer)).ToList();
        }

        /// <summary>
        /// 按关键词搜索知识文档。
        /// 返回按匹配度排序的结果（Tags 匹配 + 标题匹配）。
        /// </summary>
        /// <param name="query">搜索关键词</param>
        /// <param name="role">角色（限制搜索范围）</param>
        /// <param name="maxResults">最大返回数量</param>
        public List<KnowledgeSearchResult> Search(string query, PromptRole role, int maxResults = 3)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<KnowledgeSearchResult>();

            var allowedLayers = GetAllowedLayers(role);
            var candidates = _allDocs.Where(d => allowedLayers.Contains(d.Entry.Layer));
            var queryLower = query.ToLowerInvariant();
            var queryTerms = queryLower.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            var results = new List<KnowledgeSearchResult>();

            foreach (var doc in candidates)
            {
                int score = 0;

                // 标题匹配
                if (!string.IsNullOrEmpty(doc.Entry.Title))
                {
                    var titleLower = doc.Entry.Title.ToLowerInvariant();
                    foreach (var term in queryTerms)
                    {
                        if (titleLower.Contains(term)) score += 3;
                    }
                }

                // 描述匹配
                if (!string.IsNullOrEmpty(doc.Entry.Description))
                {
                    var descLower = doc.Entry.Description.ToLowerInvariant();
                    foreach (var term in queryTerms)
                    {
                        if (descLower.Contains(term)) score += 2;
                    }
                }

                // Tag 精确匹配（权重最高）
                if (doc.Entry.Tags != null)
                {
                    foreach (var tag in doc.Entry.Tags)
                    {
                        var tagLower = tag.ToLowerInvariant();
                        if (tagLower == queryLower) { score += 10; continue; }
                        foreach (var term in queryTerms)
                        {
                            if (tagLower.Contains(term)) score += 5;
                        }
                    }
                }

                if (score > 0)
                {
                    results.Add(new KnowledgeSearchResult
                    {
                        DocRef = doc,
                        Score = score
                    });
                }
            }

            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (results.Count > maxResults)
                results.RemoveRange(maxResults, results.Count - maxResults);
            return results;
        }

        /// <summary>
        /// 按 Action TypeId 精确匹配节点文档（P1 层）。
        /// </summary>
        public KnowledgeDocRef? FindActionDoc(string actionTypeId)
        {
            if (string.IsNullOrEmpty(actionTypeId)) return null;

            return _allDocs.FirstOrDefault(d =>
                d.Entry.Layer == KnowledgeLayer.P1_ActionGuide &&
                d.Entry.Tags != null &&
                d.Entry.Tags.Any(t => string.Equals(t, actionTypeId, System.StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// 按 Marker TypeId 精确匹配 Marker 文档（P2 层）。
        /// </summary>
        public KnowledgeDocRef? FindMarkerDoc(string markerTypeId)
        {
            if (string.IsNullOrEmpty(markerTypeId)) return null;

            return _allDocs.FirstOrDefault(d =>
                d.Entry.Layer == KnowledgeLayer.P2_MarkerGuide &&
                d.Entry.Tags != null &&
                d.Entry.Tags.Any(t => string.Equals(t, markerTypeId, System.StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// 按关键词搜索所有知识文档（不限角色/层级）。
        /// </summary>
        public List<KnowledgeSearchResult> SearchAll(string query, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<KnowledgeSearchResult>();

            var queryLower = query.ToLowerInvariant();
            var queryTerms = queryLower.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            var results = new List<KnowledgeSearchResult>();

            foreach (var doc in _allDocs)
            {
                int score = 0;

                // 标题匹配
                if (!string.IsNullOrEmpty(doc.Entry.Title))
                {
                    var titleLower = doc.Entry.Title.ToLowerInvariant();
                    foreach (var term in queryTerms)
                    {
                        if (titleLower.Contains(term)) score += 3;
                    }
                }

                // 描述匹配
                if (!string.IsNullOrEmpty(doc.Entry.Description))
                {
                    var descLower = doc.Entry.Description.ToLowerInvariant();
                    foreach (var term in queryTerms)
                    {
                        if (descLower.Contains(term)) score += 2;
                    }
                }

                // Tag 精确匹配（权重最高）
                if (doc.Entry.Tags != null)
                {
                    foreach (var tag in doc.Entry.Tags)
                    {
                        var tagLower = tag.ToLowerInvariant();
                        if (tagLower == queryLower) { score += 10; continue; }
                        foreach (var term in queryTerms)
                        {
                            if (tagLower.Contains(term)) score += 5;
                        }
                    }
                }

                if (score > 0)
                {
                    results.Add(new KnowledgeSearchResult
                    {
                        DocRef = doc,
                        Score = score
                    });
                }
            }

            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (results.Count > maxResults)
                results.RemoveRange(maxResults, results.Count - maxResults);
            return results;
        }

        /// <summary>
        /// 获取所有已注册的文档引用（只读）。
        /// </summary>
        public IReadOnlyList<KnowledgeDocRef> AllDocs => _allDocs;

        /// <summary>
        /// 加载 Prompt 配置。
        /// </summary>
        public PromptConfigData? LoadPromptConfig()
        {
            return _manifest?.LoadPromptConfig();
        }

        // ══════════════════════════════════════
        //  内部工具
        // ══════════════════════════════════════

        private static HashSet<KnowledgeLayer> GetAllowedLayers(PromptRole role)
        {
            return role switch
            {
                PromptRole.Developer => new HashSet<KnowledgeLayer>
                {
                    KnowledgeLayer.S0_CoreConcepts,
                    KnowledgeLayer.S1_Definitions,
                    KnowledgeLayer.D0_Architecture,
                    KnowledgeLayer.D1_CoreLogic,
                    KnowledgeLayer.D2_Decisions,
                },
                PromptRole.Designer => new HashSet<KnowledgeLayer>
                {
                    KnowledgeLayer.S0_CoreConcepts,
                    KnowledgeLayer.S1_Definitions,
                    KnowledgeLayer.P0_Workflow,
                    KnowledgeLayer.P1_ActionGuide,
                    KnowledgeLayer.P2_MarkerGuide,
                    KnowledgeLayer.P3_FAQ,
                },
                _ => new HashSet<KnowledgeLayer>(),
            };
        }
    }

    /// <summary>
    /// 知识检索结果。
    /// </summary>
    public class KnowledgeSearchResult
    {
        public KnowledgeDocRef DocRef = null!;
        public int Score;
    }
}
