#nullable enable
using UnityEditor;
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Pipeline.Interaction
{
    /// <summary>
    /// SceneView 状态叠层提示实现。
    /// 仅负责可视提示，不参与输入或命中逻辑。
    /// </summary>
    internal sealed class SceneStatusOverlayPresenter : IMarkerOverlayPresenter
    {
        private GUIStyle? _sceneStatusStyle;

        public void Draw(
            GizmoRenderPipeline.MarkerInteractionMode interactionMode,
            bool canCreateMarker)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            string modeText = interactionMode == GizmoRenderPipeline.MarkerInteractionMode.Edit
                ? "交互模式：编辑（单击选中 + 原生变换）"
                : "交互模式：拾取（自定义选中）";
            string stateText = BuildSelectionStateText();
            string createText = canCreateMarker
                ? "标记创建：可用（Shift + 右键）"
                : "标记创建：不可用（请打开 SceneBlueprint 窗口）";

            var style = GetSceneStatusStyle();
            var rect = new Rect(12f, 28f, 440f, 62f);

            Handles.BeginGUI();
            GUI.Label(rect, modeText + "\n" + stateText + "\n" + createText, style);
            Handles.EndGUI();
        }

        private static string BuildSelectionStateText()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
                return "当前状态：编辑中（未选中 Marker）";

            var marker = selected.GetComponent<SceneMarker>();
            if (marker == null)
                return "当前状态：编辑中（当前选中非 Marker）";

            return $"当前状态：已选中（{selected.name}）";
        }

        private GUIStyle GetSceneStatusStyle()
        {
            if (_sceneStatusStyle != null)
                return _sceneStatusStyle;

            _sceneStatusStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                richText = false,
                padding = new RectOffset(8, 8, 4, 4)
            };

            return _sceneStatusStyle;
        }
    }
}
