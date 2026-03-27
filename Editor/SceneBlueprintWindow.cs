#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using NodeGraph.Core;
using NodeGraph.Math;
using NodeGraph.View;
using NodeGraph.Unity;
using NodeGraph.Serialization;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Editor.Preview;
using SceneBlueprint.Editor.Session;
using SceneBlueprint.Editor.Settings;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Editor.WindowServices;
using SceneBlueprint.Runtime;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// SceneBlueprint 蓝图编辑器窗口（薄壳）。
    /// 仅负责 Unity EditorWindow 生命周期、布局参数持久化和 Session 管理。
    /// 所有图状态、服务和 IBlueprintEditorContext 均由 <see cref="BlueprintEditorSession"/> 持有。
    /// </summary>
    public partial class SceneBlueprintWindow : EditorWindow, IWindowCallbacks
    {
        private static bool s_skipWorkspaceRestoreOnce;
        private static bool s_isAssemblyReloading;
        private static bool s_isEditorQuitting;
        private static SceneBlueprintWindow? s_cachedLevelIdWindow;

        [InitializeOnLoadMethod]
        private static void RegisterEditorLifecycleHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnBeforeAssemblyReload()
        {
            s_isAssemblyReloading = true;
        }

        private static void OnEditorQuitting()
        {
            s_isEditorQuitting = true;
        }

        // ══════════════════════════════════════════════════════════
        //  IWindowCallbacks 实现（注入给 Session，替代 Session 对 Window 的直接依赖）
        // ══════════════════════════════════════════════════════════
        void   IWindowCallbacks.Repaint()              => Repaint();
        Vector2 IWindowCallbacks.GetWindowSize()       => new Vector2(position.width, position.height);
        void   IWindowCallbacks.EnsureWorkbenchVisible()
        {
            if (!_showWorkbench)
            {
                _showWorkbench = true;
                _showAIChat = false; // 互斥
                SaveWindowUiSettings();
            }
        }
        void IWindowCallbacks.SetExportTime(string t)  => _lastExportTime = t;
        void IWindowCallbacks.SetTitle(string title)   => titleContent = new GUIContent(title);

        // ══════════════════════════════════════════════════════════
        //  公共 API（供业务层 PropertyDrawer / 工具查询）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 获取当前打开的蓝图编辑器窗口关联的关卡 ID。
        /// 未打开窗口或未设置时返回 0。
        /// </summary>
        public static int GetCurrentLevelId()
        {
            if (s_cachedLevelIdWindow != null)
            {
                var cachedAsset = s_cachedLevelIdWindow._currentAsset;
                if (cachedAsset != null && cachedAsset.LevelId > 0)
                    return cachedAsset.LevelId;

                s_cachedLevelIdWindow = null;
            }

            var windows = Resources.FindObjectsOfTypeAll<SceneBlueprintWindow>();
            foreach (var w in windows)
            {
                var asset = w._currentAsset;
                if (asset != null && asset.LevelId > 0)
                {
                    s_cachedLevelIdWindow = w;
                    return asset.LevelId;
                }
            }

            return 0;
        }

        // ══════════════════════════════════════════════════════════
        //  Session（持有所有图状态 + IBlueprintEditorContext 实现）
        // ══════════════════════════════════════════════════════════
        private BlueprintEditorSession? _session;

        // ══════════════════════════════════════════════════════════
        //  持久化（Domain Reload 跨域保留）
        // ══════════════════════════════════════════════════════════
        [SerializeField] private BlueprintAsset? _currentAsset;
        [SerializeField] private string          _graphJsonBeforeReload = "";

        // ══════════════════════════════════════════════════════════
        //  会话状态（L2 场景感知层）
        // ══════════════════════════════════════════════════════════
        private enum SessionState { Empty, Active, Suspended }

        [SerializeField] private string _anchoredScenePath = "";
        [System.NonSerialized] private SessionState _sessionState = SessionState.Empty;

        private bool IsSceneAnchored =>
            !string.IsNullOrEmpty(_anchoredScenePath) &&
            EditorSceneManager.GetActiveScene().path == _anchoredScenePath;

        // ══════════════════════════════════════════════════════════
        //  窗口级基础设施（不属于图状态，不随 Session 重建）
        // ══════════════════════════════════════════════════════════
        private IEditorSpatialModeDescriptor? _spatialModeDescriptor;
        private readonly SceneBlueprintToolContext _toolContext       = new SceneBlueprintToolContext();

        // ══════════════════════════════════════════════════════════
        //  布局参数（UI 状态，持久化到 UserConfig）
        // ══════════════════════════════════════════════════════════
        private const float ToolbarHeight         = 22f;
        private const float MinInspectorWidth     = 240f;
        private const float MaxInspectorWidthRatio = 0.62f;
        private const float DefaultInspectorWidth = 360f;
        private const float MinWorkbenchWidth     = 220f;
        private const float MaxWorkbenchWidth     = 520f;
        private const float DefaultWorkbenchWidth = 300f;
        private const float SplitterWidth         = 4f;
        private const float MinCanvasWidth        = 260f;
        private const float MinAnalysisHeight     = 60f;
        private const float DefaultAnalysisHeight = 160f;
        private const float AnalysisSplitterHeight = 4f;
        private const float CollapsedAnalysisHeight = 22f;

        private float _inspectorWidth  = DefaultInspectorWidth;
        private float _workbenchWidth  = DefaultWorkbenchWidth;
        private float _analysisHeight  = DefaultAnalysisHeight;
        private bool  _isDraggingSplitter;
        private bool  _isDraggingWorkbenchSplitter;
        private bool  _isDraggingAnalysisSplitter;
        private bool  _showWorkbench            = true;
        private bool  _collapseAnalysisPanel;
        private bool  _useEditorToolSelectionInput = true;
        private string _lastExportTime          = "";
        private Vector2 _analysisScrollPos;

        // ══════════════════════════════════════════════════════════
        //  MenuItem
        // ══════════════════════════════════════════════════════════
        [MenuItem("SceneBlueprint/蓝图编辑器 &B")]
        public static void Open()
        {
            OpenWindowInstance(suppressWorkspaceRestore: false);
        }

        internal static SceneBlueprintWindow OpenWindowInstance(bool suppressWorkspaceRestore)
        {
            bool hasExistingWindow = Resources.FindObjectsOfTypeAll<SceneBlueprintWindow>().Length > 0;
            if (suppressWorkspaceRestore && !hasExistingWindow)
                s_skipWorkspaceRestoreOnce = true;

            var window = GetWindow<SceneBlueprintWindow>();
            window.titleContent = new GUIContent("场景蓝图编辑器");
            window.minSize = new Vector2(800, 600);
            window.Show();
            CenterOnMainWindow(window);
            return window;
        }

        /// <summary>
        /// 将 EditorWindow 居中到 Unity 主窗口。
        /// 通过反射获取主窗口位置（ContainerWindow），兼容 docked 和 floating 两种模式。
        /// </summary>
        internal static void CenterOnMainWindow(EditorWindow window)
        {
            // 获取主窗口的屏幕矩形
            var mainWindowRect = EditorGUIUtility.GetMainWindowPosition();
            var windowSize = window.position.size;

            // 计算居中坐标
            float x = mainWindowRect.x + (mainWindowRect.width  - windowSize.x) * 0.5f;
            float y = mainWindowRect.y + (mainWindowRect.height - windowSize.y) * 0.5f;

            window.position = new Rect(x, y, windowSize.x, windowSize.y);
        }

        // ══════════════════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            _spatialModeDescriptor = SpatialModeRegistry.GetProjectModeDescriptor();
            var uiSettings         = SceneBlueprintSettingsService.User.UI;
            _showWorkbench         = uiSettings.ShowWorkbench;
            _showAIChat            = uiSettings.ShowAIChat;
            _workbenchWidth        = uiSettings.WorkbenchWidth > 0f ? uiSettings.WorkbenchWidth : DefaultWorkbenchWidth;
            _analysisHeight        = uiSettings.AnalysisHeight > 0f ? uiSettings.AnalysisHeight : DefaultAnalysisHeight;
            _collapseAnalysisPanel = uiSettings.CollapseAnalysisPanel;
            // 互斥保护：不允许两个左侧面板同时激活
            if (_showWorkbench && _showAIChat)
            {
                _showAIChat = false;
            }
            _useEditorToolSelectionInput = MarkerSelectionInputRoutingSettings.LoadUseEditorTool();
            _toolContext.Attach(_useEditorToolSelectionInput);
            GizmoRenderPipeline.SetInteractionMode(GizmoRenderPipeline.MarkerInteractionMode.Edit);

            // 窗口级 Unity 事件订阅（生命周期同 Window，不随 Session 重建）
            EditorApplication.hierarchyChanged    += OnEditorHierarchyChanged;
            EditorApplication.projectChanged      += OnEditorProjectChanged;
            Undo.undoRedoPerformed             += OnUndoRedoPerformed;
            Undo.postprocessModifications      += OnUndoPostprocessModifications;
            SceneMarkerSelectionBridge.OnHighlightNodesForMarkerRequested += OnSceneMarkerSelected;
            SceneMarkerSelectionBridge.OnFrameNodeForMarkerRequested      += OnSceneMarkerDoubleClicked;
            Selection.selectionChanged                                    += OnUnitySelectionChanged;
            EditorSceneManager.activeSceneChangedInEditMode               += OnActiveSceneChanged;

            bool skipWorkspaceRestore = ConsumeSkipWorkspaceRestoreOnce();
            if (!string.IsNullOrEmpty(_graphJsonBeforeReload))
                TryRestoreAfterDomainReload();
            else if (!skipWorkspaceRestore && TryRestoreLastWorkspaceOnWindowOpen())
            {
                // 已从本地工作区恢复，保持当前 Session。
            }
            else
                CreateSessionIfNeeded();

            // L3：检测场景中遗留的 Manager
            CheckForOrphanedSceneManager();

            if (_session != null && _session.ViewModel.Graph.Nodes.Count > 0)
                _session.MarkPreviewDirtyAll("OnEnable");

            // MCP Server 自动启动
            StartKnowledgeServer();
            AttachAutosaveWindowLifecycle();

            SaveWindowUiSettings();
        }

        /// <summary>
        /// 根据用户配置自动启动 MCP Server 并加载知识库。
        /// <para>
        /// 是否自动启动、使用哪个端口，都统一来自 `SceneBlueprintSettingsService`，
        /// 不再在窗口代码中写死默认行为。
        /// </para>
        /// </summary>
        private void StartKnowledgeServer()
        {
            var service = Knowledge.KnowledgeService.Instance;
            service.LoadManifest();
            service.BindSession(_session);

            if (!SceneBlueprintSettingsService.ShouldAutoStartKnowledgeServer())
            {
                return;
            }

            if (!service.IsServerRunning)
            {
                service.StartServer(SceneBlueprintSettingsService.GetKnowledgeServerPort());
            }
        }

        private void OnDisable()
        {
            SaveWindowUiSettings();
            MarkerSelectionInputRoutingSettings.SaveUseEditorTool(_useEditorToolSelectionInput);

            if (ShouldAutoSaveOnWindowDisable())
            {
                try
                {
                    TryAutoSaveBlueprintOnWindowClose();
                }
                catch (Exception ex)
                {
                    SBLog.Error(SBLogTags.Blueprint, $"窗口关闭自动保存失败: {ex.Message}");
                }
            }

            // MCP Server 自动停止
            Knowledge.KnowledgeService.Instance.StopServer();

            // 窗口级事件取消订阅
            EditorApplication.hierarchyChanged    -= OnEditorHierarchyChanged;
            EditorApplication.projectChanged      -= OnEditorProjectChanged;
            Undo.undoRedoPerformed             -= OnUndoRedoPerformed;
            Undo.postprocessModifications      -= OnUndoPostprocessModifications;
            SceneMarkerSelectionBridge.OnHighlightNodesForMarkerRequested -= OnSceneMarkerSelected;
            SceneMarkerSelectionBridge.OnFrameNodeForMarkerRequested      -= OnSceneMarkerDoubleClicked;
            Selection.selectionChanged                                    -= OnUnitySelectionChanged;
            EditorSceneManager.activeSceneChangedInEditMode               -= OnActiveSceneChanged;
            SceneMarkerSelectionBridge.ClearHighlight();

            _toolContext.Detach();
            DetachAutosaveWindowLifecycle();

            // Domain Reload 前序列化图数据
            if (_session != null)
            {
                try   { _graphJsonBeforeReload = _session.SerializeGraph(); }
                catch (System.Exception ex)
                {
                    SBLog.Error(SBLogTags.Blueprint, $"Domain Reload 前保存图数据失败: {ex.Message}");
                    _graphJsonBeforeReload = "";
                }
            }

            DisposeSession();
        }

        private static bool ShouldAutoSaveOnWindowDisable()
        {
            if (s_isEditorQuitting)
                return true;

            if (s_isAssemblyReloading)
                return false;

            if (EditorApplication.isCompiling)
                return false;

            return true;
        }

        /// <summary>
        /// 将蓝图主窗口的本地 UI 偏好写回 UserConfig。
        /// </summary>
        private void SaveWindowUiSettings()
        {
            var user = SceneBlueprintSettingsService.User;
            user.UI.ShowWorkbench = _showWorkbench;
            user.UI.ShowAIChat = _showAIChat;
            user.UI.WorkbenchWidth = _workbenchWidth;
            user.UI.AnalysisHeight = _analysisHeight;
            user.UI.CollapseAnalysisPanel = _collapseAnalysisPanel;
            user.SaveConfig();
        }

        private void OnDestroy()
        {
            // 窗口真正关闭：清理场景中的 Manager 和 Marker
            // 注意：此时 _session 已在 OnDisable 中 Dispose，无法保存
            // 保存确认只在 TeardownSession（NewGraph/LoadBlueprint）中触发
            CleanupScene();
        }

        // ══════════════════════════════════════════════════════════
        //  L2 场景感知
        // ══════════════════════════════════════════════════════════

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            if (_session == null || _currentAsset == null)
            {
                _sessionState = SessionState.Empty;
                return;
            }

            if (IsSceneAnchored)
            {
                // 切回了锚定场景（或场景重新加载）
                if (_sessionState == SessionState.Suspended)
                    TryResumeFromSuspended();
            }
            else
            {
                // 切换到了其他场景
                if (_sessionState == SessionState.Active)
                    EnterSuspendedState();
            }

            Repaint();
        }

        private void EnterSuspendedState()
        {
            _sessionState = SessionState.Suspended;
            // 清空 BindingContext，GO 引用已失效
            _session?.BindingContextPublic.Clear();
            SBLog.Info(SBLogTags.Blueprint,
                "会话已挂起：场景已切换，切回 [{0}] 可恢复绑定",
                System.IO.Path.GetFileNameWithoutExtension(_anchoredScenePath));
        }

        private void EnterSuspendedState(string anchoredScenePath)
        {
            _anchoredScenePath = anchoredScenePath ?? "";
            EnterSuspendedState();
        }

        private void TryResumeFromSuspended()
        {
            if (_currentAsset == null || _session == null) return;

            _sessionState = SessionState.Active;
            RestoreMarkersFromSnapshot(_currentAsset);
            _session.RestoreBindingsFromScene();
            _session.RunBindingValidation();
            SBLog.Info(SBLogTags.Blueprint, "会话已恢复：场景重新匹配");
        }

        /// <summary>设置场景锚定路径（Active 状态起点）。</summary>
        internal void AnchorToCurrentScene()
        {
            _anchoredScenePath = EditorSceneManager.GetActiveScene().path;
            _sessionState = SessionState.Active;
        }

        /// <summary>清除场景锚定（TeardownSession 时调用）。</summary>
        internal void ClearSceneAnchor()
        {
            _anchoredScenePath = "";
            _sessionState = SessionState.Empty;
        }

        // ══════════════════════════════════════════════════════════
        //  L3 遗留 Manager 检测
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 检测场景中是否存在遗留的 SceneBlueprintManager GO（旧版残留）。
        /// 新流程不再使用此组件，场景中存在即为遗留，提示用户清理。
        /// </summary>
        private void CheckForOrphanedSceneManager()
        {
            var go = GameObject.Find("SceneBlueprintManager");
            if (go == null) return;

            bool shouldClean = EditorUtility.DisplayDialog(
                "检测到遗留场景管理器",
                "场景中存在一个旧版 SceneBlueprintManager 对象。\n" +
                "新版编辑器不再使用此组件，建议清理。",
                "清理",
                "保留");

            if (shouldClean)
            {
                Markers.MarkerHierarchyManager.DestroyAll();
                Undo.DestroyObjectImmediate(go);
                SBLog.Info(SBLogTags.Blueprint, "已清理遗留的 SceneBlueprintManager");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  Session 管理
        // ══════════════════════════════════════════════════════════

        private void CreateSessionIfNeeded()
        {
            if (_session != null) return;
            RecreateSession(null);
        }

        /// <summary>创建新 Session（同时 Dispose 旧 Session）。传 null 则新建空白图。</summary>
        private void RecreateSession(Graph? graph)
        {
            DisposeSession();
            _session = new BlueprintEditorSession(
                graph,
                EnsureSpatialModeDescriptor(),
                _toolContext,
                this,
                OnBlueprintSelectionChanged,
                OnNodePropertyChanged,
                OnCanvasContextMenu,
                OnNodeContextMenu,
                OnPortContextMenu);
            // A1: Session 持有资产真相，Window 通过事件同步自身的 [SerializeField] 字段
            _session.OnAssetChanged += asset => _currentAsset = asset;
            _session.SetAsset(_currentAsset);
            AttachAutosaveSessionHooks();

            // 绑定知识库服务
            Knowledge.KnowledgeService.Instance.BindSession(_session);
        }

        private void DisposeSession()
        {
            DetachAutosaveSessionHooks();
            _session?.Dispose();
            _session = null;
            _chatPanel = null; // Session 切换时重建 ChatPanel
        }

        private void TryRestoreAfterDomainReload()
        {
            try
            {
                var serializer = new JsonGraphSerializer(new ActionNodeDataSerializer(),
                    BuildTypeProviderForReload());
                var graph = serializer.Deserialize(_graphJsonBeforeReload);
                _graphJsonBeforeReload = "";
                RecreateSession(graph);
                _session!.RestoreBindingsFromScene();
                SBLog.Info(SBLogTags.Blueprint,
                    $"Domain Reload 后恢复蓝图成功（节点: {graph.Nodes.Count}）");
            }
            catch (System.Exception ex)
            {
                SBLog.Error(SBLogTags.Blueprint, $"Domain Reload 后恢复蓝图失败: {ex.Message}");
                _graphJsonBeforeReload = "";
                RecreateSession(null);
            }
        }


        private static INodeTypeCatalog BuildTypeProviderForReload()
        {
            var actionRegistry = SceneBlueprintProfile.CreateActionRegistry();
            return new ActionRegistryNodeTypeCatalog(actionRegistry);
        }

        private static bool ConsumeSkipWorkspaceRestoreOnce()
        {
            bool value = s_skipWorkspaceRestoreOnce;
            s_skipWorkspaceRestoreOnce = false;
            return value;
        }

        private bool TryRestoreLastWorkspaceOnWindowOpen()
        {
            var workspace = SceneBlueprintSettingsService.User.Workspace;
            if (!workspace.RestoreLastBlueprintOnOpen)
                return false;

            if (!TryResolveWorkspaceAssetPath(workspace, out var assetPath))
                return TryRestoreAnonymousDraftOnWindowOpen();

            var asset = AssetDatabase.LoadAssetAtPath<BlueprintAsset>(assetPath);
            if (asset == null || asset.IsEmpty)
            {
                ClearWorkspaceRestoreState();
                SBLog.Warn(SBLogTags.Blueprint, $"最近蓝图工作区已失效，已清理记录: {assetPath}");
                return false;
            }

            string currentScenePath = EditorSceneManager.GetActiveScene().path;
            string anchoredScenePath = workspace.LastAnchoredScenePath ?? string.Empty;
            bool restoreSceneContext =
                string.IsNullOrEmpty(anchoredScenePath) ||
                string.Equals(currentScenePath, anchoredScenePath, StringComparison.Ordinal);

            return TryLoadBlueprintAsset(
                asset,
                restoreSceneContext,
                restoreSceneContext ? currentScenePath : anchoredScenePath,
                showFailureDialog: false,
                loadReason: "RestoreLastWorkspace");
        }

        private void PersistCurrentWorkspaceState()
        {
            if (_currentAsset == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(_currentAsset);
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            var user = SceneBlueprintSettingsService.User;
            var workspace = user.Workspace;
            _anonymousDraftId = string.Empty;
            workspace.LastOpenedBlueprintAssetPath = assetPath;
            workspace.LastOpenedBlueprintAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            workspace.LastAnonymousDraftId = string.Empty;
            workspace.LastAnchoredScenePath = _anchoredScenePath ?? string.Empty;
            user.SaveConfig();
        }

        private void ClearWorkspaceRestoreState()
        {
            var user = SceneBlueprintSettingsService.User;
            var workspace = user.Workspace;
            _anonymousDraftId = string.Empty;
            workspace.LastOpenedBlueprintAssetGuid = string.Empty;
            workspace.LastOpenedBlueprintAssetPath = string.Empty;
            workspace.LastAnonymousDraftId = string.Empty;
            workspace.LastAnchoredScenePath = string.Empty;
            user.SaveConfig();
        }

        private static bool TryResolveWorkspaceAssetPath(
            SceneBlueprintEditorWorkspaceSettings workspace,
            out string assetPath)
        {
            assetPath = string.Empty;

            if (!string.IsNullOrWhiteSpace(workspace.LastOpenedBlueprintAssetGuid))
            {
                var pathFromGuid = AssetDatabase.GUIDToAssetPath(workspace.LastOpenedBlueprintAssetGuid);
                if (!string.IsNullOrWhiteSpace(pathFromGuid))
                {
                    assetPath = pathFromGuid;
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(workspace.LastOpenedBlueprintAssetPath))
            {
                assetPath = workspace.LastOpenedBlueprintAssetPath;
                return true;
            }

            return false;
        }

        private IEditorSpatialModeDescriptor EnsureSpatialModeDescriptor()
        {
            _spatialModeDescriptor ??= SpatialModeRegistry.GetProjectModeDescriptor();
            return _spatialModeDescriptor;
        }

        // ══════════════════════════════════════════════════════════
        //  OnGUI 主循环
        // ══════════════════════════════════════════════════════════

        private void OnGUI()
        {
            CreateSessionIfNeeded();
            if (_session == null) return;

            var vm  = _session.ViewModel;
            var rdr = _session.Renderer;
            var inp = _session.Input;
            var ec  = _session.EditContext;
            var ch  = _session.CoordinateHelper;
            var ip  = _session.InspectorPanel;
            if (vm == null || rdr == null || inp == null || ec == null || ch == null || ip == null)
                return;

            var evt    = Event.current;
            RecordAutosaveInteraction(evt);
            var layout = CalculateWindowLayout();

            DrawToolbar();

            bool showLeftPanel = _showWorkbench || _showAIChat;
            if (showLeftPanel) HandleWorkbenchSplitter(layout.WorkbenchSplitterRect, evt);
            HandleSplitter(layout.SplitterRect, evt, layout.WorkbenchRect.width);
            if (_showWorkbench) DrawWorkbenchPanel(layout.WorkbenchRect);
            else if (_showAIChat) DrawAIChatPanel(layout.WorkbenchRect);

            ch.SetGraphAreaRect(layout.GraphRect);
            var viewport = new Rect2(0, 0, layout.GraphRect.width, layout.GraphRect.height);
            inp.Update(evt, ch);

            if (evt.type == EventType.Repaint)
            {
                if (showLeftPanel)
                {
                    EditorGUI.DrawRect(new Rect(layout.WorkbenchRect.xMax - 1f, layout.ContentTop, 1f, layout.ContentHeight),
                        new Color(0.13f, 0.13f, 0.13f, 1f));
                    EditorGUI.DrawRect(layout.WorkbenchSplitterRect, new Color(0.15f, 0.15f, 0.15f, 1f));
                }
                EditorGUI.DrawRect(layout.SplitterRect, new Color(0.15f, 0.15f, 0.15f, 1f));
                GUI.BeginClip(layout.GraphRect);
                {
                    vm.Update(0.016f);
                    var frame = vm.BuildFrame(viewport);
                    rdr.Render(frame, vm.RenderConfig.Theme, new Rect(0, 0, layout.GraphRect.width, layout.GraphRect.height),
                        ec, Vector2.zero);
                }
                GUI.EndClip();
            }
            else if (evt.type == EventType.ContextClick && layout.GraphRect.Contains(evt.mousePosition))
            {
                evt.Use();
            }
            else if (IsInputEvent(evt) && layout.GraphRect.Contains(evt.mousePosition))
            {
                List<string>? deletingNodeIds = null;
                if (evt.type == EventType.KeyDown
                    && (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace))
                    deletingNodeIds = vm.Selection.SelectedNodeIds.ToList();

                vm.PreUpdateNodeSizes();
                vm.ProcessInput(inp);

                if (deletingNodeIds != null && deletingNodeIds.Count > 0)
                    _session.NotifyNodesDeleted(deletingNodeIds);
                evt.Use();
            }

            // Inspector + 底部分析面板竖向分割
            _session.InspectorDrawer.SetVariableDeclarations(
                _session.BlackboardService.BuildCombinedVariables());

            // 底部面板始终显示（分析报告）
            bool hasBottomPanel = true;
            var inspectorRect        = layout.InspectorRect;
            Rect inspContentRect     = inspectorRect;
            Rect analysisSplitterBarRect = default;
            Rect bottomPanelRect     = default;
            if (hasBottomPanel)
            {
                if (_collapseAnalysisPanel)
                {
                    var collapsedHeight = CollapsedAnalysisHeight;
                    inspContentRect = new Rect(
                        inspectorRect.x,
                        inspectorRect.y,
                        inspectorRect.width,
                        Mathf.Max(40f, inspectorRect.height - collapsedHeight));
                    bottomPanelRect = new Rect(
                        inspectorRect.x,
                        inspectorRect.yMax - collapsedHeight,
                        inspectorRect.width,
                        collapsedHeight);
                }
                else
                {
                    float clampedH = Mathf.Clamp(_analysisHeight, MinAnalysisHeight, inspectorRect.height * 0.6f);
                    _analysisHeight = clampedH;
                    float splitterY = inspectorRect.yMax - clampedH - AnalysisSplitterHeight;
                    inspContentRect         = new Rect(inspectorRect.x, inspectorRect.y,     inspectorRect.width, Mathf.Max(40f, splitterY - inspectorRect.y));
                    analysisSplitterBarRect = new Rect(inspectorRect.x, splitterY,           inspectorRect.width, AnalysisSplitterHeight);
                    bottomPanelRect         = new Rect(inspectorRect.x, splitterY + AnalysisSplitterHeight, inspectorRect.width, clampedH);
                    HandleAnalysisSplitter(analysisSplitterBarRect, evt, inspectorRect);
                }
            }

            if (ip.Draw(inspContentRect, vm))
                vm.RequestRepaint();

            if (hasBottomPanel)
            {
                if (!_collapseAnalysisPanel && evt.type == EventType.Repaint)
                    EditorGUI.DrawRect(analysisSplitterBarRect, new Color(0.15f, 0.15f, 0.15f, 1f));
                DrawBottomPanel(bottomPanelRect);
            }

            _session.DetectPreviewGraphShapeChange();

            if (vm.NeedsRepaint) Repaint();
        }

        // ════════════════════════════════════════════════════════
        //  转发方法——partial 文件调用
        // ════════════════════════════════════════════════════════

        private JsonGraphSerializer CreateGraphSerializer()
            => _session?.CreateGraphSerializer() ?? new JsonGraphSerializer(new ActionNodeDataSerializer(), null);

        private string GetPreviewContextId()
        {
            if (_currentAsset != null && !string.IsNullOrEmpty(_currentAsset.BlueprintId))
                return _currentAsset.BlueprintId!;
            return $"unsaved:{GetInstanceID()}";
        }

        // ── Undo/Redo 回调（使用语义化方法）──
        private void OnUndoRedoPerformed() => _session?.NotifyUndoRedo();

        private UndoPropertyModification[] OnUndoPostprocessModifications(UndoPropertyModification[] mods)
        {
            if (_session == null || mods == null || mods.Length == 0) return mods;
            var ids = new HashSet<string>();
            foreach (var m in mods) CollectChangedMarkerIds(m, ids);
            _session.NotifyUndoMarkerModified(ids);
            return mods;
        }

        private static void CollectChangedMarkerIds(UndoPropertyModification mod, ISet<string> ids)
        {
            var target = mod.currentValue.target;
            if (target is SceneMarker m && !string.IsNullOrEmpty(m.MarkerId))
                ids.Add(m.MarkerId);
            else if (target is Transform t)
            {
                var mc = t.GetComponent<SceneMarker>();
                if (mc != null && !string.IsNullOrEmpty(mc.MarkerId)) ids.Add(mc.MarkerId);
            }
            if (mod.currentValue.target is SceneMarker && mod.currentValue.propertyPath == "_markerId")
            {
                var old = mod.previousValue.value ?? "";
                var @new = mod.currentValue.value ?? "";
                if (!string.IsNullOrEmpty(old)) ids.Add(old);
                if (!string.IsNullOrEmpty(@new)) ids.Add(@new);
            }
        }
    }
}
