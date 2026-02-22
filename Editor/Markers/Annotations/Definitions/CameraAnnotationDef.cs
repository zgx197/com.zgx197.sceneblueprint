#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Markers.Annotations;

namespace SceneBlueprint.Editor.Markers.Annotations.Definitions
{
    /// <summary>
    /// 摄像机标注定义——描述 <see cref="CameraAnnotation"/> 的元数据。
    /// <para>
    /// 适用于 PointMarker，不允许同一 Marker 上挂多个 CameraAnnotation。
    /// </para>
    /// </summary>
    [AnnotationDef("Camera")]
    public class CameraAnnotationDef : IAnnotationDefinitionProvider
    {
        public AnnotationDefinition Define() => new AnnotationDefinition
        {
            TypeId = "Camera",
            DisplayName = "摄像机标注",
            Description = "标记该位置的摄像机参数（FOV、过渡时长、缓动曲线）",
            ComponentType = typeof(CameraAnnotation),
            ApplicableMarkerTypes = new[] { MarkerTypeIds.Point },
            AllowMultiple = false,
            TagPrefix = "Camera"
        };
    }
}
