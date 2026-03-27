#nullable enable
using System;
using UnityEngine;

namespace SceneBlueprint.Runtime.Markers
{
    /// <summary>
    /// 场景标记抽象基类——蓝图节点与场景空间的桥梁。
    /// <para>
    /// 设计师在 Scene View 中放置标记来标注空间信息（刷怪点、触发区域、摄像机位等），
    /// 蓝图节点通过 <see cref="MarkerId"/> 引用标记，建立逻辑→空间的绑定关系。
    /// </para>
    /// <para>
    /// 标记本身是"哑"对象——只描述空间属性，不包含业务逻辑。
    /// 逻辑由蓝图节点定义，标记只提供位置、区域、实体等空间数据。
    /// </para>
    /// </summary>
    public abstract class SceneMarker : MonoBehaviour
    {
        [Header("标记标识")]

        [Tooltip("唯一 ID——蓝图节点通过此 ID 引用标记")]
        [SerializeField] private string _markerId = "";

        [Tooltip("设计师可读名称（在 Gizmo 标签中显示）")]
        public string MarkerName = "";

        [Header("分类")]

        [Tooltip("Tag 标签——用于图层映射和分类过滤（如 Combat.SpawnPoint）")]
        public string Tag = "";

        [Header("显示")]

        [Tooltip("是否覆盖图层默认 Gizmo 颜色")]
        public bool UseCustomGizmoColor;

        [Tooltip("自定义 Gizmo 颜色（UseCustomGizmoColor=true 时生效）")]
        public Color CustomGizmoColor = Color.white;

        [Tooltip("所属子蓝图 ID（为空表示顶层）")]
        [SerializeField] private string _subGraphId = "";

        /// <summary>唯一标识符——蓝图节点通过此 ID 引用标记</summary>
        public string MarkerId
        {
            get => _markerId;
            set => _markerId = value;
        }

        /// <summary>所属子蓝图 ID（为空表示属于顶层图）</summary>
        public string SubGraphId
        {
            get => _subGraphId;
            set => _subGraphId = value;
        }

        /// <summary>
        /// 标记类型 ID（由子类实现）——对应 <see cref="MarkerTypeIds"/> 中定义的常量。
        /// <para>如 "Point", "Area", "Entity"，也可以是自定义类型 ID。</para>
        /// </summary>
        public abstract string MarkerTypeId { get; }

        /// <summary>
        /// 返回标记的代表位置——用于双向联动聚焦（Scene View Frame Selected）。
        /// <para>默认返回 Transform 位置，AreaMarker 等子类可重写为区域中心。</para>
        /// </summary>
        public virtual Vector3 GetRepresentativePosition() => transform.position;

        /// <summary>
        /// 返回标记的显示标签——Gizmo 标签文本。
        /// <para>优先显示 MarkerName，为空时回退到 GameObject 名称。</para>
        /// </summary>
        public string GetDisplayLabel()
        {
            return string.IsNullOrEmpty(MarkerName) ? gameObject.name : MarkerName;
        }

        /// <summary>
        /// 从 Tag 中提取图层前缀（第一级，如 "Combat.SpawnPoint" → "Combat"）。
        /// <para>用于 Gizmo 颜色映射和图层过滤。</para>
        /// </summary>
        public string GetLayerPrefix()
        {
            if (string.IsNullOrEmpty(Tag)) return "";
            int dotIndex = Tag.IndexOf('.');
            return dotIndex > 0 ? Tag.Substring(0, dotIndex) : Tag;
        }

        // ── 快照序列化（子类 override 以持久化特有的几何数据）──

        /// <summary>
        /// 将 Marker 特有的空间/几何数据序列化为 JSON 字符串。
        /// <para>
        /// 基类默认返回 "{}"，子类（如 AreaMarker、PointMarker）override
        /// 以序列化各自的特有字段（BoxSize/Vertices/Shape/Height 等）。
        /// 由 <see cref="Runtime.Snapshot.BindingSnapshot"/> 拍快照时调用。
        /// </para>
        /// </summary>
        public virtual string SerializeShapeData() => "{}";

        /// <summary>
        /// 从 JSON 字符串恢复 Marker 特有的空间/几何数据。
        /// <para>
        /// 基类默认不做任何事，子类 override 以反序列化各自的特有字段。
        /// 由快照恢复器在重建 Marker 后调用。
        /// </para>
        /// </summary>
        /// <param name="json">SerializeShapeData 输出的 JSON 字符串</param>
        public virtual void DeserializeShapeData(string json) { }

        // ── 几何版本号（用于 IAreaShape 缓存失效） ──

        /// <summary>
        /// 几何数据版本号——每次形状参数变化时单调递增。
        /// <para>
        /// 消费方（如 AreaMarker.GetShape()）缓存上次构建时的版本号，
        /// 若当前版本号不同则重新构建几何对象。
        /// </para>
        /// </summary>
        public uint GeometryVersion { get; private set; }

        /// <summary>
        /// 递增几何版本号，通知依赖方几何数据已变化。
        /// <para>由子类在修改形状参数后调用（OnValidate、Handle 编辑、反序列化后）。</para>
        /// </summary>
        protected void IncrementGeometryVersion()
        {
            GeometryVersion++;
        }

        /// <summary>自动生成 MarkerId（如果尚未设置）</summary>
        protected virtual void Reset()
        {
            if (string.IsNullOrEmpty(_markerId))
            {
                _markerId = Guid.NewGuid().ToString("N").Substring(0, 12);
            }
        }
    }
}
