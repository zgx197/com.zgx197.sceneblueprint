#nullable enable
using UnityEditor;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// SceneView 标记交互模式的编辑器级持久化配置。
    ///
    /// 统一管理 EditorPrefs 读写，避免窗口层散落硬编码 key。
    /// </summary>
    internal static class MarkerInteractionModeSettings
    {
        private const string PrefsKey = "SceneBlueprint.Marker.InteractionMode";

        public static GizmoRenderPipeline.MarkerInteractionMode Load()
        {
            int fallback = (int)GizmoRenderPipeline.MarkerInteractionMode.Edit;
            int raw = EditorPrefs.GetInt(PrefsKey, fallback);
            if (!System.Enum.IsDefined(typeof(GizmoRenderPipeline.MarkerInteractionMode), raw))
                raw = fallback;

            return (GizmoRenderPipeline.MarkerInteractionMode)raw;
        }

        public static void Save(GizmoRenderPipeline.MarkerInteractionMode mode)
        {
            EditorPrefs.SetInt(PrefsKey, (int)mode);
        }
    }
}
