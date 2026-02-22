#nullable enable
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        // ── 工作台面板（Blackboard 变量）──

        private void DrawWorkbenchPanel(Rect panelRect)
        {
            GUILayout.BeginArea(panelRect);
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
                _session?.BlackboardService.DrawBlackboardPanel();
                EditorGUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        /// <summary>在给定 Rect 区域内绘制分析结果面板（带 BeginArea）。</summary>
        private void DrawAnalysisPanel(Rect rect)
        {
            GUILayout.BeginArea(rect);
            DrawAnalysisSection();
            GUILayout.EndArea();
        }

        private void DrawAnalysisSection()
        {
            var report = _session?.LastAnalysisReport;
            if (report == null) return;
            var vm = _session?.ViewModel;
            var prevColor = GUI.color;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.color = report.HasErrors   ? new Color(1f, 0.35f, 0.35f)
                      : report.HasWarnings ? new Color(1f, 0.8f, 0.2f)
                      : new Color(0.4f, 0.9f, 0.4f);
            string title = report.HasErrors
                ? $"分析  {report.ErrorCount} 错误  {report.WarningCount} 警告"
                : report.HasWarnings ? $"分析  {report.WarningCount} 警告"
                : "分析  ✓ 无问题";
            GUILayout.Label(title, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();

            _analysisScrollPos = EditorGUILayout.BeginScrollView(_analysisScrollPos, GUILayout.ExpandHeight(true));
            foreach (var d in report.Diagnostics)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = d.Severity == DiagnosticSeverity.Error   ? new Color(1f, 0.35f, 0.35f)
                          : d.Severity == DiagnosticSeverity.Warning ? new Color(1f, 0.75f, 0.2f)
                          : Color.white;
                string icon = d.Severity == DiagnosticSeverity.Error ? "✕"
                            : d.Severity == DiagnosticSeverity.Warning ? "△" : "ℹ";
                GUILayout.Label($"{icon} [{d.Code}]", EditorStyles.miniLabel, GUILayout.Width(70));
                GUI.color = prevColor;
                if (GUILayout.Button(d.Message, EditorStyles.miniLabel, GUILayout.ExpandWidth(true))
                    && d.NodeId != null && vm != null)
                {
                    vm.Selection.SelectMultiple(new[] { d.NodeId });
                    vm.RequestRepaint();
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        // ── 辅助方法（Editor 生命周期回调 + Registry）──

        private void OnEditorHierarchyChanged()
        {
            _session?.NotifyHierarchyChanged();
        }

        private void OnEditorProjectChanged()
        {
            _session?.InvalidateActionRegistry();
        }

        private bool HasSyncedSceneBindings()
        {
            if (_currentAsset == null)
                return false;

            return _sceneBindingStore.IsBoundToBlueprint(_currentAsset);
        }

        // ── 辅助方法（输入判断）──

        private static bool IsInputEvent(Event evt)
        {
            return evt.type == EventType.MouseDown
                || evt.type == EventType.MouseUp
                || evt.type == EventType.MouseDrag
                || evt.type == EventType.ScrollWheel
                || evt.type == EventType.KeyDown
                || evt.type == EventType.KeyUp
                || evt.type == EventType.ContextClick;
        }
    }
}
