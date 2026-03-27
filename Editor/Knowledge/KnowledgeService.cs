#nullable enable
using System;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Editor.Session;
using SceneBlueprint.Runtime.Knowledge;
using SceneBlueprint.Editor.Knowledge.ChatPanel;

namespace SceneBlueprint.Editor.Knowledge
{
    /// <summary>
    /// 知识库服务——统一管理 Registry、ContextProvider、EmbeddingService、MCP Server 的生命周期。
    /// 在 Editor 层作为单例使用，供 AIChatPanel 和外部工具访问。
    /// </summary>
    public class KnowledgeService : IDisposable
    {
        private static KnowledgeService? _instance;
        public static KnowledgeService Instance => _instance ??= new KnowledgeService();

        // ── 核心组件 ──
        public KnowledgeRegistry Registry { get; private set; }
        public EditorBlueprintContextProvider ContextProvider { get; private set; }
        public EmbeddingService Embedding { get; private set; }

        // ── MCP Server ──
        private KnowledgeServer? _server;
        private KnowledgeServerEndpoints? _endpoints;
        public bool IsServerRunning => _server?.IsRunning ?? false;

        // ── 上下文刷新节流 ──
        private double _lastContextRefresh;
        private const double ContextRefreshInterval = 1.0; // 秒

        private KnowledgeService()
        {
            Registry = new KnowledgeRegistry();
            ContextProvider = new EditorBlueprintContextProvider();
            Embedding = new EmbeddingService();
        }

        // ══════════════════════════════════════
        //  初始化
        // ══════════════════════════════════════

        /// <summary>
        /// 加载 KnowledgeManifest 资产，初始化 Registry。
        /// 可在编辑器启动或首次需要时调用。
        /// </summary>
        public void LoadManifest(KnowledgeManifest? manifest = null)
        {
            if (manifest == null)
            {
                // 自动查找项目中的 KnowledgeManifest
                var guids = AssetDatabase.FindAssets("t:KnowledgeManifest");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    manifest = AssetDatabase.LoadAssetAtPath<KnowledgeManifest>(path);
                }
            }

            if (manifest != null)
            {
                Registry.Initialize(manifest);
                UnityEngine.Debug.Log($"[KnowledgeService] Manifest 已加载: {manifest.name}");

                // 自动检测知识库文档变更，触发增量 Embedding 索引
                TryAutoReindex();
            }
        }

        /// <summary>
        /// 自动检测知识库文档是否有变更，如果 Embedding 已配置则自动触发增量索引。
        /// 在 Manifest 加载后调用，对应窗口 OnEnable。
        /// </summary>
        private void TryAutoReindex()
        {
            if (!AiModelManager.HasEmbeddingSupport()) return;
            if (Embedding.IsIndexing) return;

            Embedding.LoadCache();
            if (Embedding.NeedsReindex(Registry))
            {
                UnityEngine.Debug.Log("[KnowledgeService] 检测到知识库文档变更，自动触发增量索引...");
                Embedding.IndexAllAsync(Registry,
                    status => UnityEngine.Debug.Log($"[KnowledgeService] {status}"),
                    () => UnityEngine.Debug.Log($"[KnowledgeService] 自动索引完成（{Embedding.ChunkCount} 块）"));
            }
        }

        /// <summary>
        /// 绑定当前活跃的 Session（由 SceneBlueprintWindow 在 Session 创建/切换时调用）。
        /// </summary>
        internal void BindSession(BlueprintEditorSession? session)
        {
            ContextProvider.ActiveSession = session;
        }

        // ══════════════════════════════════════
        //  MCP Server
        // ══════════════════════════════════════

        public bool StartServer(int port = KnowledgeServer.DefaultPort)
        {
            if (_server != null && _server.IsRunning)
                return true;

            _endpoints = new KnowledgeServerEndpoints(Registry, ContextProvider);
            _server = new KnowledgeServer(_endpoints, port);

            if (_server.Start())
            {
                // 注册 EditorUpdate 轮询刷新上下文缓存
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
                return true;
            }
            return false;
        }

        public void StopServer()
        {
            EditorApplication.update -= OnEditorUpdate;
            _server?.Stop();
            _server = null;
            _endpoints = null;
        }

        // ══════════════════════════════════════
        //  EditorUpdate 回调
        // ══════════════════════════════════════

        private void OnEditorUpdate()
        {
            if (_endpoints == null) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastContextRefresh >= ContextRefreshInterval)
            {
                _lastContextRefresh = now;
                _endpoints.RefreshContextCache();
            }
        }

        // ══════════════════════════════════════
        //  清理
        // ══════════════════════════════════════

        public void Dispose()
        {
            StopServer();
            _instance = null;
        }

        /// <summary>
        /// 在 Domain Reload 时清理（由 [InitializeOnLoadMethod] 调用）。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            // Domain Reload 后旧实例已失效，清除引用
            if (_instance != null)
            {
                _instance.StopServer();
                _instance = null;
            }
        }
    }
}
