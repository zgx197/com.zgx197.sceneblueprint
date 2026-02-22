#nullable enable

namespace SceneBlueprint.Editor.Markers.Pipeline.Interaction
{
    /// <summary>
    /// SceneView 状态提示呈现器。
    /// 负责绘制交互模式/创建能力等提示文案，不参与输入仲裁。
    /// </summary>
    internal interface IMarkerOverlayPresenter
    {
        void Draw(
            GizmoRenderPipeline.MarkerInteractionMode interactionMode,
            bool canCreateMarker);
    }
}
