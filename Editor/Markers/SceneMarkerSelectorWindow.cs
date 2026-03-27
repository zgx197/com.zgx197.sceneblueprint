#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// 通用场景标记选择器窗口——模仿 Unity 原生 "Select GameObject" 风格，
    /// 但只列出满足过滤条件的 SceneMarker。
    /// <para>
    /// 设计为通用组件，可被 Inspector 绘制器、右键菜单等任何需要选择场景标记的地方调用。
    /// 支持按 MarkerTypeId、RequiredAnnotations、自定义谓词进行过滤。
    /// </para>
    /// </summary>
    public class SceneMarkerSelectorWindow : EditorWindow
    {
        // ─── 常量 ───

        private const float kSearchBarHeight = 22f;
        private const float kItemHeight = 36f;
        private const float kNoneItemHeight = 24f;
        private const float kSeparatorHeight = 1f;
        private const float kIconSize = 16f;
        private const float kCheckMarkWidth = 20f;

        // ─── 过滤参数 ───

        /// <summary>过滤配置</summary>
        public class FilterConfig
        {
            /// <summary>限定的 MarkerTypeId（为 null 或空字符串表示不限类型）</summary>
            public string? MarkerTypeId { get; set; }

            /// <summary>必需的 Annotation 类型 ID 列表（为 null 或空表示无要求）</summary>
            public string[]? RequiredAnnotations { get; set; }

            /// <summary>自定义过滤谓词（返回 true 表示保留）</summary>
            public Func<SceneMarker, bool>? CustomFilter { get; set; }

            /// <summary>窗口标题（为 null 时自动生成）</summary>
            public string? Title { get; set; }
        }

        // ─── 回调 ───

        private Action<SceneMarker?>? _onSelected;
        private FilterConfig _filter = new();
        private string _currentMarkerId = "";

        // ─── UI 状态 ───

        private string _searchText = "";
        private Vector2 _scrollPosition;
        private List<CandidateEntry> _candidates = new();
        private List<CandidateEntry> _filteredCandidates = new();
        private int _hoverIndex = -1;
        private bool _focusSearchField = true;

        // ─── 样式缓存 ───

        private bool _stylesInitialized;
        private GUIStyle _searchFieldStyle = null!;
        private GUIStyle _searchCancelStyle = null!;
        private GUIStyle _searchCancelEmptyStyle = null!;
        private GUIStyle _itemLabelStyle = null!;
        private GUIStyle _itemLabelBoldStyle = null!;
        private GUIStyle _subtitleStyle = null!;
        private GUIStyle _subtitleBoldStyle = null!;
        private GUIStyle _checkMarkStyle = null!;

        // ─── 颜色常量 ───

        private static readonly Color kSelectedBg   = new(0.172f, 0.365f, 0.529f, 1f);
        private static readonly Color kHoverBg      = new(0.27f, 0.27f, 0.27f, 1f);
        private static readonly Color kSeparatorCol = new(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color kBgDark       = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color kBgAlt        = new(0.25f, 0.25f, 0.25f, 1f);

        private struct CandidateEntry
        {
            public SceneMarker Marker;
            public string DisplayLabel;
            public string SearchKey;
            public string TypeLabel;
            public string TagLabel;
        }

        // ─── 公开 API ───

        /// <summary>
        /// 打开标记选择器窗口。
        /// </summary>
        public static void Show(
            FilterConfig filter,
            string currentMarkerId,
            Action<SceneMarker?> onSelected,
            Rect buttonRect = default)
        {
            // 关闭已有实例
            var existing = GetWindow<SceneMarkerSelectorWindow>();
            if (existing != null) existing.Close();

            var window = CreateInstance<SceneMarkerSelectorWindow>();
            window._filter = filter ?? new FilterConfig();
            window._currentMarkerId = currentMarkerId ?? "";
            window._onSelected = onSelected;

            // 窗口标题
            string title = filter?.Title
                ?? (string.IsNullOrEmpty(filter?.MarkerTypeId)
                    ? "Select SceneMarker"
                    : $"Select SceneMarker ({filter!.MarkerTypeId})");
            window.titleContent = new GUIContent(title);

            // 收集候选
            window.CollectCandidates();

            // 定位和尺寸
            var size = new Vector2(300, 360);
            if (buttonRect != default)
            {
                var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(buttonRect.x, buttonRect.yMax));
                window.position = new Rect(screenPos.x, screenPos.y, size.x, size.y);
            }
            else
            {
                var main = EditorGUIUtility.GetMainWindowPosition();
                float x = main.x + (main.width - size.x) * 0.5f;
                float y = main.y + (main.height - size.y) * 0.5f;
                window.position = new Rect(x, y, size.x, size.y);
            }

            window.ShowAuxWindow();
            window.Focus();
        }

        // ─── 候选收集 ───

        private void CollectCandidates()
        {
            _candidates.Clear();
            var allMarkers = MarkerHierarchyManager.FindAllMarkers();

            foreach (var marker in allMarkers)
            {
                if (!PassesFilter(marker)) continue;

                _candidates.Add(new CandidateEntry
                {
                    Marker = marker,
                    DisplayLabel = marker.GetDisplayLabel(),
                    SearchKey = (marker.GetDisplayLabel() + " " + marker.MarkerTypeId + " " + marker.Tag).ToLowerInvariant(),
                    TypeLabel = marker.MarkerTypeId,
                    TagLabel = string.IsNullOrEmpty(marker.Tag) ? "" : marker.Tag,
                });
            }

            _candidates.Sort((a, b) => string.Compare(a.DisplayLabel, b.DisplayLabel, StringComparison.OrdinalIgnoreCase));
            ApplySearchFilter();
        }

        private bool PassesFilter(SceneMarker marker)
        {
            if (!string.IsNullOrEmpty(_filter.MarkerTypeId)
                && marker.MarkerTypeId != _filter.MarkerTypeId)
                return false;

            if (_filter.RequiredAnnotations != null && _filter.RequiredAnnotations.Length > 0)
            {
                var annotations = marker.GetComponents<Runtime.Markers.Annotations.MarkerAnnotation>();
                var annoTypeIds = new HashSet<string>();
                foreach (var anno in annotations)
                    annoTypeIds.Add(anno.AnnotationTypeId);

                foreach (var req in _filter.RequiredAnnotations)
                {
                    if (!annoTypeIds.Contains(req)) return false;
                }
            }

            if (_filter.CustomFilter != null && !_filter.CustomFilter(marker))
                return false;

            return true;
        }

        private void ApplySearchFilter()
        {
            _filteredCandidates.Clear();
            if (string.IsNullOrEmpty(_searchText))
            {
                _filteredCandidates.AddRange(_candidates);
            }
            else
            {
                var lower = _searchText.ToLowerInvariant();
                foreach (var c in _candidates)
                {
                    if (c.SearchKey.Contains(lower))
                        _filteredCandidates.Add(c);
                }
            }
            _hoverIndex = -1;
        }

        // ─── GUI ───

        private void OnGUI()
        {
            EnsureStyles();

            // 背景
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), kBgDark);

            // 搜索栏（固定高度区域）
            DrawSearchBar();

            // 候选列表（占据剩余空间）
            DrawCandidateList();

            HandleKeyboard();
            wantsMouseMove = true;
        }

        private void DrawSearchBar()
        {
            // 搜索栏固定区域
            var barRect = GUILayoutUtility.GetRect(position.width, kSearchBarHeight + 6);

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(barRect, new Color(0.19f, 0.19f, 0.19f, 1f));

            // 搜索框 Rect（留左右边距）
            var searchRect = new Rect(barRect.x + 5, barRect.y + 3, barRect.width - 10, kSearchBarHeight);

            GUI.SetNextControlName("MarkerSearchField");
            var newSearch = EditorGUI.TextField(searchRect, _searchText, _searchFieldStyle);
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                ApplySearchFilter();
            }

            // 清除按钮（搜索框右侧内嵌）
            if (!string.IsNullOrEmpty(_searchText))
            {
                var cancelRect = new Rect(searchRect.xMax - 18, searchRect.y + 1, 16, searchRect.height - 2);
                if (GUI.Button(cancelRect, GUIContent.none, _searchCancelStyle))
                {
                    _searchText = "";
                    ApplySearchFilter();
                    GUI.FocusControl("MarkerSearchField");
                }
            }

            // 搜索栏下方分隔线
            var sepRect = GUILayoutUtility.GetRect(position.width, kSeparatorHeight);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(sepRect, kSeparatorCol);

            // 首次打开时自动聚焦搜索框
            if (_focusSearchField && Event.current.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl("MarkerSearchField");
                _focusSearchField = false;
            }
        }

        private void DrawCandidateList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // ── None 选项 ──
            DrawNoneItem();

            // ── 分隔线 ──
            DrawSeparator();

            // ── 候选项 ──
            if (_filteredCandidates.Count == 0)
            {
                GUILayout.Space(20);
                var centered = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                    fontSize = 11,
                };
                GUILayout.Label("No markers found", centered);
            }
            else
            {
                for (int i = 0; i < _filteredCandidates.Count; i++)
                {
                    DrawCandidateItem(i);
                }
            }

            GUILayout.Space(4);
            EditorGUILayout.EndScrollView();
        }

        private void DrawNoneItem()
        {
            bool isNoneSelected = string.IsNullOrEmpty(_currentMarkerId);
            var itemRect = GUILayoutUtility.GetRect(position.width, kNoneItemHeight);

            // 背景
            if (Event.current.type == EventType.Repaint)
            {
                if (isNoneSelected)
                    EditorGUI.DrawRect(itemRect, kSelectedBg);
                else if (itemRect.Contains(Event.current.mousePosition))
                    EditorGUI.DrawRect(itemRect, kHoverBg);
            }

            // 勾选标记
            if (isNoneSelected)
            {
                var checkRect = new Rect(itemRect.x + 4, itemRect.y + 2, kCheckMarkWidth, itemRect.height - 4);
                GUI.Label(checkRect, "\u2713", _checkMarkStyle);
            }

            // 文字
            var labelRect = new Rect(itemRect.x + kCheckMarkWidth + 4, itemRect.y, itemRect.width - kCheckMarkWidth - 8, itemRect.height);
            var style = isNoneSelected ? _itemLabelBoldStyle : _itemLabelStyle;
            GUI.Label(labelRect, "None", style);

            // 点击
            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                SelectAndClose(null);
                Event.current.Use();
            }
        }

        private void DrawCandidateItem(int index)
        {
            var entry = _filteredCandidates[index];
            bool isCurrent = !string.IsNullOrEmpty(entry.Marker.MarkerId)
                && entry.Marker.MarkerId == _currentMarkerId;
            bool isHover = index == _hoverIndex;

            var itemRect = GUILayoutUtility.GetRect(position.width, kItemHeight);

            // 背景：交替色 + hover/selected
            if (Event.current.type == EventType.Repaint)
            {
                if (isCurrent)
                    EditorGUI.DrawRect(itemRect, kSelectedBg);
                else if (isHover)
                    EditorGUI.DrawRect(itemRect, kHoverBg);
                else if (index % 2 == 1)
                    EditorGUI.DrawRect(itemRect, kBgAlt);
            }

            // 勾选标记（当前绑定项）
            if (isCurrent)
            {
                var checkRect = new Rect(itemRect.x + 4, itemRect.y + 2, kCheckMarkWidth, itemRect.height - 4);
                GUI.Label(checkRect, "\u2713", _checkMarkStyle);
            }

            // 图标（使用 Unity 内置 GameObject 图标）
            var iconContent = EditorGUIUtility.IconContent("d_GameObject Icon");
            if (iconContent?.image != null)
            {
                var iconRect = new Rect(
                    itemRect.x + kCheckMarkWidth + 4,
                    itemRect.y + (itemRect.height - kIconSize) * 0.5f,
                    kIconSize, kIconSize);
                GUI.DrawTexture(iconRect, iconContent.image, ScaleMode.ScaleToFit);
            }

            float textX = itemRect.x + kCheckMarkWidth + kIconSize + 10;
            float textW = itemRect.width - textX - 8;

            // 主标题
            var titleRect = new Rect(textX, itemRect.y + 3, textW, 18);
            var titleStyle = isCurrent ? _itemLabelBoldStyle : _itemLabelStyle;
            GUI.Label(titleRect, entry.DisplayLabel, titleStyle);

            // 副标题：类型 + Tag
            var subtitle = entry.TypeLabel;
            if (!string.IsNullOrEmpty(entry.TagLabel))
                subtitle += "  \u00B7  " + entry.TagLabel;
            var subRect = new Rect(textX, itemRect.y + 19, textW, 14);
            var subStyle = isCurrent ? _subtitleBoldStyle : _subtitleStyle;
            GUI.Label(subRect, subtitle, subStyle);

            // hover 追踪
            if (Event.current.type == EventType.MouseMove)
            {
                int newHover = itemRect.Contains(Event.current.mousePosition) ? index : _hoverIndex;
                if (newHover != _hoverIndex)
                {
                    _hoverIndex = newHover;
                    Repaint();
                }
            }

            // 点击选择
            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                SelectAndClose(entry.Marker);
                Event.current.Use();
            }
        }

        private void HandleKeyboard()
        {
            if (Event.current.type != EventType.KeyDown) return;

            switch (Event.current.keyCode)
            {
                case KeyCode.Escape:
                    Close();
                    Event.current.Use();
                    break;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_hoverIndex >= 0 && _hoverIndex < _filteredCandidates.Count)
                    {
                        SelectAndClose(_filteredCandidates[_hoverIndex].Marker);
                        Event.current.Use();
                    }
                    break;

                case KeyCode.UpArrow:
                    _hoverIndex = Mathf.Max(-1, _hoverIndex - 1);
                    EnsureHoverVisible();
                    Repaint();
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    _hoverIndex = Mathf.Min(_filteredCandidates.Count - 1, _hoverIndex + 1);
                    EnsureHoverVisible();
                    Repaint();
                    Event.current.Use();
                    break;
            }
        }

        private void EnsureHoverVisible()
        {
            if (_hoverIndex < 0) return;
            // 粗略估算 hover 项的 Y 位置并滚动
            float headerOffset = kSearchBarHeight + 4 + kNoneItemHeight + kSeparatorHeight;
            float itemTop = headerOffset + _hoverIndex * kItemHeight;
            float itemBot = itemTop + kItemHeight;
            float viewH = position.height - kSearchBarHeight - 10;

            if (itemTop < _scrollPosition.y)
                _scrollPosition.y = itemTop;
            else if (itemBot > _scrollPosition.y + viewH)
                _scrollPosition.y = itemBot - viewH;
        }

        // ─── 选择与关闭 ───

        private void SelectAndClose(SceneMarker? marker)
        {
            _onSelected?.Invoke(marker);
            Close();
        }

        private void OnLostFocus()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null) Close();
            };
        }

        // ─── 绘制辅助 ───

        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, kSeparatorHeight);
            EditorGUI.DrawRect(rect, kSeparatorCol);
        }

        // ─── 样式初始化 ───

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _searchFieldStyle = new GUIStyle("SearchTextField")
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 0, 0),
            };

            _searchCancelStyle = new GUIStyle("SearchCancelButton");
            _searchCancelEmptyStyle = new GUIStyle("SearchCancelButtonEmpty");

            _itemLabelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            };

            _itemLabelBoldStyle = new GUIStyle(_itemLabelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };

            _subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = new Color(0.55f, 0.55f, 0.55f) },
            };

            _subtitleBoldStyle = new GUIStyle(_subtitleStyle)
            {
                normal = { textColor = new Color(0.8f, 0.85f, 0.9f) },
            };

            _checkMarkStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(0, 0, 0, 0),
            };
        }
    }
}
