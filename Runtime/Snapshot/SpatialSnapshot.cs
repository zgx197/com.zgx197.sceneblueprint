#nullable enable
using System;
using UnityEngine;

namespace SceneBlueprint.Runtime.Snapshot
{
    /// <summary>
    /// 空间数据快照 — 记录 Marker 的 Transform 和几何形状数据。
    /// <para>
    /// position/rotation/localScale 对应 Transform 的世界坐标数据。
    /// shapeDataJson 由 <see cref="Markers.SceneMarker.SerializeShapeData"/> 生成，
    /// 存储 Marker 子类特有的几何数据（如 AreaMarker 的 BoxSize/Vertices/Shape/Height）。
    /// </para>
    /// </summary>
    [Serializable]
    public class SpatialSnapshot
    {
        /// <summary>Transform.position（世界坐标）</summary>
        public Vector3 position;

        /// <summary>Transform.rotation（世界旋转）</summary>
        public Quaternion rotation = Quaternion.identity;

        /// <summary>Transform.localScale</summary>
        public Vector3 localScale = Vector3.one;

        /// <summary>
        /// Marker 子类的几何数据（JSON 字符串）。
        /// <para>
        /// 由 SceneMarker.SerializeShapeData() 生成，
        /// 由 SceneMarker.DeserializeShapeData() 消费。
        /// 基类默认 "{}"，子类 override 序列化自己的特有字段。
        /// </para>
        /// </summary>
        public string shapeDataJson = "{}";
    }
}
