#nullable enable
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        // ── 布局结构 ──

        private readonly struct WindowLayout
        {
            public readonly Rect WorkbenchRect;
            public readonly Rect WorkbenchSplitterRect;
            public readonly Rect GraphRect;
            public readonly Rect SplitterRect;
            public readonly Rect InspectorRect;
            public readonly float ContentTop;
            public readonly float ContentHeight;

            public WindowLayout(Rect wb, Rect wbSplit, Rect graph, Rect split, Rect insp, float top, float height)
            {
                WorkbenchRect         = wb;
                WorkbenchSplitterRect = wbSplit;
                GraphRect             = graph;
                SplitterRect          = split;
                InspectorRect         = insp;
                ContentTop            = top;
                ContentHeight         = height;
            }
        }

        private WindowLayout CalculateWindowLayout()
        {
            float contentTop    = ToolbarHeight;
            float contentHeight = position.height - contentTop;
            float splitterCount = _showWorkbench ? 2f : 1f;
            float workbenchWidth = 0f;
            if (_showWorkbench)
            {
                float maxWB = Mathf.Min(MaxWorkbenchWidth,
                    position.width - MinInspectorWidth - MinCanvasWidth - SplitterWidth * splitterCount);
                if (maxWB < MinWorkbenchWidth) maxWB = MinWorkbenchWidth;
                workbenchWidth  = Mathf.Clamp(_workbenchWidth, MinWorkbenchWidth, maxWB);
                _workbenchWidth = workbenchWidth;
            }
            float maxInsp = Mathf.Min(MaxInspectorWidth,
                position.width - workbenchWidth - MinCanvasWidth - SplitterWidth * splitterCount);
            if (maxInsp < MinInspectorWidth) maxInsp = MinInspectorWidth;
            _inspectorWidth = Mathf.Clamp(_inspectorWidth, MinInspectorWidth, maxInsp);
            float canvasWidth = Mathf.Max(MinCanvasWidth,
                position.width - workbenchWidth - _inspectorWidth - SplitterWidth * splitterCount);

            var wbRect   = new Rect(0,               contentTop, workbenchWidth, contentHeight);
            var wbSplit  = new Rect(workbenchWidth,   contentTop, SplitterWidth,  contentHeight);
            float graphX = _showWorkbench ? wbRect.xMax + SplitterWidth : 0f;
            var gRect    = new Rect(graphX,           contentTop, canvasWidth,    contentHeight);
            var sRect    = new Rect(gRect.xMax,       contentTop, SplitterWidth,  contentHeight);
            var iRect    = new Rect(sRect.xMax,       contentTop, _inspectorWidth, contentHeight);
            return new WindowLayout(wbRect, wbSplit, gRect, sRect, iRect, contentTop, contentHeight);
        }

        // ── 分栏拖拽 ──

        private void HandleWorkbenchSplitter(Rect splitterRect, Event evt)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(evt.mousePosition))
                    {
                        _isDraggingWorkbenchSplitter = true;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isDraggingWorkbenchSplitter)
                    {
                        float nextWidth = evt.mousePosition.x - SplitterWidth * 0.5f;
                        float maxWorkbenchWidth = Mathf.Min(
                            MaxWorkbenchWidth,
                            position.width - _inspectorWidth - MinCanvasWidth - SplitterWidth * 2f);
                        if (maxWorkbenchWidth < MinWorkbenchWidth)
                            maxWorkbenchWidth = MinWorkbenchWidth;

                        _workbenchWidth = Mathf.Clamp(nextWidth, MinWorkbenchWidth, maxWorkbenchWidth);
                        Repaint();
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isDraggingWorkbenchSplitter)
                    {
                        _isDraggingWorkbenchSplitter = false;
                        EditorPrefs.SetFloat(WorkbenchWidthPrefsKey, _workbenchWidth);
                        evt.Use();
                    }
                    break;
            }
        }

        private void HandleSplitter(Rect splitterRect, Event evt, float workbenchWidth)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(evt.mousePosition))
                    {
                        _isDraggingSplitter = true;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isDraggingSplitter)
                    {
                        float nextWidth = position.width - evt.mousePosition.x - SplitterWidth * 0.5f;
                        float splitterCount = _showWorkbench ? 2f : 1f;
                        float maxInspectorWidth = Mathf.Min(
                            MaxInspectorWidth,
                            position.width - workbenchWidth - MinCanvasWidth - SplitterWidth * splitterCount);
                        if (maxInspectorWidth < MinInspectorWidth)
                            maxInspectorWidth = MinInspectorWidth;

                        _inspectorWidth = Mathf.Clamp(nextWidth, MinInspectorWidth, maxInspectorWidth);
                        Repaint();
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isDraggingSplitter)
                    {
                        _isDraggingSplitter = false;
                        evt.Use();
                    }
                    break;
            }
        }

        private void HandleAnalysisSplitter(Rect splitterRect, Event evt, Rect parentRect)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(evt.mousePosition))
                    {
                        _isDraggingAnalysisSplitter = true;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isDraggingAnalysisSplitter)
                    {
                        float newHeight = parentRect.yMax - evt.mousePosition.y - AnalysisSplitterHeight;
                        _analysisHeight = Mathf.Clamp(newHeight, MinAnalysisHeight, parentRect.height * 0.6f);
                        Repaint();
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isDraggingAnalysisSplitter)
                    {
                        _isDraggingAnalysisSplitter = false;
                        EditorPrefs.SetFloat(AnalysisHeightPrefsKey, _analysisHeight);
                        evt.Use();
                    }
                    break;
            }
        }
    }
}
