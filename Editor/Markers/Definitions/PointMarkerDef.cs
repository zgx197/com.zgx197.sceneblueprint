#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers.Definitions
{
    /// <summary>
    /// 点标记定义——单点位置 + 朝向。
    /// </summary>
    [MarkerDef(MarkerTypeIds.Point)]
    public class PointMarkerDef : IMarkerDefinitionProvider
    {
        public MarkerDefinition Define() => new MarkerDefinition
        {
            TypeId = MarkerTypeIds.Point,
            DisplayName = "点标记",
            Description = "单点位置 + 朝向（如刷怪点、摄像机位、VFX 播放点）",
            ComponentType = typeof(PointMarker),
            DefaultSpacing = 2f,
        };
    }
}
