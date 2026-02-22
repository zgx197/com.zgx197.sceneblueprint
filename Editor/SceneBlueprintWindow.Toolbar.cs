#nullable enable
using UnityEditor;
using UnityEngine;

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

            if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                SaveBlueprint();
            }

            if (GUILayout.Button("加载", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                LoadBlueprint();
            }

            GUILayout.Space(6);

            bool wantVariables = GUILayout.Toggle(
                _showWorkbench,
                new GUIContent("变量", "显示/隐藏 Blackboard 变量面板"),
                EditorStyles.toolbarButton,
                GUILayout.Width(40));
            if (wantVariables != _showWorkbench)
            {
                _showWorkbench = wantVariables;
                EditorPrefs.SetBool(WorkbenchVisiblePrefsKey, _showWorkbench);
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

                if (report != null)
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

            GUILayout.Space(6);

            if (GUILayout.Button("同步到场景", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                SyncToScene();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("导出", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                _session?.ExportService.ExportBlueprint();
            }

            if (!string.IsNullOrEmpty(_lastExportTime))
            {
                GUILayout.Label($"↑{_lastExportTime}", EditorStyles.miniLabel, GUILayout.Width(58));
            }

            GUILayout.Space(6);

            GUILayout.EndHorizontal();
        }
    }
}
