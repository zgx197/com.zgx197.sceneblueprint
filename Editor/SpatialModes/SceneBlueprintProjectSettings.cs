#nullable enable
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.SpatialModes
{
    [FilePath("ProjectSettings/SceneBlueprintProjectSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class SceneBlueprintProjectSettings : ScriptableSingleton<SceneBlueprintProjectSettings>
    {
        [SerializeField]
        private string _spatialModeId = "Unity3D";

        public string SpatialModeId
        {
            get => string.IsNullOrWhiteSpace(_spatialModeId) ? "Unity3D" : _spatialModeId;
            set => _spatialModeId = string.IsNullOrWhiteSpace(value) ? "Unity3D" : value.Trim();
        }

        public static string GetSpatialModeId()
        {
            return instance.SpatialModeId;
        }

        public static void SetSpatialModeId(string modeId)
        {
            instance.SpatialModeId = modeId;
            instance.Save(true);
        }
    }

    internal static class SceneBlueprintProjectSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/SceneBlueprint", SettingsScope.Project)
            {
                label = "SceneBlueprint",
                guiHandler = _ =>
                {
                    var settings = SceneBlueprintProjectSettings.instance;

                    var descriptors = SpatialModeRegistry.GetAll()
                        .OrderBy(d => d.DisplayName)
                        .ToList();

                    if (descriptors.Count == 0)
                    {
                        EditorGUILayout.HelpBox("未发现可用空间模式描述器。", MessageType.Warning);
                        return;
                    }

                    var options = descriptors
                        .Select(d => $"{d.DisplayName} ({d.ModeId})")
                        .ToArray();
                    var modeIds = descriptors.Select(d => d.ModeId).ToArray();

                    int currentIndex = System.Array.FindIndex(modeIds,
                        id => id.Equals(settings.SpatialModeId, System.StringComparison.OrdinalIgnoreCase));
                    if (currentIndex < 0)
                        currentIndex = 0;

                    EditorGUI.BeginChangeCheck();
                    int nextIndex = EditorGUILayout.Popup(
                        new GUIContent("Spatial Mode", "项目固定空间模式（影响 SceneView 放置与导出编码）"),
                        currentIndex,
                        options);

                    if (EditorGUI.EndChangeCheck())
                    {
                        SceneBlueprintProjectSettings.SetSpatialModeId(modeIds[nextIndex]);
                    }
                },
                keywords = new System.Collections.Generic.HashSet<string>(new[]
                {
                    "SceneBlueprint", "Spatial", "Mode", "2D", "3D"
                })
            };
        }
    }
}
