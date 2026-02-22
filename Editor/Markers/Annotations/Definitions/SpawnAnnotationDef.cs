#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Markers.Annotations;

namespace SceneBlueprint.Editor.Markers.Annotations.Definitions
{
    /// <summary>
    /// 刷怪点标注定义——描述 <see cref="SpawnAnnotation"/> 的元数据。
    /// <para>
    /// 适用于 PointMarker，不允许同一 Marker 上挂多个 SpawnAnnotation。
    /// </para>
    /// </summary>
    [AnnotationDef("Spawn")]
    public class SpawnAnnotationDef : IAnnotationDefinitionProvider
    {
        public AnnotationDefinition Define() => new AnnotationDefinition
        {
            TypeId = "Spawn",
            DisplayName = "刷怪点标注",
            Description = "标记该位置要生成的怪物及其初始行为",
            ComponentType = typeof(SpawnAnnotation),
            ApplicableMarkerTypes = new[] { MarkerTypeIds.Point },
            AllowMultiple = false,
            TagPrefix = "Combat"
        };
    }
}
