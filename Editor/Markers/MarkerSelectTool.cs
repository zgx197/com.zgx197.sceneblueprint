#nullable enable
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Editor.Logging;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// SceneBlueprint 标记选中工具（EditorTool）。
    ///
    /// 职责：
    /// - 在 Tool 激活时，将选中输入路由切到 GizmoRenderPipeline 的 Tool 入口；
    /// - 在 Tool 失活时回落到 duringSceneGui 输入路由（P0 兼容开关）。
    /// </summary>
    [EditorTool("SceneBlueprint Marker Select")]
    internal sealed class MarkerSelectTool : EditorTool
    {
        private static bool _enabled;

        public static bool IsEnabled => _enabled;
        public static bool IsActive => ToolManager.activeToolType == typeof(MarkerSelectTool);

        public static void SetEnabled(bool enabled)
        {
            if (_enabled == enabled)
                return;

            _enabled = enabled;
            SBLog.Info(SBLogTags.Selection,
                $"MarkerSelectTool.SetEnabled => {_enabled} (activeToolType={ToolManager.activeToolType?.Name ?? "null"})");
            Trace($"SetEnabled => {_enabled}, activeToolType={ToolManager.activeToolType?.Name ?? "null"}");

            if (!_enabled)
                DeactivateIfActive();
        }

        public static void ActivateIfEnabled()
        {
            if (!_enabled)
                return;

            SBLog.Info(SBLogTags.Selection,
                $"MarkerSelectTool.ActivateIfEnabled (before activeToolType={ToolManager.activeToolType?.Name ?? "null"})");
            Trace($"ActivateIfEnabled before activeToolType={ToolManager.activeToolType?.Name ?? "null"}");
            ToolManager.SetActiveTool<MarkerSelectTool>();
        }

        public static void DeactivateIfActive()
        {
            if (ToolManager.activeToolType == typeof(MarkerSelectTool))
            {
                SBLog.Info(SBLogTags.Selection, "MarkerSelectTool.DeactivateIfActive => RestorePreviousTool");
                Trace("DeactivateIfActive => RestorePreviousTool");
                ToolManager.RestorePreviousTool();
            }
            else
            {
                SBLog.Info(SBLogTags.Selection,
                    $"MarkerSelectTool.DeactivateIfActive => no-op restore, force fallback route (activeToolType={ToolManager.activeToolType?.Name ?? "null"})");
                Trace($"DeactivateIfActive => fallback route, activeToolType={ToolManager.activeToolType?.Name ?? "null"}");
                GizmoRenderPipeline.SetSelectionInputDrivenByTool(false);
            }
        }

        public override void OnActivated()
        {
            SBLog.Info(SBLogTags.Selection,
                $"MarkerSelectTool.OnActivated (enabled={_enabled}, activeToolType={ToolManager.activeToolType?.Name ?? "null"})");
            Trace($"OnActivated enabled={_enabled}, activeToolType={ToolManager.activeToolType?.Name ?? "null"}");

            if (!_enabled)
            {
                GizmoRenderPipeline.SetSelectionInputDrivenByTool(false);
                return;
            }

            GizmoRenderPipeline.SetSelectionInputDrivenByTool(true);
        }

        public override void OnWillBeDeactivated()
        {
            SBLog.Info(SBLogTags.Selection,
                $"MarkerSelectTool.OnWillBeDeactivated (activeToolType={ToolManager.activeToolType?.Name ?? "null"})");
            Trace($"OnWillBeDeactivated activeToolType={ToolManager.activeToolType?.Name ?? "null"}");
            GizmoRenderPipeline.SetSelectionInputDrivenByTool(false);
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!_enabled)
                return;

            var sceneView = window as SceneView;
            if (sceneView == null)
                return;

            var evt = Event.current;
            if (evt == null)
                return;

            if (IsTraceEvent(evt.type))
            {
                SBLog.Debug(SBLogTags.Selection,
                    $"MarkerSelectTool.OnToolGUI evt={evt.type}, button={evt.button}, mods={evt.modifiers}, activeToolType={ToolManager.activeToolType?.Name ?? "null"}");
                Trace($"OnToolGUI evt={evt.type}, button={evt.button}, mods={evt.modifiers}, activeToolType={ToolManager.activeToolType?.Name ?? "null"}");
            }

            // P2：创建输入也走 Tool 路由（Shift + 右键）。
            SceneViewMarkerTool.HandleCreateFromTool(evt, sceneView);

            GizmoRenderPipeline.HandlePickingFromTool(evt);
        }

        private static bool IsTraceEvent(EventType type)
        {
            return type == EventType.MouseDown
                || type == EventType.MouseUp
                || type == EventType.MouseDrag
                || type == EventType.Used
                || type == EventType.Ignore;
        }

        private static void Trace(string message)
        {
            // Debug.Log($"[SB.Selection.Trace][MarkerSelectTool] {message}");
        }
    }
}
