#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
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
                EditorPrefs.SetBool(WorkbenchVisiblePrefsKey, true);
            }
        }
        void IWindowCallbacks.SetExportTime(string t)  => _lastExportTime = t;
        void IWindowCallbacks.SetTitle(string title)   => titleContent = new GUIContent(title);

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
        //  窗口级基础设施（不属于图状态，不随 Session 重建）
        // ══════════════════════════════════════════════════════════
        private IEditorSpatialModeDescriptor? _spatialModeDescriptor;
        private readonly SceneBlueprintToolContext _toolContext       = new SceneBlueprintToolContext();
        private readonly ISceneBindingStore        _sceneBindingStore = new SceneManagerBindingStore();

        // ══════════════════════════════════════════════════════════
        //  布局参数（UI 状态，持久化到 EditorPrefs）
        // ══════════════════════════════════════════════════════════
        private const float ToolbarHeight         = 22f;
        private const float MinInspectorWidth     = 200f;
        private const float MaxInspectorWidth     = 500f;
        private const float DefaultInspectorWidth = 280f;
        private const float MinWorkbenchWidth     = 220f;
        private const float MaxWorkbenchWidth     = 520f;
        private const float DefaultWorkbenchWidth = 300f;
        private const float SplitterWidth         = 4f;
        private const float MinCanvasWidth        = 260f;
        private const string WorkbenchVisiblePrefsKey = "SceneBlueprint.Workbench.Visible";
        private const string WorkbenchWidthPrefsKey   = "SceneBlueprint.Workbench.Width";
        private const string AnalysisHeightPrefsKey   = "SceneBlueprint.Analysis.Height";
        private const float MinAnalysisHeight     = 60f;
        private const float DefaultAnalysisHeight = 160f;
        private const float AnalysisSplitterHeight = 4f;

        private float _inspectorWidth  = DefaultInspectorWidth;
        private float _workbenchWidth  = DefaultWorkbenchWidth;
        private float _analysisHeight  = DefaultAnalysisHeight;
        private bool  _isDraggingSplitter;
        private bool  _isDraggingWorkbenchSplitter;
        private bool  _isDraggingAnalysisSplitter;
        private bool  _showWorkbench            = true;
        private bool  _useEditorToolSelectionInput = true;
        private string _lastExportTime          = "";
        private Vector2 _analysisScrollPos;

        // ══════════════════════════════════════════════════════════
        //  MenuItem
        // ══════════════════════════════════════════════════════════
        [MenuItem("SceneBlueprint/蓝图编辑器 &B")]
        public static void Open()
        {
            var window = GetWindow<SceneBlueprintWindow>();
            window.titleContent = new GUIContent("场景蓝图编辑器");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        // ══════════════════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            _spatialModeDescriptor = SpatialModeRegistry.GetProjectModeDescriptor();
            _showWorkbench         = EditorPrefs.GetBool(WorkbenchVisiblePrefsKey, true);
            _workbenchWidth        = EditorPrefs.GetFloat(WorkbenchWidthPrefsKey,  DefaultWorkbenchWidth);
            _analysisHeight        = EditorPrefs.GetFloat(AnalysisHeightPrefsKey,  DefaultAnalysisHeight);
            _useEditorToolSelectionInput = MarkerSelectionInputRoutingSettings.LoadUseEditorTool();
            _toolContext.Attach(_useEditorToolSelectionInput);
            GizmoRenderPipeline.SetInteractionMode(GizmoRenderPipeline.MarkerInteractionMode.Edit);

            // 窗口级 Unity 事件订阅（生命周期同 Window，不随 Session 重建）
            EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
            EditorApplication.projectChanged   += OnEditorProjectChanged;
            Undo.undoRedoPerformed             += OnUndoRedoPerformed;
            Undo.postprocessModifications      += OnUndoPostprocessModifications;
            SceneMarkerSelectionBridge.OnHighlightNodesForMarkerRequested += OnSceneMarkerSelected;
            SceneMarkerSelectionBridge.OnFrameNodeForMarkerRequested      += OnSceneMarkerDoubleClicked;
            Selection.selectionChanged                                    += OnUnitySelectionChanged;

            if (!string.IsNullOrEmpty(_graphJsonBeforeReload))
                TryRestoreAfterDomainReload();
            else
                CreateSessionIfNeeded();

            if (_session != null && _session.ViewModel.Graph.Nodes.Count > 0)
                _session.MarkPreviewDirtyAll("OnEnable");
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(WorkbenchVisiblePrefsKey,  _showWorkbench);
            EditorPrefs.SetFloat(WorkbenchWidthPrefsKey,   _workbenchWidth);
            EditorPrefs.SetFloat(AnalysisHeightPrefsKey,   _analysisHeight);
            MarkerSelectionInputRoutingSettings.SaveUseEditorTool(_useEditorToolSelectionInput);

            // 窗口级事件取消订阅
            EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
            EditorApplication.projectChanged   -= OnEditorProjectChanged;
            Undo.undoRedoPerformed             -= OnUndoRedoPerformed;
            Undo.postprocessModifications      -= OnUndoPostprocessModifications;
            SceneMarkerSelectionBridge.OnHighlightNodesForMarkerRequested -= OnSceneMarkerSelected;
            SceneMarkerSelectionBridge.OnFrameNodeForMarkerRequested      -= OnSceneMarkerDoubleClicked;
            Selection.selectionChanged                                    -= OnUnitySelectionChanged;
            SceneMarkerSelectionBridge.ClearHighlight();

            _toolContext.Detach();

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
                _sceneBindingStore,
                this,
                OnBlueprintSelectionChanged,
                OnNodePropertyChanged,
                OnCanvasContextMenu,
                OnNodeContextMenu,
                OnPortContextMenu);
            // A1: Session 持有资产真相，Window 通过事件同步自身的 [SerializeField] 字段
            _session.OnAssetChanged += asset => _currentAsset = asset;
            _session.SetAsset(_currentAsset);
        }

        private void DisposeSession()
        {
            _session?.Dispose();
            _session = null;
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
            var layout = CalculateWindowLayout();

            DrawToolbar();

            if (_showWorkbench) HandleWorkbenchSplitter(layout.WorkbenchSplitterRect, evt);
            HandleSplitter(layout.SplitterRect, evt, layout.WorkbenchRect.width);
            if (_showWorkbench) DrawWorkbenchPanel(layout.WorkbenchRect);

            ch.SetGraphAreaRect(layout.GraphRect);
            var viewport = new Rect2(0, 0, layout.GraphRect.width, layout.GraphRect.height);
            inp.Update(evt, ch);

            if (evt.type == EventType.Repaint)
            {
                if (_showWorkbench)
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

            // Inspector + Analysis 竖向分割
            _session.InspectorDrawer.SetVariableDeclarations(
                _session.BlackboardService.BuildCombinedVariables());

            bool hasAnalysis = _session.LastAnalysisReport != null;
            var inspectorRect        = layout.InspectorRect;
            Rect inspContentRect     = inspectorRect;
            Rect analysisSplitterBarRect = default;
            Rect analysisContentRect = default;
            if (hasAnalysis)
            {
                float clampedH = Mathf.Clamp(_analysisHeight, MinAnalysisHeight, inspectorRect.height * 0.6f);
                _analysisHeight = clampedH;
                float splitterY = inspectorRect.yMax - clampedH - AnalysisSplitterHeight;
                inspContentRect         = new Rect(inspectorRect.x, inspectorRect.y,     inspectorRect.width, Mathf.Max(40f, splitterY - inspectorRect.y));
                analysisSplitterBarRect = new Rect(inspectorRect.x, splitterY,           inspectorRect.width, AnalysisSplitterHeight);
                analysisContentRect     = new Rect(inspectorRect.x, splitterY + AnalysisSplitterHeight, inspectorRect.width, clampedH);
                HandleAnalysisSplitter(analysisSplitterBarRect, evt, inspectorRect);
            }

            if (ip.Draw(inspContentRect, vm))
                vm.RequestRepaint();

            if (hasAnalysis)
            {
                if (evt.type == EventType.Repaint)
                    EditorGUI.DrawRect(analysisSplitterBarRect, new Color(0.15f, 0.15f, 0.15f, 1f));
                DrawAnalysisPanel(analysisContentRect);
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
