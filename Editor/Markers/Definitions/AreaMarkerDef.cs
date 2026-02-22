#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Markers;
using UnityEngine;

namespace SceneBlueprint.Editor.Markers.Definitions
{
    /// <summary>
    /// 区域标记定义——多边形或 Box 区域。
    /// </summary>
    [MarkerDef(MarkerTypeIds.Area)]
    public class AreaMarkerDef : IMarkerDefinitionProvider
    {
        public MarkerDefinition Define() => new MarkerDefinition
        {
            TypeId = MarkerTypeIds.Area,
            DisplayName = "区域标记",
            Description = "多边形或 Box 区域（如触发区、刷怪区、灯光区）",
            ComponentType = typeof(AreaMarker),
            DefaultSpacing = 10f,
            Initializer = marker =>
            {
                var am = (AreaMarker)marker;
                am.Shape = AreaShape.Box;
                am.BoxSize = new Vector3(8f, 3f, 8f);
            },
        };
    }
}
