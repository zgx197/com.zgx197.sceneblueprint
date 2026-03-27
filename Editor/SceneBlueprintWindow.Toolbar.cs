#nullable enable
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Editor.Settings;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        // ── 工具栏 ──

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                NewGraph();
            }

            using (new EditorGUI.DisabledScope(_sessionState == SessionState.Suspended))
            {
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    SaveBlueprint();
            }

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                LoadBlueprint();
            }

            GUILayout.Space(6);

            // 变量面板 Toggle
            bool wantVariables = GUILayout.Toggle(
                _showWorkbench,
                new GUIContent("变量", "显示/隐藏 Blackboard 变量面板"),
                EditorStyles.toolbarButton,
                GUILayout.Width(40));
            if (wantVariables != _showWorkbench)
            {
                _showWorkbench = wantVariables;
                // 变量和 AI 助手互斥占用左侧面板
                if (_showWorkbench)
                {
                    _showAIChat = false;
                }
                SaveWindowUiSettings();
                Repaint();
            }

            // AI 助手 Toggle
            bool wantAIChat = GUILayout.Toggle(
                _showAIChat,
                new GUIContent("AI 助手", "显示/隐藏 AI 对话面板"),
                EditorStyles.toolbarButton,
                GUILayout.Width(52));
            if (wantAIChat != _showAIChat)
            {
                _showAIChat = wantAIChat;
                // 变量和 AI 助手互斥占用左侧面板
                if (_showAIChat)
                {
                    _showWorkbench = false;
                }
                SaveWindowUiSettings();
                Repaint();
            }

            GUILayout.Space(6);

            if (GUILayout.Button("+ 子蓝图", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                var title = EditorInputDialog.Show("新建子蓝图", "请输入子蓝图名称：", "新子蓝图");
                if (!string.IsNullOrEmpty(title)) _session?.SubGraphCtrl.CreateEmpty(title);
            }

            if (GUILayout.Button("全部折叠", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _session?.SubGraphCtrl.CollapseAll(true);
            }

            if (GUILayout.Button("全部展开", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _session?.SubGraphCtrl.CollapseAll(false);
            }

            GUILayout.Space(6);

            if (GUILayout.Button("设置", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                Settings.SceneBlueprintSettingsHubWindow.ShowWindow();
            }

            GUILayout.Space(8);
            var prevAutosaveColor = GUI.color;
            GUI.color = GetAutosaveStatusColor();
            GUILayout.Label(
                new GUIContent(GetAutosaveStatusText(), GetAutosaveStatusTooltip()),
                EditorStyles.miniLabel,
                GUILayout.Width(118));
            GUI.color = prevAutosaveColor;

            GUILayout.FlexibleSpace();

            var vm = _session?.ViewModel;
            var bc = _session?.BindingContextPublic;
            var report = _session?.LastAnalysisReport;
            if (vm != null)
            {
                int subGraphCount = vm.Graph.SubGraphFrames.Count;
                int nodeCount    = vm.Graph.Nodes.Count;
                int edgeCount    = vm.Graph.Edges.Count;
                string bindingInfo = bc != null ? $"绑定: {bc.BoundCount}/{bc.Count}" : "";

                string statusText = subGraphCount > 0
                    ? $"子蓝图: {subGraphCount}  节点: {nodeCount}  连线: {edgeCount}  {bindingInfo}"
                    : $"节点: {nodeCount}  连线: {edgeCount}  {bindingInfo}";

                if (_sessionState == SessionState.Suspended)
                {
                    var sceneName = System.IO.Path.GetFileNameWithoutExtension(_anchoredScenePath);
                    var prevColor = GUI.color;
                    GUI.color = new Color(1f, 0.6f, 0.2f);
                    GUILayout.Label($"⚠ 场景已切换，绑定已挂起。切回 [{sceneName}] 可恢复。",
                        EditorStyles.miniLabel);
                    GUI.color = prevColor;
                }
                else if (report != null)
                {
                    string analysisStatus = report.HasErrors   ? $"  ✕ {report.ErrorCount}错误"
                                          : report.HasWarnings ? $"  △ {report.WarningCount}警告"
                                          : "  ✓ 通过";
                    var prevColor = GUI.color;
                    GUI.color = report.HasErrors   ? new Color(1f, 0.4f, 0.4f)
                              : report.HasWarnings ? new Color(1f, 0.8f, 0.2f)
                              : new Color(0.4f, 0.9f, 0.4f);
                    GUILayout.Label(statusText + analysisStatus, EditorStyles.miniLabel);
                    GUI.color = prevColor;
                }
                else
                {
                    GUILayout.Label(statusText, EditorStyles.miniLabel);
                }
            }

            GUILayout.Space(10);

            // ── 关卡 ID（toggle 编辑模式） ──
            DrawLevelIdSection();

            GUILayout.Space(4);

            // ── 导出 ──
            using (new EditorGUI.DisabledScope(_sessionState == SessionState.Suspended))
            {
                if (GUILayout.Button("导出", EditorStyles.toolbarButton, GUILayout.Width(40)))
                {
                    if (_session != null)
                    {
                        if (_session.LevelId <= 0)
                        {
                            EditorUtility.DisplayDialog("导出提示",
                                "请先设置关卡 ID（点击工具栏右侧的「修改关卡」按钮）。", "确定");
                        }
                        else
                        {
                            _session.ExportService.ExportBlueprint();
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(_lastExportTime))
            {
                GUILayout.Label($"↑{_lastExportTime}", EditorStyles.miniLabel, GUILayout.Width(58));
            }

            GUILayout.Space(6);

            GUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════════
        //  关卡 ID Toggle 编辑
        // ══════════════════════════════════════════════════════════

        /// <summary>是否处于关卡 ID 编辑模式</summary>
        private bool _editingLevelId;

        /// <summary>编辑中的临时值</summary>
        private int _editingLevelIdValue;

        /// <summary>
        /// 绘制关卡 ID 区域：只读标签 ↔ 可编辑输入框，通过 toggle 按钮切换。
        /// </summary>
        private void DrawLevelIdSection()
        {
            bool hasAsset = _session?.CurrentAsset != null;
            int currentLevelId = _session?.LevelId ?? 0;

            if (_editingLevelId && hasAsset)
            {
                // ── 编辑模式：输入框 + 确认按钮 ──
                EditorGUILayout.LabelField("关卡", GUILayout.Width(28));
                _editingLevelIdValue = EditorGUILayout.IntField(_editingLevelIdValue, GUILayout.Width(48));
                if (_editingLevelIdValue < 1) _editingLevelIdValue = 1;

                if (GUILayout.Button("确认关卡", EditorStyles.toolbarButton, GUILayout.Width(56)))
                {
                    if (_session != null)
                        _session.LevelId = _editingLevelIdValue;
                    _editingLevelId = false;
                }
            }
            else
            {
                // ── 只读模式：显示当前值 + 修改按钮 ──
                string labelText = currentLevelId > 0 ? $"关卡: {currentLevelId}" : "关卡: 未设置";

                var prevColor = GUI.color;
                if (currentLevelId <= 0 && hasAsset)
                    GUI.color = new Color(1f, 0.7f, 0.3f); // 未设置时橙色
                GUILayout.Label(labelText, EditorStyles.miniLabel);
                GUI.color = prevColor;

                using (new EditorGUI.DisabledScope(!hasAsset))
                {
                    if (GUILayout.Button("修改关卡", EditorStyles.toolbarButton, GUILayout.Width(56)))
                    {
                        _editingLevelId = true;
                        _editingLevelIdValue = currentLevelId > 0 ? currentLevelId : 1;
                    }
                }
            }
        }
    }
}
