#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NodeGraph.Commands;
using NodeGraph.Core;
using NodeGraph.Math;
using NodeGraph.Serialization;
using NodeGraph.Unity;
using NodeGraph.View;
using SceneBlueprint.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.Preview;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Editor.WindowServices;
using SceneBlueprint.Editor.WindowServices.Binding;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.Session
{
    /// <summary>
    /// 蓝图编辑器会话——持有所有图状态和服务，实现 IBlueprintEditorContext。
    /// <para>
    /// Window 仅持有会话引用；会话通过 <see cref="IWindowCallbacks"/> 触发窗口副作用，
    /// 不直接依赖 EditorWindow，因此可在无窗口的测试环境中独立构造。
    /// </para>
    /// <para>生命周期：<c>new BlueprintEditorSession(...)</c> → 使用 → <c>Dispose()</c></para>
    /// </summary>
    internal sealed class BlueprintEditorSession : IDisposable
    {
        private readonly IWindowCallbacks             _callbacks;
        private readonly SceneBlueprintToolContext    _toolContext;
        private readonly ISceneBindingStore           _bindingStore;
        private readonly IEditorSpatialModeDescriptor _spatialDescriptor;
        private readonly Action                       _onBlueprintSelectionChanged;

        // ── NodeGraph 核心组件 ──
        public GraphViewModel       ViewModel        { get; private set; } = null!;
        public UnityGraphRenderer   Renderer         { get; private set; } = null!;
        public UnityPlatformInput   Input            { get; private set; } = null!;
        public UnityEditContext     EditContext       { get; private set; } = null!;
        public CanvasCoordinateHelper CoordinateHelper { get; private set; } = null!;
        public BlueprintProfile     Profile          { get; private set; } = null!;

        // ── Inspector 面板 ──
        public InspectorPanel            InspectorPanel  { get; private set; } = null!;
        public ActionNodeInspectorDrawer InspectorDrawer { get; private set; } = null!;

        private BindingContext _bindingContext = null!;
        private ActionRegistry _actionRegistry = null!;
        public BindingContext BindingContextPublic => _bindingContext;
        public ActionRegistry ActionRegistry       => _actionRegistry;

        // ── 资产状态（A1：下沉为 Session 一级字段）──
        public  BlueprintAsset? CurrentAsset { get; private set; }

        /// <summary>资产变更时触发（保存/创建新文件后）。订阅者（通常是 Window）同步自身的 _currentAsset 字段。</summary>
        public  event Action<BlueprintAsset?>? OnAssetChanged;

        /// <summary>内部更新当前资产并通知订阅者。由 Operations.cs 的保存/加载操作调用。</summary>
        public  void SetAsset(BlueprintAsset? asset)
        {
            CurrentAsset = asset;
            OnAssetChanged?.Invoke(asset);
        }

        // ── 分析状态 ──
        public  AnalysisReport? LastAnalysisReport { get; private set; }
        public  string          LastExportTime     { get; private set; } = "";

        // ── WindowServices ──
        public BlueprintAnalysisController     AnalysisCtrl      { get; private set; } = null!;
        public NodePreviewScheduler            PreviewScheduler  { get; private set; } = null!;
        public SceneBindingRestorer  BindingRestorer  { get; private set; } = null!;
        public SceneBindingCollector BindingCollector { get; private set; } = null!;
        public SceneBindingValidator BindingValidator { get; private set; } = null!;
        public BlackboardVariableEditorService BlackboardService { get; private set; } = null!;
        public BlueprintExportService          ExportService     { get; private set; } = null!;
        public SelectionMarkerResolver         SelectionResolver { get; private set; } = null!;
        public EditorDirtyScheduler            DirtyScheduler    { get; private set; } = null!;
        public SubGraphController              SubGraphCtrl      { get; private set; } = null!;
        private BlueprintPreviewManager _previewManager = null!;
        private string _previewKey = "";

        // ── 只读视图（P1：SessionReadView struct 替换委托层）──
        public IBlueprintEditorContext Context { get; private set; } = null!;

        // ── ISessionService 生命周期追踪 + 显式服务注册表 ──
        private readonly System.Collections.Generic.List<ISessionService>         _managedServices = new();
        private readonly System.Collections.Generic.Dictionary<Type, ISessionService> _services    = new();

        // ═══════════════════════════════════════════════════════════
        //  构造器
        // ═══════════════════════════════════════════════════════════

        public BlueprintEditorSession(
            Graph?                           existingGraph,
            IEditorSpatialModeDescriptor     spatialDescriptor,
            SceneBlueprintToolContext        toolContext,
            ISceneBindingStore               bindingStore,
            IWindowCallbacks                 callbacks,
            Action                           onBlueprintSelectionChanged,
            Action<string, ActionNodeData>   onNodePropertyChanged,
            Action<Vec2>                     onCanvasContextMenu,
            Action<Node, Vec2>               onNodeContextMenu,
            Action<NodeGraph.Core.Port, Vec2> onPortContextMenu)
        {
            _callbacks = callbacks; _toolContext = toolContext;
            _bindingStore = bindingStore; _spatialDescriptor = spatialDescriptor;
            _onBlueprintSelectionChanged = onBlueprintSelectionChanged;

            // 回滚操作列表：构造期间异常时逆序执行，防止资源泄漏
            var rollback = new System.Collections.Generic.List<Action>();
            try
            {
                var graph = InitCore(existingGraph, rollback);
                InitAdapters(rollback);
                InitInspector(onNodePropertyChanged, rollback);
                Context = new SessionReadView(this, _callbacks, _spatialDescriptor);
                InitServices(graph, rollback);

                ViewModel.OnContextMenuRequested     = onCanvasContextMenu;
                ViewModel.OnNodeContextMenuRequested = onNodeContextMenu;
                ViewModel.OnPortContextMenuRequested = onPortContextMenu;

                _toolContext.EnableMarkerTool(_actionRegistry, _spatialDescriptor);
                rollback.Add(() => _toolContext.DisableMarkerTool());

                SubscribeAll();
                rollback.Add(UnsubscribeAll);

                UpdateTitle();
                if (graph.Nodes.Count > 0) PreviewScheduler.MarkDirtyAll("SessionCreated");
                ScheduleAnalysis();
            }
            catch
            {
                // 逐步逆序清理已建立的资源
                rollback.Reverse();
                foreach (var cleanup in rollback)
                {
                    try { cleanup(); }
                    catch (Exception ex) { UnityEngine.Debug.LogError($"[Session] 构造回滚失败: {ex.Message}"); }
                }
                throw;
            }
        }

        private Graph InitCore(Graph? existingGraph, System.Collections.Generic.List<Action> rollback)
        {
            var settings = existingGraph?.Settings ?? new GraphSettings { Topology = GraphTopologyPolicy.DAG };
            settings.Behavior.ConnectionPolicy = new DefaultConnectionPolicy(new DataTypeRegistryValidator());
            // P1-B: Create 不再需要 NodeTypeRegistry，返回 catalog 同时设入 settings.NodeTypes
            var (profile, builtRegistry, catalog) = SceneBlueprintProfile.Create(new UnityTextMeasurer());
            settings.NodeTypes = catalog;
            Profile = profile; _actionRegistry = builtRegistry;
            var graph = existingGraph ?? new Graph(settings);
            ViewModel = new GraphViewModel(graph, Profile.BuildRenderConfig())
            {
                NodeTypeCatalog = catalog
            };
            return graph;
        }

        private void InitAdapters(System.Collections.Generic.List<Action> _)
        {
            Input = new UnityPlatformInput(); EditContext = new UnityEditContext();
            CoordinateHelper = new CanvasCoordinateHelper();
            Renderer = new UnityGraphRenderer(ViewModel.RenderConfig.ContentRenderers, ViewModel.RenderConfig.EdgeLabelRenderer);
        }

        private void InitInspector(Action<string, ActionNodeData> onNodePropertyChanged,
            System.Collections.Generic.List<Action> _)
        {
            _bindingContext = new BindingContext();
            InspectorDrawer = new ActionNodeInspectorDrawer(_actionRegistry);
            InspectorPanel  = new InspectorPanel(InspectorDrawer);
            InspectorDrawer.SetBindingContext(_bindingContext);
            InspectorDrawer.SetGraph(ViewModel.Graph);
            InspectorDrawer.OnPropertyChanged = onNodePropertyChanged;
        }

        private void InitServices(Graph graph, System.Collections.Generic.List<Action> rollback)
        {
            var ctx = Context;
            _previewManager   = new BlueprintPreviewManager();
            _previewKey       = GetHashCode().ToString();
            BlueprintPreviewManager.Register(_previewKey, _previewManager);
            rollback.Add(() => { BlueprintPreviewManager.Unregister(_previewKey); _previewManager.ClearAllPreviews(); });
            DirtyScheduler    = Track(new EditorDirtyScheduler(OnDirtyFlush));
            AnalysisCtrl      = Track(new BlueprintAnalysisController(ctx, ctx, () => Profile));
            PreviewScheduler  = Track(new NodePreviewScheduler(ctx, ctx, GetPreviewContextId, _previewManager));
            BindingRestorer  = new SceneBindingRestorer(ctx, _bindingContext, _bindingStore);
            BindingCollector = new SceneBindingCollector(ctx, _bindingContext, _bindingStore);
            BindingValidator = new SceneBindingValidator(ctx);
            BlackboardService  = Track(new BlackboardVariableEditorService(ctx));
            ExportService      = Track(new BlueprintExportService(
                ctx, ctx,
                BindingCollector,
                AnalysisCtrl,
                t => { LastExportTime = t; _callbacks.SetExportTime(t); }));
            SelectionResolver  = Track(new SelectionMarkerResolver(ctx, _bindingContext));
            SubGraphCtrl       = Track(new SubGraphController(ctx, ctx, _callbacks));
            // 回滚：逆序通知所有 ISessionService 清理自身（事件反订阅等）
            rollback.Add(() =>
            {
                var svcs = new System.Collections.Generic.List<ISessionService>(_managedServices);
                svcs.Reverse();
                foreach (var s in svcs) s.OnSessionDisposed();
                _managedServices.Clear();
            });
            PreviewScheduler.SyncGraphShapeSnapshot(graph);
            PreviewScheduler.RebuildMarkerNodeIndex();
        }

        private T Track<T>(T service)
        {
            if (service is ISessionService ss)
            {
                _managedServices.Add(ss);
                _services[typeof(T)] = ss;
            }
            return service;
        }

        /// <summary>按类型查找服务（调用方知道确切类型时使用）。</summary>
        public T GetService<T>() where T : class, ISessionService
            => (T)_services[typeof(T)];

        /// <summary>按类型尝试查找服务，不存在时返回 null。</summary>
        public bool TryGetService<T>(out T? service) where T : class, ISessionService
        {
            if (_services.TryGetValue(typeof(T), out var raw) && raw is T typed)
            { service = typed; return true; }
            service = null; return false;
        }

        // ═══════════════════════════════════════════════════════════
        //  公开 API（供 Window 调用）
        // ═══════════════════════════════════════════════════════════

        /// <summary>序列化当前图为 JSON（供 Window 的 SaveBlueprint 使用）</summary>
        public string SerializeGraph()
        {
            var serializer = CreateGraphSerializer();
            return serializer.Serialize(ViewModel.Graph);
        }

        public void CenterView()
        {
            var winSize = _callbacks.GetWindowSize();
            if (ViewModel.Graph.Nodes.Count == 0)
            {
                ViewModel.PanOffset = new Vec2(winSize.x / 2f, winSize.y / 2f);
                ViewModel.ZoomLevel = 1f;
            }
            else
            {
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                foreach (var node in ViewModel.Graph.Nodes)
                {
                    if (node.Position.X < minX) minX = node.Position.X;
                    if (node.Position.Y < minY) minY = node.Position.Y;
                    if (node.Position.X + node.Size.X > maxX) maxX = node.Position.X + node.Size.X;
                    if (node.Position.Y + node.Size.Y > maxY) maxY = node.Position.Y + node.Size.Y;
                }
                float cx = (minX + maxX) / 2f, cy = (minY + maxY) / 2f;
                ViewModel.PanOffset = new Vec2(
                    winSize.x / 2f - cx * ViewModel.ZoomLevel,
                    winSize.y / 2f - cy * ViewModel.ZoomLevel);
            }
            _callbacks.Repaint();
        }

        public void AddDefaultNodes()
        {
            ViewModel.Commands.Execute(new NodeGraph.Commands.AddNodeCommand("Flow.Start", new Vec2(100, 100)));
            ViewModel.Commands.Execute(new NodeGraph.Commands.AddNodeCommand("Flow.End",   new Vec2(400, 100)));
            ViewModel.RequestRepaint();
        }

        public void RestoreBindingsFromScene() => BindingRestorer.RestoreFromScene();
        public void RunBindingValidation()      => BindingValidator.RunValidation();
        public void SyncToScene()               => BindingCollector.SyncToScene();

        // ── 语义化预览通知 API（Fix-3：收窄对外暴露的 API 面）──

        public void NotifyNodePropertyChanged(string nodeId, ActionNodeData nodeData)
        {
            PreviewScheduler.NotifyNodeDataChanged(nodeId, nodeData);
            PreviewScheduler.MarkDirtyForNode(nodeId, "NodePropertyChanged");
            BlackboardService.MarkVariablesDirty();
            SelectionResolver.InvalidateCache();
        }

        public void NotifyNodesDeleted(IList<string> ids)
        {
            PreviewScheduler.MarkDirtyForNodes(ids, "Input.DeleteKey");
        }

        public void NotifyHierarchyChanged()
        {
            var previewIds = _previewManager.GetCurrentPreviewMarkerIds();
            int matched = 0;
            if (previewIds.Count > 0)
            {
                var changed = PreviewScheduler.CollectChangedPreviewMarkerIds(previewIds) ?? new List<string>();
                if (changed.Count > 0)
                {
                    matched = PreviewScheduler.MarkDirtyForNodesByAreaMarkerIds(changed, "HierarchyChanged.MarkerChanged");
                    if (matched == 0) matched = PreviewScheduler.MarkDirtyForAllRandomAreaNodes("HierarchyChanged.MarkerChangedFallback");
                }
            }
            int uncached = PreviewScheduler.MarkDirtyForUncachedRandomAreaNodes("HierarchyChanged.UncachedRandomArea");
            if (previewIds.Count == 0 && matched == 0 && uncached == 0)
                PreviewScheduler.MarkDirtyForAllRandomAreaNodes("HierarchyChanged.RandomAreaFallback");
        }

        public void NotifyUndoRedo() => PreviewScheduler.MarkDirtyAll("UndoRedo");

        public void NotifyUndoMarkerModified(ISet<string> markerIds)
        {
            if (markerIds.Count == 0) return;
            int matched = PreviewScheduler.MarkDirtyForNodesByAreaMarkerIds(markerIds, "Undo.PostprocessMarker");
            if (matched == 0) PreviewScheduler.MarkDirtyForUncachedRandomAreaNodes("Undo.PostprocessMarker.UncachedFallback");
        }

        public void MarkPreviewDirtyAll(string reason)      => PreviewScheduler.MarkDirtyAll(reason);
        public void DetectPreviewGraphShapeChange()         => PreviewScheduler.DetectGraphShapeChange();

        // ── 分析 ──
        public void ScheduleAnalysis() => DirtyScheduler.MarkDirty(EditorDirtyScheduler.DirtyFlag.Analysis);

        public AnalysisReport ForceRunAnalysis()
        {
            var report = AnalysisCtrl.ForceRunNow() ?? AnalysisReport.Empty;
            LastAnalysisReport = report;
            return report;
        }

        public void OnCommandExecutedForAnalysis(NodeGraph.Commands.ICommand cmd)
        {
            SelectionResolver.InvalidateCache();
            if (cmd is not NodeGraph.Commands.IStructuralCommand) return;
            ScheduleAnalysis();
            BlackboardService.MarkVariablesDirty();
        }
        public void OnGraphHistoryChangedForAnalysis() => ScheduleAnalysis();

        private void OnDirtyFlush(EditorDirtyScheduler.DirtyFlag flags)
        {
            if ((flags & EditorDirtyScheduler.DirtyFlag.Analysis) != 0)
                AnalysisCtrl.Schedule();
            if ((flags & EditorDirtyScheduler.DirtyFlag.Preview) != 0)
                PreviewScheduler.MarkDirtyAll("DirtyScheduler.Flush");
            if ((flags & EditorDirtyScheduler.DirtyFlag.Workbench) != 0)
                _callbacks.Repaint();
        }

        // ── 序列化工具 ──
        public JsonGraphSerializer CreateGraphSerializer(INodeTypeCatalog? typeProvider = null)
            => new JsonGraphSerializer(new ActionNodeDataSerializer(), typeProvider);

        /// <summary>基于当前 Profile 的 NodeTypes 构建反序列化用 TypeProvider</summary>
        public INodeTypeCatalog CreateProfileTypeProvider() => Profile.NodeTypes;

        /// <summary>Domain Reload 路径：Profile 尚未建立时从零构建</summary>
        public INodeTypeCatalog CreateTypeProvider()
        {
            var actionRegistry = SceneBlueprintProfile.CreateActionRegistry();
            return new ActionRegistryNodeTypeCatalog(actionRegistry);
        }

        // ═══════════════════════════════════════════════════════════
        //  私有辅助
        // ═══════════════════════════════════════════════════════════

        private string GetPreviewContextId()
        {
            if (CurrentAsset != null && !string.IsNullOrEmpty(CurrentAsset.BlueprintId))
                return CurrentAsset.BlueprintId!;
            return $"unsaved:{GetHashCode()}";
        }

        internal void UpdateTitle()
        {
            string name = CurrentAsset != null && !string.IsNullOrEmpty(CurrentAsset.BlueprintName)
                ? CurrentAsset.BlueprintName
                : "未保存";
            _callbacks.SetTitle($"场景蓝图编辑器 - {name}");
        }

        private void SubscribeAll()
        {
            ViewModel.Selection.OnSelectionChanged += _onBlueprintSelectionChanged;
            ViewModel.Commands.OnCommandExecuted   += OnCommandExecutedForAnalysis;
            ViewModel.Commands.OnHistoryChanged    += OnGraphHistoryChangedForAnalysis;
        }

        private void UnsubscribeAll()
        {
            ViewModel.Selection.OnSelectionChanged -= _onBlueprintSelectionChanged;
            ViewModel.Commands.OnCommandExecuted   -= OnCommandExecutedForAnalysis;
            ViewModel.Commands.OnHistoryChanged    -= OnGraphHistoryChangedForAnalysis;
        }

        // ═══════════════════════════════════════════════════════════
        //  IDisposable
        // ═══════════════════════════════════════════════════════════

        public void InvalidateActionRegistry()
        {
            _actionRegistry = SceneBlueprintProfile.CreateActionRegistry();
        }

        public void Dispose()
        {
            UnsubscribeAll();
            foreach (var s in _managedServices) s.OnSessionDisposed();
            _managedServices.Clear();
            PreviewScheduler.ResetState();
            _previewManager.ClearAllPreviews();
            BlueprintPreviewManager.Unregister(_previewKey);
        }
    }
}
