#nullable enable
using System;

namespace SceneBlueprint.Runtime.Snapshot
{
    /// <summary>
    /// Annotation 数据快照 — 记录一个 MarkerAnnotation 的业务数据。
    /// <para>
    /// typeId 对应 <see cref="Markers.Annotations.MarkerAnnotation.AnnotationTypeId"/>，
    /// propertiesJson 是 CollectExportData 输出的 key-value 数据序列化为 JSON 字符串。
    /// 恢复时通过 AnnotationDefinitionRegistry 查找 ComponentType，
    /// 再调用 RestoreFromExportData 将数据写回组件字段。
    /// </para>
    /// </summary>
    [Serializable]
    public class AnnotationSnapshot
    {
        /// <summary>
        /// MarkerAnnotation.AnnotationTypeId（如 "MonsterPool"、"SpawnAssignment"、"Camera"）。
        /// <para>用于从 AnnotationDefinitionRegistry 查找对应的 ComponentType。</para>
        /// </summary>
        public string typeId = "";

        /// <summary>
        /// CollectExportData 输出的 key-value 数据，序列化为 JSON 字符串。
        /// <para>
        /// 恢复时反序列化为 IDictionary&lt;string, object&gt;，
        /// 传给 MarkerAnnotation.RestoreFromExportData()。
        /// </para>
        /// </summary>
        public string propertiesJson = "{}";
    }
}
