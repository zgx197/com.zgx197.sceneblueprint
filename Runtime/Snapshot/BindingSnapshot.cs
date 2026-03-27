#nullable enable
using System;
using UnityEngine;

namespace SceneBlueprint.Runtime.Snapshot
{
    /// <summary>
    /// 场景绑定快照 — 记录一个 SceneBinding 的完整场景数据。
    /// <para>
    /// 每个 BindingSnapshot 对应蓝图中一个节点属性的场景绑定，包含：
    /// - Marker 标识信息（MarkerId、MarkerTypeId、MarkerName 等）
    /// - Hierarchy 分组信息（与子蓝图解耦，策划自由命名）
    /// - 空间数据（Transform + Shape 几何数据）
    /// - Annotation 业务数据（CollectExportData 输出）
    /// - 子 Marker 快照（递归，如 AreaMarker 下的 PointMarker）
    /// </para>
    /// <para>
    /// 设计原则：
    /// - 快照不包含拓扑信息（拓扑已存于 GraphData）
    /// - 快照通过 scopedBindingKey 与拓扑建立引用关系
    /// - schemaVersion 预留用于未来破坏性变更时的数据迁移
    /// </para>
    /// </summary>
    [Serializable]
    public class BindingSnapshot
    {
        /// <summary>快照 schema 版本号，支持未来数据迁移</summary>
        public int schemaVersion = 1;

        // ── 绑定关联 ──

        /// <summary>
        /// 作用域绑定键（"nodeId/propKey"），关联到拓扑中的具体 ActionNode 属性。
        /// <para>如 "7ad6008d/spawnArea"，唯一标识这个绑定在蓝图中的位置。</para>
        /// </summary>
        public string scopedBindingKey = "";

        // ── Marker 标识 ──

        /// <summary>SceneMarker.MarkerId（GUID 短字符串，唯一标识）</summary>
        public string markerId = "";

        /// <summary>
        /// SceneMarker.MarkerTypeId（如 "Area"、"Point"、"WaveSpawnArea"、"SpawnPoint"）。
        /// <para>恢复时通过 MarkerDefinitionRegistry 查找对应的 ComponentType。</para>
        /// </summary>
        public string markerTypeId = "";

        /// <summary>SceneMarker.MarkerName（策划可读名称）</summary>
        public string markerName = "";

        /// <summary>SceneMarker.Tag（图层映射标签，如 "Combat.SpawnPoint"）</summary>
        public string tag = "";

        /// <summary>
        /// SceneMarker.SubGraphId（拓扑归属，不决定 Hierarchy 位置）。
        /// <para>为空表示属于顶层图。</para>
        /// </summary>
        public string subGraphId = "";

        // ── Hierarchy 组织 ──

        /// <summary>
        /// 在 SceneBlueprintMarkers/ 下的分组名（策划自定义，与子蓝图无关）。
        /// <para>为空表示 [未分组]。恢复时用于 MarkerHierarchyManager.GetOrCreateGroup()。</para>
        /// </summary>
        public string hierarchyGroup = "";

        /// <summary>
        /// GameObject.name（恢复时重建命名）。
        /// <para>如 "WaveSpawnArea_走廊中段"、"SpawnPoint_01"。</para>
        /// </summary>
        public string gameObjectName = "";

        // ── 空间数据 ──

        /// <summary>Marker 的 Transform 和几何形状数据</summary>
        public SpatialSnapshot spatial = new();

        // ── Annotation 数据 ──

        /// <summary>
        /// 该 Marker 上所有 Annotation 的快照。
        /// <para>恢复时按 typeId 查找 ComponentType 并调用 RestoreFromExportData。</para>
        /// </summary>
        public AnnotationSnapshot[] annotations = System.Array.Empty<AnnotationSnapshot>();

        // ── 子 Marker ──

        /// <summary>
        /// 子 Marker 快照（如 AreaMarker 下的 PointMarker）。
        /// <para>恢复时递归创建，并设置 transform.SetParent(parentMarker.transform)。</para>
        /// </summary>
        public BindingSnapshot[] children = System.Array.Empty<BindingSnapshot>();
    }
}
