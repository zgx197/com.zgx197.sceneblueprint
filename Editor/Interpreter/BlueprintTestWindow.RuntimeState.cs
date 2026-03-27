#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Interpreter;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Editor.Interpreter
{
    public partial class BlueprintTestWindow
    {
        [SerializeField] private bool _showRuntimeStates = true;
        [SerializeField] private bool _showRuntimeSnapshots = true;
        [SerializeField] private string _selectedRuntimeStateKey = string.Empty;
        [SerializeField] private string _runtimeStateFilterText = string.Empty;
        [SerializeField] private string _selectedRuntimeSnapshotId = string.Empty;
        [SerializeField] private string _selectedRuntimeSnapshotEntryKey = string.Empty;
        [SerializeField] private string _runtimeSnapshotFilterText = string.Empty;
        [SerializeField] private string _runtimeSnapshotEntryFilterText = string.Empty;
        private Vector2 _runtimeStateListScroll;
        private Vector2 _runtimeStateDetailScroll;
        private Vector2 _runtimeSnapshotListScroll;
        private Vector2 _runtimeSnapshotEntryListScroll;
        private Vector2 _runtimeSnapshotEntryDetailScroll;
        private readonly RuntimeStateHistoryStore _runtimeStateHistory = new(600);
        private readonly List<RuntimeSnapshot> _runtimeSnapshots = new();
        private const int RuntimeSnapshotCapacity = 16;

        private static readonly RuntimeStatePresenterRegistry RuntimeStatePresenters = RuntimeStatePresenterRegistry.Default;
        private static GUIStyle? s_runtimeStateListButtonStyle;
        private static GUIStyle? s_runtimeStateSelectedButtonStyle;
        private static GUIStyle? s_runtimeStateDetailLabelStyle;

        private static GUIStyle RuntimeStateListButtonStyle => s_runtimeStateListButtonStyle ??= CreateRuntimeStateListButtonStyle(isSelected: false);

        private static GUIStyle RuntimeStateSelectedButtonStyle => s_runtimeStateSelectedButtonStyle ??= CreateRuntimeStateListButtonStyle(isSelected: true);

        private static GUIStyle RuntimeStateDetailLabelStyle => s_runtimeStateDetailLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            richText = false
        };

        private void DrawRuntimeStatePanel(StateViewMode viewMode)
        {
            var host = _runner?.GetService<RuntimeStateHost>();
            const string foldoutLabel = "运行时状态 Inspector";

            _showRuntimeStates = EditorGUILayout.Foldout(_showRuntimeStates, foldoutLabel, true);
            if (!_showRuntimeStates)
            {
                return;
            }

            if (host == null)
            {
                EditorGUILayout.HelpBox("当前 Runner 尚未提供 RuntimeStateHost，无法读取结构化运行时状态。", MessageType.Info);
                return;
            }

            RuntimeStatePresentationResult presentationResult;
            try
            {
                if (viewMode == StateViewMode.History)
                {
                    var inspectTick = _inspectTick >= 0 ? _inspectTick : _debugCtrl?.InspectTick ?? -1;
                    if (!_runtimeStateHistory.TryGetRecord(inspectTick, out var record) || record == null)
                    {
                        EditorGUILayout.HelpBox($"当前未记录 Tick={inspectTick} 的运行时状态历史。请先单步执行或重新运行带历史记录的流程。", MessageType.Info);
                        DrawRuntimeSnapshotPanel(host);
                        return;
                    }

                    presentationResult = record.PresentationResult;
                }
                else
                {
                    var observation = host.Inspector.Inspect(ObservationRequest.ForHost(
                        includeChildren: true));
                    presentationResult = RuntimeStatePresenters.BuildPresentations(observation, _runner?.Frame);
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"读取 RuntimeState Observation 失败：{ex.Message}", MessageType.Warning);
                return;
            }

            if (presentationResult.SupportedEntryCount == 0)
            {
                EditorGUILayout.LabelField(
                    $"已接入 {presentationResult.SupportedEntryCount} / {presentationResult.TotalEntryCount} 个 runtime 条目",
                    EditorStyles.miniLabel);
                EditorGUILayout.HelpBox("当前没有已接入摘要渲染的运行时状态类型。", MessageType.None);
                DrawRuntimeSnapshotPanel(host);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"已接入 {presentationResult.SupportedEntryCount} / {presentationResult.TotalEntryCount} 个 runtime 条目",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                _runtimeStateFilterText = EditorGUILayout.TextField(
                    "筛选",
                    _runtimeStateFilterText,
                    GUILayout.Width(Mathf.Min(280f, position.width * 0.42f)));
            }

            var panelModel = RuntimeStateInspectorPanelBuilder.Build(
                presentationResult,
                _runtimeStateFilterText,
                _selectedRuntimeStateKey);
            _selectedRuntimeStateKey = panelModel.SelectedLogicalEntryKey;

            if (!panelModel.HasVisiblePresentations || panelModel.SelectedPresentation == null)
            {
                EditorGUILayout.HelpBox("当前筛选条件下没有可显示的运行时状态。", MessageType.Info);
                DrawRuntimeSnapshotPanel(host);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(240f, position.width * 0.42f))))
                {
                    _runtimeStateListScroll = EditorGUILayout.BeginScrollView(
                        _runtimeStateListScroll,
                        GUILayout.MinHeight(110f),
                        GUILayout.MaxHeight(220f));

                    for (var index = 0; index < panelModel.VisiblePresentations.Count; index++)
                    {
                        DrawRuntimeStateSummaryButton(panelModel.VisiblePresentations[index].Summary);
                    }

                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
                {
                    DrawRuntimeStateDetail(panelModel.SelectedPresentation.Detail);
                }
            }

            DrawRuntimeSnapshotPanel(host);
        }

        private void DrawRuntimeStateSummaryButton(RuntimeStateSummaryViewModel summary)
        {
            var isSelected = string.Equals(_selectedRuntimeStateKey, summary.LogicalEntryKey, StringComparison.Ordinal);
            var phaseText = summary.Phase?.ToString() ?? "Unknown";
            var title = string.IsNullOrWhiteSpace(summary.Title) ? "(未命名状态)" : summary.Title;
            var subtitle = string.IsNullOrWhiteSpace(summary.Subtitle) ? "无补充信息" : summary.Subtitle;
            var summaryText = string.IsNullOrWhiteSpace(summary.SummaryText) ? "无摘要" : summary.SummaryText;
            var content = $"[{phaseText}] {title}\n{subtitle}\n{summaryText}";

            var previousColor = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.36f, 0.62f, 0.98f, 0.95f);
            }

            if (GUILayout.Button(
                content,
                isSelected ? RuntimeStateSelectedButtonStyle : RuntimeStateListButtonStyle,
                GUILayout.ExpandWidth(true)))
            {
                _selectedRuntimeStateKey = summary.LogicalEntryKey;
            }

            GUI.backgroundColor = previousColor;
        }

        private void DrawRuntimeStateDetail(RuntimeStateDetailViewModel detail)
        {
            EditorGUILayout.LabelField(detail.Title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(detail.Subtitle))
            {
                EditorGUILayout.LabelField(detail.Subtitle, EditorStyles.miniLabel);
            }

            if (!string.IsNullOrWhiteSpace(detail.SummaryText))
            {
                EditorGUILayout.HelpBox(detail.SummaryText, MessageType.None);
            }

            _runtimeStateDetailScroll = EditorGUILayout.BeginScrollView(
                _runtimeStateDetailScroll,
                GUILayout.MinHeight(110f),
                GUILayout.MaxHeight(220f));

            var currentSectionTitle = string.Empty;
            for (var index = 0; index < detail.Fields.Count; index++)
            {
                var field = detail.Fields[index];
                if (!string.Equals(currentSectionTitle, field.SectionTitle, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(field.SectionTitle))
                {
                    currentSectionTitle = field.SectionTitle;
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(currentSectionTitle, EditorStyles.miniBoldLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(field.Label, GUILayout.Width(86f));
                    EditorGUILayout.LabelField(field.Value, RuntimeStateDetailLabelStyle);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRuntimeSnapshotPanel(RuntimeStateHost host)
        {
            const string foldoutLabel = "运行时 Snapshot 浏览";
            _showRuntimeSnapshots = EditorGUILayout.Foldout(_showRuntimeSnapshots, foldoutLabel, true);
            if (!_showRuntimeSnapshots)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("捕获当前 Snapshot", GUILayout.Width(140f)))
                {
                    CaptureRuntimeSnapshot(host);
                }

                GUILayout.FlexibleSpace();
                _runtimeSnapshotFilterText = EditorGUILayout.TextField(
                    "筛选 Snapshot",
                    _runtimeSnapshotFilterText,
                    GUILayout.Width(Mathf.Min(280f, position.width * 0.42f)));
            }

            var panelModel = RuntimeSnapshotBrowserBuilder.Build(
                _runtimeSnapshots,
                _runtimeSnapshotFilterText,
                _selectedRuntimeSnapshotId,
                _runtimeSnapshotEntryFilterText,
                _selectedRuntimeSnapshotEntryKey);
            _selectedRuntimeSnapshotId = panelModel.SelectedSnapshotId;
            _selectedRuntimeSnapshotEntryKey = panelModel.SelectedEntryKey;

            if (panelModel.VisibleSnapshots.Count == 0)
            {
                EditorGUILayout.HelpBox("当前还没有捕获任何 Runtime Snapshot。", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(240f, position.width * 0.34f))))
                {
                    _runtimeSnapshotListScroll = EditorGUILayout.BeginScrollView(
                        _runtimeSnapshotListScroll,
                        GUILayout.MinHeight(90f),
                        GUILayout.MaxHeight(180f));

                    for (var index = 0; index < panelModel.VisibleSnapshots.Count; index++)
                    {
                        DrawRuntimeSnapshotButton(panelModel.VisibleSnapshots[index]);
                    }

                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(260f, position.width * 0.36f))))
                {
                    _runtimeSnapshotEntryFilterText = EditorGUILayout.TextField("筛选条目", _runtimeSnapshotEntryFilterText);
                    _runtimeSnapshotEntryListScroll = EditorGUILayout.BeginScrollView(
                        _runtimeSnapshotEntryListScroll,
                        GUILayout.MinHeight(90f),
                        GUILayout.MaxHeight(180f));

                    for (var index = 0; index < panelModel.VisibleEntries.Count; index++)
                    {
                        DrawRuntimeSnapshotEntryButton(panelModel.VisibleEntries[index]);
                    }

                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
                {
                    DrawRuntimeSnapshotEntryDetail(panelModel.SelectedEntry);
                }
            }
        }

        private void DrawRuntimeSnapshotButton(RuntimeSnapshotBrowserSnapshotViewModel snapshot)
        {
            var isSelected = string.Equals(_selectedRuntimeSnapshotId, snapshot.SnapshotId, StringComparison.Ordinal);
            var previousColor = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.36f, 0.62f, 0.98f, 0.95f);
            }

            if (GUILayout.Button(
                $"{snapshot.Title}\n{snapshot.SummaryText}",
                isSelected ? RuntimeStateSelectedButtonStyle : RuntimeStateListButtonStyle,
                GUILayout.ExpandWidth(true)))
            {
                _selectedRuntimeSnapshotId = snapshot.SnapshotId;
            }

            GUI.backgroundColor = previousColor;
        }

        private void DrawRuntimeSnapshotEntryButton(RuntimeSnapshotBrowserEntryViewModel entry)
        {
            var isSelected = string.Equals(_selectedRuntimeSnapshotEntryKey, entry.Entry.LogicalEntryKey, StringComparison.Ordinal);
            var previousColor = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.28f, 0.75f, 0.62f, 0.95f);
            }

            if (GUILayout.Button(
                $"{entry.Title}\n{entry.Subtitle}\n{entry.SummaryText}",
                isSelected ? RuntimeStateSelectedButtonStyle : RuntimeStateListButtonStyle,
                GUILayout.ExpandWidth(true)))
            {
                _selectedRuntimeSnapshotEntryKey = entry.Entry.LogicalEntryKey;
            }

            GUI.backgroundColor = previousColor;
        }

        private void DrawRuntimeSnapshotEntryDetail(RuntimeSnapshotBrowserEntryViewModel? entry)
        {
            if (entry == null)
            {
                EditorGUILayout.HelpBox("请选择一个 Snapshot 条目查看详情。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField(entry.Title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(entry.Subtitle, EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(entry.SummaryText, MessageType.None);

            _runtimeSnapshotEntryDetailScroll = EditorGUILayout.BeginScrollView(
                _runtimeSnapshotEntryDetailScroll,
                GUILayout.MinHeight(110f),
                GUILayout.MaxHeight(220f));

            DrawSnapshotDetailRow("条目 Key", entry.Entry.LogicalEntryKey);
            DrawSnapshotDetailRow("Domain", entry.Entry.DomainId.ToString());
            DrawSnapshotDetailRow("EntryRef", entry.Entry.EntryRef?.ToString() ?? string.Empty);
            DrawSnapshotDetailRow("导出模式", entry.Entry.ExportMode.ToString());
            DrawSnapshotDetailRow("恢复模式", entry.Entry.RestoreMode.ToString());
            DrawSnapshotDetailRow("载荷类型", entry.Entry.PayloadKind.ToString());
            DrawSnapshotDetailRow("载荷摘要", entry.PayloadSummary);
            var currentSectionTitle = string.Empty;
            for (var index = 0; index < entry.PayloadDetails.Count; index++)
            {
                if (!string.Equals(currentSectionTitle, entry.PayloadDetails[index].SectionTitle, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(entry.PayloadDetails[index].SectionTitle))
                {
                    currentSectionTitle = entry.PayloadDetails[index].SectionTitle;
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(currentSectionTitle, EditorStyles.miniBoldLabel);
                }

                DrawSnapshotDetailRow(entry.PayloadDetails[index].Label, entry.PayloadDetails[index].Value);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSnapshotDetailRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(86f));
                EditorGUILayout.LabelField(value, RuntimeStateDetailLabelStyle);
            }
        }

        private void CaptureRuntimeStateHistoryFrame(BlueprintRunner runner)
        {
            if (runner?.Frame == null)
            {
                return;
            }

            var host = runner.GetService<RuntimeStateHost>();
            if (host == null)
            {
                return;
            }

            try
            {
                var observation = host.Inspector.Inspect(ObservationRequest.ForHost(
                    includeChildren: true));
                var presentationResult = RuntimeStatePresenters.BuildPresentations(observation, runner.Frame);
                _runtimeStateHistory.Record(runner.Frame.TickCount, presentationResult);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[BlueprintTestWindow] 记录 Runtime State 历史失败: {ex.Message}");
            }
        }

        private void CaptureRuntimeSnapshot(RuntimeStateHost host)
        {
            try
            {
                var tick = _runner?.Frame?.TickCount ?? -1;
                var snapshot = host.Snapshot.Capture(SnapshotRequest.ForHost(
                    tag: tick >= 0 ? $"runtime@T{tick}" : "runtime"));
                _runtimeSnapshots.Add(snapshot);
                if (_runtimeSnapshots.Count > RuntimeSnapshotCapacity)
                {
                    _runtimeSnapshots.RemoveAt(0);
                }

                _selectedRuntimeSnapshotId = snapshot.SnapshotId;
                _selectedRuntimeSnapshotEntryKey = string.Empty;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[BlueprintTestWindow] 捕获 Runtime Snapshot 失败: {ex.Message}");
            }
        }
        private static GUIStyle CreateRuntimeStateListButtonStyle(bool isSelected)
        {
            return new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                richText = false,
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                fixedHeight = 0f,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 2, 2)
            };
        }
    }
}
