#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using SceneBlueprint.Runtime.Knowledge;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// Embedding 语义检索服务。
    /// 调用 OpenAI 兼容的 Embedding API 对知识文档分块并向量化。
    /// 向量缓存在 Library/SceneBlueprint/embeddings.json（不进版本控制）。
    /// 支持增量更新——文档内容 hash 变化时才重新计算向量。
    /// </summary>
    public class EmbeddingService
    {
        private const string CacheDir = "Library/SceneBlueprint";
        private const string CacheFile = "Library/SceneBlueprint/embeddings.json";
        private const int MaxChunkChars = 800;
        private const int MinChunkChars = 100;

        private EmbeddingCache _cache = new();
        private bool _cacheLoaded;
        private bool _indexing;

        public bool IsIndexing => _indexing;
        public int ChunkCount => _cache.Chunks.Count;

        // ══════════════════════════════════════
        //  缓存管理
        // ══════════════════════════════════════

        /// <summary>加载本地向量缓存。</summary>
        public void LoadCache()
        {
            if (_cacheLoaded) return;
            _cacheLoaded = true;

            string absPath = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, CacheFile);
            if (!File.Exists(absPath)) return;

            try
            {
                string json = File.ReadAllText(absPath);
                _cache = JsonUtility.FromJson<EmbeddingCache>(json) ?? new EmbeddingCache();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[EmbeddingService] 加载缓存失败: {ex.Message}");
                _cache = new EmbeddingCache();
            }
        }

        /// <summary>保存向量缓存到本地。</summary>
        public void SaveCache()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath)!;
            string dirPath = Path.Combine(projectRoot, CacheDir);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            string absPath = Path.Combine(projectRoot, CacheFile);
            string json = JsonUtility.ToJson(_cache, true);
            File.WriteAllText(absPath, json);
        }

        // ══════════════════════════════════════
        //  文档分块
        // ══════════════════════════════════════

        /// <summary>
        /// 将知识文档按 ## 二级标题分块。
        /// 每块 100~800 字符，过长的块按行截断。
        /// </summary>
        public static List<DocChunk> ChunkDocument(KnowledgeDocRef docRef)
        {
            var chunks = new List<DocChunk>();
            string? content = docRef.ReadContent();
            if (string.IsNullOrEmpty(content)) return chunks;

            string docTitle = docRef.Entry.Title;
            string docId = docRef.Entry.Tags?.FirstOrDefault() ?? docTitle;

            // 按 ## 分割
            var sections = SplitBySections(content!);
            foreach (var (heading, body) in sections)
            {
                string text = string.IsNullOrEmpty(heading) ? body : $"## {heading}\n{body}";
                text = text.Trim();
                if (text.Length < MinChunkChars) continue;

                // 过长时按行截断
                if (text.Length > MaxChunkChars)
                {
                    var subChunks = SplitLongText(text, MaxChunkChars);
                    for (int i = 0; i < subChunks.Count; i++)
                    {
                        chunks.Add(new DocChunk
                        {
                            DocId = docId,
                            DocTitle = docTitle,
                            Section = heading ?? "",
                            PartIndex = i,
                            Text = subChunks[i],
                        });
                    }
                }
                else
                {
                    chunks.Add(new DocChunk
                    {
                        DocId = docId,
                        DocTitle = docTitle,
                        Section = heading ?? "",
                        PartIndex = 0,
                        Text = text,
                    });
                }
            }

            return chunks;
        }

        /// <summary>按 ## 标题分割 Markdown。</summary>
        private static List<(string? heading, string body)> SplitBySections(string content)
        {
            var result = new List<(string?, string)>();
            var lines = content.Split('\n');
            string? currentHeading = null;
            var bodyBuilder = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    // 保存前一节
                    if (bodyBuilder.Length > 0 || currentHeading != null)
                    {
                        result.Add((currentHeading, bodyBuilder.ToString()));
                        bodyBuilder.Clear();
                    }
                    currentHeading = line.Substring(3).Trim();
                }
                else
                {
                    bodyBuilder.AppendLine(line);
                }
            }

            // 最后一节
            if (bodyBuilder.Length > 0 || currentHeading != null)
                result.Add((currentHeading, bodyBuilder.ToString()));

            return result;
        }

        /// <summary>将过长文本按行截断为多块。</summary>
        private static List<string> SplitLongText(string text, int maxChars)
        {
            var result = new List<string>();
            var lines = text.Split('\n');
            var sb = new StringBuilder();

            foreach (var line in lines)
            {
                if (sb.Length + line.Length + 1 > maxChars && sb.Length >= MinChunkChars)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                sb.AppendLine(line);
            }
            if (sb.Length > 0)
                result.Add(sb.ToString().Trim());

            return result;
        }

        // ══════════════════════════════════════
        //  增量索引
        // ══════════════════════════════════════

        /// <summary>
        /// 轻量级检测：当前知识库文档是否需要重建索引。
        /// 只做 hash 比对，不调用 API，适合在窗口打开时快速判断。
        /// </summary>
        public bool NeedsReindex(KnowledgeRegistry registry)
        {
            LoadCache();

            // 缓存为空 → 需要索引
            if (_cache.Chunks.Count == 0) return true;

            // 收集缓存中所有已有的 hash
            var existingHashes = new HashSet<string>();
            foreach (var c in _cache.Chunks)
                existingHashes.Add(c.ContentHash);

            // 收集缓存中已有的 DocId
            var cachedDocIds = new HashSet<string>();
            foreach (var c in _cache.Chunks)
                cachedDocIds.Add(c.DocId);

            // 检查是否有新文档或内容变化的块
            var allDocs = registry.AllDocs;
            var currentDocIds = new HashSet<string>();
            foreach (var docRef in allDocs)
            {
                var docId = docRef.Entry.Tags != null && docRef.Entry.Tags.Length > 0
                    ? docRef.Entry.Tags[0] : docRef.Entry.Title;
                currentDocIds.Add(docId);

                // 新文档（缓存中不存在）
                if (!cachedDocIds.Contains(docId)) return true;

                // 内容变化
                var newChunks = ChunkDocument(docRef);
                foreach (var chunk in newChunks)
                {
                    chunk.ContentHash = ComputeHash(chunk.Text);
                    if (!existingHashes.Contains(chunk.ContentHash))
                        return true;
                }
            }

            // 检查是否有文档被删除
            foreach (var cachedId in cachedDocIds)
            {
                if (!currentDocIds.Contains(cachedId)) return true;
            }

            return false;
        }

        /// <summary>
        /// 对知识库的所有文档进行增量 Embedding 索引。
        /// 仅对内容 hash 变化的文档重新请求 API。
        /// </summary>
        public void IndexAllAsync(KnowledgeRegistry registry, Action<string>? onStatus = null, Action? onComplete = null)
        {
            if (_indexing)
            {
                onStatus?.Invoke("索引正在进行中...");
                return;
            }

            LoadCache();

            var allDocs = registry.AllDocs;
            UnityEngine.Debug.Log($"[EmbeddingService] AllDocs 数量: {allDocs.Count}");
            var chunksToEmbed = new List<DocChunk>();
            var existingHashes = new HashSet<string>();
            foreach (var c in _cache.Chunks)
                existingHashes.Add(c.ContentHash);

            // 收集需要重新计算的块
            foreach (var docRef in allDocs)
            {
                var newChunks = ChunkDocument(docRef);
                string? content = docRef.ReadContent();
                UnityEngine.Debug.Log($"[EmbeddingService] 文档 '{docRef.Entry.Title}': content={(content != null ? $"{content.Length} chars" : "NULL")}, chunks={newChunks.Count}");
                foreach (var chunk in newChunks)
                {
                    chunk.ContentHash = ComputeHash(chunk.Text);
                    if (!existingHashes.Contains(chunk.ContentHash))
                        chunksToEmbed.Add(chunk);
                }
            }

            // 清理缓存中不再存在的文档块
            var currentDocIds = new HashSet<string>();
            foreach (var doc in allDocs)
            {
                var id = doc.Entry.Tags?.FirstOrDefault() ?? doc.Entry.Title;
                currentDocIds.Add(id);
            }
            _cache.Chunks.RemoveAll(c => !currentDocIds.Contains(c.DocId));

            UnityEngine.Debug.Log($"[EmbeddingService] 需要 embed 的块: {chunksToEmbed.Count}, 缓存中已有: {_cache.Chunks.Count}");
            if (chunksToEmbed.Count == 0)
            {
                onStatus?.Invoke($"索引已是最新（共 {_cache.Chunks.Count} 个块）");
                onComplete?.Invoke();
                return;
            }

            // Embedding API 必须可用
            if (!SupportsEmbeddingApi())
            {
                UnityEngine.Debug.LogWarning("[EmbeddingService] Embedding API 未配置，请在设置中选择 Embedding 模型并填写 API Key");
                onStatus?.Invoke("❌ Embedding API 未配置，请在设置中配置");
                onComplete?.Invoke();
                return;
            }

            onStatus?.Invoke($"需要计算 {chunksToEmbed.Count} 个新块的向量...");
            _indexing = true;

            // 批量请求 Embedding API
            EmbedBatchAsync(chunksToEmbed, 0, onStatus, () =>
            {
                _indexing = false;
                SaveCache();
                onStatus?.Invoke($"索引完成（共 {_cache.Chunks.Count} 个块）");
                onComplete?.Invoke();
            });
        }

        /// <summary>逐批请求 Embedding API（每批最多 6 条，DashScope 限制 ≤ 10）。</summary>
        private void EmbedBatchAsync(List<DocChunk> chunks, int startIdx,
            Action<string>? onStatus, Action onComplete)
        {
            const int batchSize = 6; // DashScope text-embedding-v4 限制 batch ≤ 10，留余量
            if (startIdx >= chunks.Count)
            {
                onComplete();
                return;
            }

            int end = Math.Min(startIdx + batchSize, chunks.Count);
            var batch = chunks.GetRange(startIdx, end - startIdx);

            onStatus?.Invoke($"正在计算向量 {startIdx + 1}~{end} / {chunks.Count}...");

            CallEmbeddingApi(batch, vectors =>
            {
                if (vectors != null && vectors.Count == batch.Count)
                {
                    for (int i = 0; i < batch.Count; i++)
                    {
                        batch[i].Embedding = vectors[i];
                        _cache.Chunks.Add(batch[i]);
                    }
                }
                else
                {
                    onStatus?.Invoke($"批次 {startIdx + 1}~{end} 向量计算失败，跳过");
                }

                // 继续下一批
                EmbedBatchAsync(chunks, end, onStatus, onComplete);
            });
        }

        // ══════════════════════════════════════
        //  语义搜索
        // ══════════════════════════════════════

        /// <summary>
        /// 用 Embedding 向量进行语义搜索，返回 Top-K 最相关的文档块。
        /// </summary>
        public void SearchAsync(string query, int topK, Action<List<DocChunk>> onResult)
        {
            LoadCache();

            if (_cache.Chunks.Count == 0)
            {
                onResult(new List<DocChunk>());
                return;
            }

            // Embedding API 必须可用
            if (!SupportsEmbeddingApi())
            {
                UnityEngine.Debug.LogWarning("[EmbeddingService] Embedding API 未配置，无法进行语义检索");
                onResult(new List<DocChunk>());
                return;
            }

            // 先获取 query 的 embedding
            var queryChunk = new DocChunk { Text = query };
            CallEmbeddingApi(new List<DocChunk> { queryChunk }, vectors =>
            {
                if (vectors == null || vectors.Count == 0)
                {
                    UnityEngine.Debug.LogWarning("[EmbeddingService] 获取 query embedding 失败");
                    onResult(new List<DocChunk>());
                    return;
                }

                float[] queryVec = vectors[0];

                // 计算余弦相似度
                var scored = new List<(DocChunk chunk, float score)>();
                foreach (var chunk in _cache.Chunks)
                {
                    if (chunk.Embedding == null || chunk.Embedding.Length == 0) continue;
                    float sim = CosineSimilarity(queryVec, chunk.Embedding);
                    scored.Add((chunk, sim));
                }

                scored.Sort((a, b) => b.score.CompareTo(a.score));
                var results = scored.Take(topK).Select(s => s.chunk).ToList();
                onResult(results);
            });
        }

        // ══════════════════════════════════════
        //  Embedding API 调用
        // ══════════════════════════════════════

        /// <summary>调用 Embedding API，返回每个输入文本对应的向量。</summary>
        private void CallEmbeddingApi(List<DocChunk> chunks, Action<List<float[]>?> onResult)
        {
            var embConfig = AiModelManager.GetEmbeddingConfig();
            string apiKey = AiModelManager.GetEmbeddingApiKey();

            if (embConfig.Provider == "none" || string.IsNullOrEmpty(apiKey))
            {
                UnityEngine.Debug.LogWarning("[EmbeddingService] Embedding API Key 未配置或为关键词模式");
                onResult(null);
                return;
            }

            string embeddingUrl = embConfig.ApiUrl;
            string embeddingModel = embConfig.Model;
            string provider = embConfig.Provider;

            // 调试：打印实际使用的配置
            string keyHead = apiKey.Length > 8 ? apiKey.Substring(0, 8) : apiKey;
            string keyTail = apiKey.Length > 4 ? apiKey.Substring(apiKey.Length - 4) : "";
            UnityEngine.Debug.Log($"[EmbeddingService] CallAPI: provider={provider}, model={embeddingModel}, url={embeddingUrl}, keyLen={apiKey.Length}, key={keyHead}...{keyTail}");

            // 按 Provider 构建请求 JSON
            string requestBody = BuildEmbeddingRequestBody(provider, embeddingModel, chunks);
            UnityEngine.Debug.Log($"[EmbeddingService] RequestBody (first 200): {requestBody.Substring(0, Math.Min(200, requestBody.Length))}");

            var request = new UnityWebRequest(embeddingUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.timeout = 60;

            var operation = request.SendWebRequest();

            void PollResult()
            {
                if (!operation.isDone) return;
                EditorApplication.update -= PollResult;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.LogWarning($"[EmbeddingService] API 请求失败: {request.error}\n{request.downloadHandler?.text}");
                    request.Dispose();
                    onResult(null);
                    return;
                }

                string responseText = request.downloadHandler.text;
                request.Dispose();

                var vectors = ParseEmbeddingResponse(provider, responseText);
                onResult(vectors);
            }

            EditorApplication.update += PollResult;
        }

        /// <summary>按 Provider 构建 Embedding 请求体。</summary>
        private static string BuildEmbeddingRequestBody(string provider, string model, List<DocChunk> chunks)
        {
            var sb = new StringBuilder();

            if (provider == "dashscope")
            {
                // 通义千问 DashScope 格式: {"model": "...", "input": {"texts": [...]}}
                sb.Append("{\"model\":\"");
                sb.Append(EscJson(model));
                sb.Append("\",\"input\":{\"texts\":[");
                for (int i = 0; i < chunks.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append("\"");
                    sb.Append(EscJson(chunks[i].Text));
                    sb.Append("\"");
                }
                sb.Append("]}}");

            }
            else
            {
                // OpenAI 兼容格式: {"model": "...", "input": [...]}
                sb.Append("{\"model\":\"");
                sb.Append(EscJson(model));
                sb.Append("\",\"input\":[");
                for (int i = 0; i < chunks.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append("\"");
                    sb.Append(EscJson(chunks[i].Text));
                    sb.Append("\"");
                }
                sb.Append("]}");
            }

            return sb.ToString();
        }

        /// <summary>按 Provider 解析 Embedding API 响应，提取向量数组。</summary>
        private static List<float[]>? ParseEmbeddingResponse(string provider, string json)
        {
            try
            {
                if (provider == "dashscope")
                    return ParseDashScopeEmbeddingResponse(json);
                else
                    return ParseOpenAiEmbeddingResponse(json);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[EmbeddingService] 解析响应失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析 OpenAI 格式响应。
        /// 结构: {"data": [{"embedding": [0.1, 0.2, ...]}, ...]}
        /// </summary>
        private static List<float[]>? ParseOpenAiEmbeddingResponse(string json)
        {
            var results = new List<float[]>();
            int dataIdx = json.IndexOf("\"data\"", StringComparison.Ordinal);
            if (dataIdx < 0) return null;

            int searchFrom = dataIdx;
            while (true)
            {
                int embIdx = json.IndexOf("\"embedding\"", searchFrom, StringComparison.Ordinal);
                if (embIdx < 0) break;

                var vec = ExtractFloatArray(json, embIdx, out int nextPos);
                if (vec == null) break;
                results.Add(vec);
                searchFrom = nextPos;
            }

            return results.Count > 0 ? results : null;
        }

        /// <summary>
        /// 解析通义千问 DashScope 格式响应。
        /// 结构: {"output": {"embeddings": [{"text_index": 0, "embedding": [0.1, 0.2, ...]}, ...]}}
        /// </summary>
        private static List<float[]>? ParseDashScopeEmbeddingResponse(string json)
        {
            var results = new List<float[]>();
            int outputIdx = json.IndexOf("\"embeddings\"", StringComparison.Ordinal);
            if (outputIdx < 0) return null;

            int searchFrom = outputIdx;
            while (true)
            {
                int embIdx = json.IndexOf("\"embedding\"", searchFrom, StringComparison.Ordinal);
                if (embIdx < 0) break;

                var vec = ExtractFloatArray(json, embIdx, out int nextPos);
                if (vec == null) break;
                results.Add(vec);
                searchFrom = nextPos;
            }

            return results.Count > 0 ? results : null;
        }

        /// <summary>从 JSON 字符串中提取一个 float 数组，起始位置从 keyIdx 后的第一个 '[' 开始。</summary>
        private static float[]? ExtractFloatArray(string json, int keyIdx, out int nextPos)
        {
            nextPos = keyIdx + 1;
            int arrStart = json.IndexOf('[', keyIdx);
            if (arrStart < 0) return null;

            int arrEnd = json.IndexOf(']', arrStart);
            if (arrEnd < 0) return null;

            nextPos = arrEnd + 1;
            string arrStr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            var parts = arrStr.Split(',');
            var vec = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out vec[i]))
                    vec[i] = 0f;
            }
            return vec;
        }

        // ══════════════════════════════════════
        //  工具方法
        // ══════════════════════════════════════

        /// <summary>判断当前 Embedding 配置是否支持向量检索。</summary>
        private static bool SupportsEmbeddingApi()
        {
            return AiModelManager.HasEmbeddingSupport();
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length || a.Length == 0) return 0f;
            float dot = 0f, normA = 0f, normB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            float denom = Mathf.Sqrt(normA) * Mathf.Sqrt(normB);
            return denom > 0f ? dot / denom : 0f;
        }

        private static string ComputeHash(string text)
        {
            // 简易哈希（FNV-1a 变体），用于增量更新判断
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return hash.ToString("x8");
            }
        }

        private static string EscJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        // ══════════════════════════════════════
        //  数据结构
        // ══════════════════════════════════════

        /// <summary>文档分块数据。</summary>
        [Serializable]
        public class DocChunk
        {
            public string DocId = "";
            public string DocTitle = "";
            public string Section = "";
            public int PartIndex;
            public string Text = "";
            public string ContentHash = "";
            public float[] Embedding = Array.Empty<float>();
        }

        /// <summary>Embedding 缓存（序列化到 Library/SceneBlueprint/embeddings.json）。</summary>
        [Serializable]
        public class EmbeddingCache
        {
            public string Version = "1.0";
            public List<DocChunk> Chunks = new();
        }
    }
}
