#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SceneBlueprint.Editor.Logging
{
    /// <summary>
    /// SceneBlueprint 日志查看窗口。
    /// <para>
    /// 功能：实时滚动、级别过滤、模块过滤、关键词搜索、导出到剪贴板、设置面板。
    /// </para>
    /// </summary>
    public class SBLogWindow : EditorWindow
    {
        [MenuItem("SceneBlueprint/日志窗口", priority = 200)]
        public static void ShowWindow()
        {
            var win = GetWindow<SBLogWindow>();
            win.titleContent = new GUIContent("SB Log");
            win.minSize = new Vector2(480, 300);
            win.Show();
        }

        // ─── 过滤状态 ───

        private string _searchKeyword = "";
        private SBLogLevel _filterLevel = SBLogLevel.Debug;
        private int _filterTagIndex = 0; // 0 = All
        private bool _autoScroll = true;
        private bool _showSettings = false;

        // ─── 缓存 ───

        private List<SBLogEntry> _filteredEntries = new();
        private bool _dirty = true;
        private Vector2 _scrollPos;
        private Vector2 _settingsScrollPos;

        // ─── Tag 下拉列表 ───

        private string[] _tagOptions = null!;
        private int _bufferCapacityInput;

        // ─── 样式（懒初始化） ───

        private GUIStyle? _logLineStyle;
        private GUIStyle? _debugStyle;
        private GUIStyle? _infoStyle;
        private GUIStyle? _warnStyle;
        private GUIStyle? _errorStyle;
        private GUIStyle? _toolbarSearchStyle;
        private GUIStyle? _toolbarCancelStyle;

        private void OnEnable()
        {
            SBLog.OnLogEntry += OnNewEntry;
            SBLog.Buffer.OnCleared += OnBufferCleared;
            _bufferCapacityInput = SBLogSettings.BufferCapacity;
            RebuildTagOptions();
            _dirty = true;
        }

        private void OnDisable()
        {
            SBLog.OnLogEntry -= OnNewEntry;
            SBLog.Buffer.OnCleared -= OnBufferCleared;
        }

        private void OnNewEntry(SBLogEntry entry)
        {
            _dirty = true;
            Repaint();
        }

        private void OnBufferCleared()
        {
            _dirty = true;
            Repaint();
        }

        private void RebuildTagOptions()
        {
            var tags = SBLogSettings.GetAllPredefinedTags();
            _tagOptions = new string[tags.Count + 1];
            _tagOptions[0] = "All";
            for (int i = 0; i < tags.Count; i++)
                _tagOptions[i + 1] = tags[i];
        }

        private void EnsureStyles()
        {
            if (_logLineStyle != null) return;

            _logLineStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = false,
                fontSize = 11,
                padding = new RectOffset(4, 4, 1, 1),
            };

            _debugStyle = new GUIStyle(_logLineStyle);
            _debugStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            _infoStyle = new GUIStyle(_logLineStyle);
            _infoStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _warnStyle = new GUIStyle(_logLineStyle);
            _warnStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            _errorStyle = new GUIStyle(_logLineStyle);
            _errorStyle.normal.textColor = new Color(1f, 0.35f, 0.35f);
        }

        // ─── GUI ───

        private void OnGUI()
        {
            EnsureStyles();

            DrawToolbar();

            if (_showSettings)
            {
                DrawSettingsPanel();
                return;
            }

            RefreshIfDirty();
            DrawLogList();
            DrawStatusBar();
        }

        // ─── Toolbar ───

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 级别快速过滤按钮
            DrawLevelToggle("D", SBLogLevel.Debug);
            DrawLevelToggle("I", SBLogLevel.Info);
            DrawLevelToggle("W", SBLogLevel.Warning);
            DrawLevelToggle("E", SBLogLevel.Error);

            GUILayout.Space(8);

            // 模块过滤
            EditorGUI.BeginChangeCheck();
            _filterTagIndex = EditorGUILayout.Popup(_filterTagIndex, _tagOptions,
                EditorStyles.toolbarPopup, GUILayout.Width(90));
            if (EditorGUI.EndChangeCheck()) _dirty = true;

            GUILayout.Space(4);

            // 搜索框
            EditorGUI.BeginChangeCheck();
            _searchKeyword = EditorGUILayout.TextField(_searchKeyword,
                EditorStyles.toolbarSearchField, GUILayout.MinWidth(100));
            if (EditorGUI.EndChangeCheck()) _dirty = true;

            // 自动滚动
            _autoScroll = GUILayout.Toggle(_autoScroll, "自动滚动",
                EditorStyles.toolbarButton, GUILayout.Width(60));

            // 设置齿轮
            if (GUILayout.Button("⚙", EditorStyles.toolbarButton, GUILayout.Width(24)))
                _showSettings = !_showSettings;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLevelToggle(string label, SBLogLevel level)
        {
            bool active = _filterLevel <= level;
            var style = EditorStyles.toolbarButton;
            if (GUILayout.Toggle(active, label, style, GUILayout.Width(24)) != active)
            {
                _filterLevel = active ? (SBLogLevel)(level + 1) : level;
                _dirty = true;
            }
        }

        // ─── Log List ───

        private void DrawLogList()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _filteredEntries.Count; i++)
            {
                var entry = _filteredEntries[i];
                var style = entry.Level switch
                {
                    SBLogLevel.Debug => _debugStyle!,
                    SBLogLevel.Warning => _warnStyle!,
                    SBLogLevel.Error => _errorStyle!,
                    _ => _infoStyle!,
                };

                var levelIcon = entry.Level switch
                {
                    SBLogLevel.Debug => "🔍",
                    SBLogLevel.Info => "ℹ",
                    SBLogLevel.Warning => "⚠",
                    SBLogLevel.Error => "❌",
                    _ => " ",
                };

                var mins = (int)(entry.Timestamp / 60);
                var secs = entry.Timestamp - mins * 60;
                var line = $"{mins:D2}:{secs:00.00} [{entry.Tag}] {levelIcon} {entry.Message}";

                // 交替背景色
                if (i % 2 == 1)
                {
                    var rect = EditorGUILayout.GetControlRect(false, 18);
                    EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.08f));
                    GUI.Label(rect, line, style);
                }
                else
                {
                    EditorGUILayout.LabelField(line, style, GUILayout.Height(18));
                }
            }

            // 自动滚动到底部
            if (_autoScroll && Event.current.type == EventType.Repaint)
            {
                _scrollPos.y = float.MaxValue;
            }

            EditorGUILayout.EndScrollView();
        }

        // ─── Status Bar ───

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var total = SBLog.Buffer.Count;
            var shown = _filteredEntries.Count;
            EditorGUILayout.LabelField($"显示: {shown}/{total} 条", GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("复制已过滤", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                var tag = _filterTagIndex > 0 ? _tagOptions[_filterTagIndex] : null;
                var keyword = string.IsNullOrEmpty(_searchKeyword) ? null : _searchKeyword;
                var text = SBLog.Buffer.ExportAsText(_filterLevel, tag, keyword);
                EditorGUIUtility.systemCopyBuffer = text;
                SBLog.Info(SBLogTags.Blueprint, "已复制 {0} 条日志到剪贴板", shown);
            }

            if (GUILayout.Button("导出全部", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                var text = SBLog.Buffer.ExportAsText();
                EditorGUIUtility.systemCopyBuffer = text;
                SBLog.Info(SBLogTags.Blueprint, "已导出 {0} 条日志到剪贴板", total);
            }

            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                SBLog.Buffer.Clear();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─── Settings Panel ───

        private void DrawSettingsPanel()
        {
            _settingsScrollPos = EditorGUILayout.BeginScrollView(_settingsScrollPos);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("日志设置", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // 全局级别
            EditorGUI.BeginChangeCheck();
            var newLevel = (SBLogLevel)EditorGUILayout.EnumPopup("全局最低级别", SBLogSettings.GlobalLevel);
            if (EditorGUI.EndChangeCheck())
                SBLogSettings.GlobalLevel = newLevel;

            // 缓冲区容量
            EditorGUILayout.BeginHorizontal();
            _bufferCapacityInput = EditorGUILayout.IntField("缓冲区容量", _bufferCapacityInput);
            if (_bufferCapacityInput != SBLogSettings.BufferCapacity)
            {
                if (GUILayout.Button("应用", GUILayout.Width(50)))
                {
                    SBLogSettings.BufferCapacity = _bufferCapacityInput;
                    SBLog.ReinitBuffer();
                    _dirty = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            // 静默 Console
            EditorGUI.BeginChangeCheck();
            var mute = EditorGUILayout.Toggle("静默 Console", SBLogSettings.MuteConsole);
            if (EditorGUI.EndChangeCheck())
                SBLogSettings.MuteConsole = mute;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("模块开关", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // 模块列表
            var tags = SBLogSettings.GetAllPredefinedTags();
            // 每行2个
            for (int i = 0; i < tags.Count; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                DrawTagToggle(tags[i]);
                if (i + 1 < tags.Count)
                    DrawTagToggle(tags[i + 1]);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全部开启")) SBLogSettings.EnableAllTags();
            if (GUILayout.Button("全部静默")) SBLogSettings.MuteAllTags();
            if (GUILayout.Button("重置默认")) SBLogSettings.ResetToDefaults();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("返回日志"))
                _showSettings = false;

            EditorGUILayout.EndScrollView();
        }

        private void DrawTagToggle(string tag)
        {
            EditorGUI.BeginChangeCheck();
            var enabled = EditorGUILayout.ToggleLeft(tag, SBLogSettings.IsTagEnabled(tag), GUILayout.Width(120));
            if (EditorGUI.EndChangeCheck())
                SBLogSettings.SetTagEnabled(tag, enabled);
        }

        // ─── 刷新过滤 ───

        private void RefreshIfDirty()
        {
            if (!_dirty) return;
            _dirty = false;

            var tag = _filterTagIndex > 0 ? _tagOptions[_filterTagIndex] : null;
            var keyword = string.IsNullOrEmpty(_searchKeyword) ? null : _searchKeyword;

            _filteredEntries = SBLog.Buffer.Filter(_filterLevel, tag, keyword);
        }
    }
}
