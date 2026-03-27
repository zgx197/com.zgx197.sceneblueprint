#nullable enable
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Knowledge;
using SceneBlueprint.Editor.Knowledge.ChatPanel;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        // ── AI 助手面板 ──
        private AIChatPanel? _chatPanel;
        private bool _showAIChat;

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

        // ── AI 助手面板（独立左侧面板）──

        private void DrawAIChatPanel(Rect panelRect)
        {
            EnsureChatPanel();
            GUILayout.BeginArea(panelRect);
            {
                EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
                _chatPanel?.Draw();
                EditorGUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        private void EnsureChatPanel()
        {
            if (_chatPanel != null) return;
            // KnowledgeService 已在 OnEnable → StartKnowledgeServer 中初始化
            _chatPanel = new AIChatPanel(KnowledgeService.Instance);
        }

        // ── 底部面板（分析报告）──

        private void DrawBottomPanel(Rect rect)
        {
            GUILayout.BeginArea(rect);

            // 工具栏：显示分析状态标签
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            bool hasReport = _session?.LastAnalysisReport != null;
            string analysisLabel = "分析";
            if (hasReport)
            {
                var rpt = _session!.LastAnalysisReport!;
                analysisLabel = rpt.HasErrors   ? $"分析 ✕{rpt.ErrorCount}"
                              : rpt.HasWarnings ? $"分析 △{rpt.WarningCount}"
                              : "分析 ✓";
            }
            var foldLabel = _collapseAnalysisPanel ? $"▸ {analysisLabel}" : $"▾ {analysisLabel}";
            if (GUILayout.Button(foldLabel, EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                _collapseAnalysisPanel = !_collapseAnalysisPanel;
                SaveWindowUiSettings();
                Repaint();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!_collapseAnalysisPanel)
            {
                DrawAnalysisSection();
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
            if (report == null)
            {
                EditorGUILayout.HelpBox("暂无分析报告。保存或修改蓝图后将自动生成。", MessageType.Info);
                return;
            }
            var vm = _session?.ViewModel;
            var prevColor = GUI.color;

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
