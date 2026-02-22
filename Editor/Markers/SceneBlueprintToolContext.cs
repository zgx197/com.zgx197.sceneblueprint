#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Editor.SpatialModes;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// SceneBlueprint 工具上下文（P3）。
    /// 统一托管：
    /// - MarkerSelectTool 激活/失活；
    /// - 选中输入路由（Tool / duringSceneGui）；
    /// - 创建输入路由（Tool / duringSceneGui）；
    /// - SceneViewMarkerTool 生命周期。
    /// </summary>
    internal sealed class SceneBlueprintToolContext
    {
        private bool _attached;
        private bool _useEditorToolSelectionInput;
        private bool _markerToolEnabled;

        public bool UseEditorToolSelectionInput => _useEditorToolSelectionInput;

        public void Attach(bool useEditorToolSelectionInput)
        {
            _attached = true;
            _useEditorToolSelectionInput = useEditorToolSelectionInput;

            SBLog.Info(SBLogTags.Selection,
                $"SceneBlueprintToolContext.Attach useEditorTool={_useEditorToolSelectionInput}");

            ApplyInputRouting();
        }

        public void Detach()
        {
            if (!_attached && !_markerToolEnabled)
                return;

            _attached = false;

            if (_markerToolEnabled)
            {
                SceneViewMarkerTool.Disable();
                _markerToolEnabled = false;
            }

            MarkerSelectTool.SetEnabled(false);
            SceneViewMarkerTool.SetCreateInputDrivenByTool(false);
            GizmoRenderPipeline.SetSelectionInputDrivenByTool(false);

            SBLog.Info(SBLogTags.Selection, "SceneBlueprintToolContext.Detach => reset all tool routes");
        }

        public void SetSelectionInputRouting(bool useEditorToolSelectionInput)
        {
            _useEditorToolSelectionInput = useEditorToolSelectionInput;

            if (!_attached)
                return;

            ApplyInputRouting();
        }

        public void EnableMarkerTool(IActionRegistry registry, IEditorSpatialModeDescriptor spatialMode)
        {
            SceneViewMarkerTool.Enable(registry, spatialMode);
            _markerToolEnabled = true;

            if (_attached)
                ApplyInputRouting();
        }

        public void DisableMarkerTool()
        {
            if (!_markerToolEnabled)
                return;

            SceneViewMarkerTool.Disable();
            _markerToolEnabled = false;
        }

        private void ApplyInputRouting()
        {
            if (!_attached)
                return;

            if (_useEditorToolSelectionInput)
            {
                MarkerSelectTool.SetEnabled(true);
                SceneViewMarkerTool.SetCreateInputDrivenByTool(true);
                MarkerSelectTool.ActivateIfEnabled();
            }
            else
            {
                MarkerSelectTool.SetEnabled(false);
                SceneViewMarkerTool.SetCreateInputDrivenByTool(false);
                GizmoRenderPipeline.SetSelectionInputDrivenByTool(false);
            }

            SBLog.Info(SBLogTags.Selection,
                $"SceneBlueprintToolContext.ApplyInputRouting => {(_useEditorToolSelectionInput ? "Tool" : "duringSceneGui")}");
        }
    }
}
