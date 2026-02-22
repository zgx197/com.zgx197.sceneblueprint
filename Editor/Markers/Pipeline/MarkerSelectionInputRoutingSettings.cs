#nullable enable
using UnityEditor;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// 标记选中输入路由配置。
    /// true  = 由 MarkerSelectTool(EditorTool) 驱动选中输入。
    /// false = 回退到 GizmoRenderPipeline(duringSceneGui) 驱动。
    /// </summary>
    internal static class MarkerSelectionInputRoutingSettings
    {
        private const string PrefsKey = "SceneBlueprint.Marker.SelectionInput.UseEditorTool";

        public static bool LoadUseEditorTool()
        {
            return EditorPrefs.GetBool(PrefsKey, true);
        }

        public static void SaveUseEditorTool(bool useEditorTool)
        {
            EditorPrefs.SetBool(PrefsKey, useEditorTool);
        }
    }
}
