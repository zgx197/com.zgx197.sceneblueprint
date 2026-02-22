#nullable enable
using UnityEngine;

namespace SceneBlueprint.Editor.Preview
{
    /// <summary>
    /// 预览类型枚举
    /// </summary>
    public enum PreviewType
    {
        /// <summary>刷怪位置预览（绿色 Cube）</summary>
        SpawnPositions,
        
        /// <summary>巡逻路径预览（未来扩展）</summary>
        PatrolPath,
        
        /// <summary>触发区域预览（未来扩展）</summary>
        TriggerArea
    }

    /// <summary>
    /// Blueprint 预览数据——由 BlueprintPreviewManager 生成并缓存。
    /// <para>
    /// 用于在 SceneView 中显示 Blueprint 节点的空间效果预览。
    /// </para>
    /// </summary>
    public class PreviewData
    {
        /// <summary>预览类型</summary>
        public PreviewType PreviewType { get; set; }

        /// <summary>源标记 ID（如 AreaMarker 的 MarkerId）</summary>
        public string SourceMarkerId { get; set; } = "";

        /// <summary>生成的位置列表（用于 SpawnPositions）</summary>
        public Vector3[]? Positions { get; set; }

        /// <summary>关联的 Blueprint ID</summary>
        public string BlueprintId { get; set; } = "";

        /// <summary>关联的节点 ID</summary>
        public string NodeId { get; set; } = "";

        /// <summary>生成时间戳（用于过期检测）</summary>
        public double Timestamp { get; set; }
    }
}
