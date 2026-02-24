#nullable enable
using System;

namespace SceneBlueprint.Editor.Markers.Extensions
{
    /// <summary>
    /// 标记编辑器扩展特性 — 标注在 IMarkerEditorExtension 实现类上，
    /// 声明此扩展适用的 Marker 类型 ID。
    /// <para>
    /// MarkerEditorExtensionRegistry.AutoDiscover() 通过反射扫描此特性，
    /// 自动注册所有业务层的编辑器工具。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [MarkerEditorExtension("PresetSpawnArea")]
    /// public class SpawnPointGeneratorTool : IMarkerEditorExtension
    /// {
    ///     public string TargetMarkerTypeId => "PresetSpawnArea";
    ///     public string DisplayName => "刷怪点生成工具";
    ///     public void DrawInspectorGUI(SceneMarker marker) { /* ... */ }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MarkerEditorExtensionAttribute : Attribute
    {
        /// <summary>目标 Marker 类型 ID（如 "PresetSpawnArea"）</summary>
        public string TargetMarkerTypeId { get; }

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="targetMarkerTypeId">
        /// 目标 Marker 类型 ID，必须与 SceneMarker.MarkerTypeId 匹配。
        /// </param>
        public MarkerEditorExtensionAttribute(string targetMarkerTypeId)
        {
            if (string.IsNullOrEmpty(targetMarkerTypeId))
                throw new ArgumentException("TargetMarkerTypeId 不能为空", nameof(targetMarkerTypeId));

            TargetMarkerTypeId = targetMarkerTypeId;
        }
    }
}
