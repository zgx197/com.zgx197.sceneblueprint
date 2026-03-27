#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor
{
    public sealed class BlueprintNodeWorkspaceWindow : EditorWindow
    {
        private const float MinWindowWidth = 960f;
        private const float MinWindowHeight = 620f;
        private const double DefaultActiveRepaintWindowSeconds = 0.35;
        private const float DefaultFrameTimeMs = 16.67f;
        private const float FpsSmoothingFactor = 0.18f;
        private const double SamplingGapResetSeconds = 0.2d;

        [SerializeField] private string _nodeId = string.Empty;
        [SerializeField] private string _preferredWorkspaceId = string.Empty;
        [SerializeField] private double _activeRepaintUntil;
        [SerializeField] private double _lastEditorUpdateTimestamp;
        [SerializeField] private bool _hasEditorUpdateTimestamp;
        [SerializeField] private float _smoothedEditorUpdateFrameTimeMs = DefaultFrameTimeMs;
        [SerializeField] private float _smoothedEditorUpdateFps = 1000f / DefaultFrameTimeMs;

        public float SmoothedEditorUpdateFps => _smoothedEditorUpdateFps;

        public float SmoothedEditorUpdateFrameTimeMs => _smoothedEditorUpdateFrameTimeMs;

        [MenuItem("SceneBlueprint/节点工作台", priority = 261)]
        public static void OpenForCurrentSelection()
        {
            if (SceneBlueprintWindow.TryGetSelectedNodeWorkspaceContext(out var context))
            {
                ShowWindow(context.NodeId);
                return;
            }

            ShowWindow(string.Empty);
        }

        public static BlueprintNodeWorkspaceWindow ShowWindow(string nodeId, string preferredWorkspaceId = "")
        {
            var window = GetWindow<BlueprintNodeWorkspaceWindow>();
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
            window.Bind(nodeId, preferredWorkspaceId);
            window.Show();
            window.Focus();
            return window;
        }

        public void Bind(string nodeId, string preferredWorkspaceId = "")
        {
            _nodeId = nodeId ?? string.Empty;
            _preferredWorkspaceId = preferredWorkspaceId ?? string.Empty;
            titleContent = new GUIContent("节点工作台");
            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }

        public void RequestActiveRepaintWindow(double durationSeconds = DefaultActiveRepaintWindowSeconds)
        {
            var safeDurationSeconds = Math.Max(0.05d, durationSeconds);
            _activeRepaintUntil = Math.Max(
                _activeRepaintUntil,
                EditorApplication.timeSinceStartup + safeDurationSeconds);
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            wantsLessLayoutEvents = true;
            EditorApplication.update -= HandleEditorUpdate;
            EditorApplication.update += HandleEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);

            if (!TryResolveNodeContext(out var nodeContext))
            {
                DrawEmptyState();
                return;
            }

            if (!BlueprintNodeWorkspaceRegistry.TryResolve(nodeContext, _preferredWorkspaceId, out var provider))
            {
                DrawUnsupportedState(nodeContext);
                return;
            }

            titleContent = new GUIContent(provider.GetTitle(nodeContext));
            provider.Draw(new BlueprintNodeWorkspaceDrawContext(this, nodeContext), out var changed);
            if (changed)
            {
                SceneBlueprintWindow.NotifyWorkspaceNodeChanged(nodeContext.NodeId);
                Repaint();
            }
        }

        private void HandleEditorUpdate()
        {
            UpdateEditorUpdateFrameRateSample();

            if (focusedWindow != this)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup > _activeRepaintUntil)
            {
                return;
            }

            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }

        private void UpdateEditorUpdateFrameRateSample()
        {
            var now = EditorApplication.timeSinceStartup;
            if (_hasEditorUpdateTimestamp
                && now - _lastEditorUpdateTimestamp <= SamplingGapResetSeconds)
            {
                var deltaSeconds = Math.Max(0.0001d, now - _lastEditorUpdateTimestamp);
                var frameTimeMs = (float)(deltaSeconds * 1000.0d);
                _smoothedEditorUpdateFrameTimeMs = Mathf.Lerp(
                    _smoothedEditorUpdateFrameTimeMs,
                    frameTimeMs,
                    FpsSmoothingFactor);
                _smoothedEditorUpdateFps = 1000f / Mathf.Max(0.0001f, _smoothedEditorUpdateFrameTimeMs);
            }
            else
            {
                _smoothedEditorUpdateFrameTimeMs = DefaultFrameTimeMs;
                _smoothedEditorUpdateFps = 1000f / DefaultFrameTimeMs;
                _hasEditorUpdateTimestamp = true;
            }

            _lastEditorUpdateTimestamp = now;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    string.IsNullOrWhiteSpace(_nodeId) ? "当前未锁定节点" : _nodeId,
                    EditorStyles.toolbarTextField,
                    GUILayout.MinWidth(280f));
            }

            if (GUILayout.Button("使用当前选中节点", EditorStyles.toolbarButton, GUILayout.Width(110f))
                && SceneBlueprintWindow.TryGetSelectedNodeWorkspaceContext(out var selectedContext))
            {
                Bind(selectedContext.NodeId);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_nodeId)))
            {
                if (GUILayout.Button("定位节点", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    SceneBlueprintWindow.TryFocusNodeInEditor(_nodeId);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private bool TryResolveNodeContext(out BlueprintNodeWorkspaceContext context)
        {
            if (!string.IsNullOrWhiteSpace(_nodeId)
                && SceneBlueprintWindow.TryGetNodeWorkspaceContext(_nodeId, out context))
            {
                return true;
            }

            if (SceneBlueprintWindow.TryGetSelectedNodeWorkspaceContext(out context))
            {
                _nodeId = context.NodeId;
                return true;
            }

            context = default;
            return false;
        }

        private static void DrawEmptyState()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("节点工作台", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("请先打开场景蓝图编辑器，并选中一个支持复杂编辑的节点。", MessageType.Info);
            }
        }

        private static void DrawUnsupportedState(BlueprintNodeWorkspaceContext nodeContext)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("节点工作台", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    $"当前节点类型 {nodeContext.Data.ActionTypeId} 还没有注册专用工作台。",
                    MessageType.Info);
            }
        }
    }
}
