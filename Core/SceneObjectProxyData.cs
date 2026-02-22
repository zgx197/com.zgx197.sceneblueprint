#nullable enable
using NodeGraph.Core;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 场景对象代理节点数据——代表场景中的标记。
    /// <para>
    /// SceneObjectProxy 是一种特殊节点，不是 Action，而是场景对象在图中的"影子"。
    /// 它的作用是让场景对象（Marker）可以在图中被可视化，并通过连线与 Action 节点绑定。
    /// </para>
    /// <para>
    /// 设计原则：
    /// - 代理节点本身不执行逻辑，只作为数据引用
    /// - 通过 SceneObjectId 关联场景中的实际对象
    /// - 支持场景对象被删除时的断链检测
    /// </para>
    /// </summary>
    public class SceneObjectProxyData : INodeData
    {
        /// <summary>
        /// 场景对象类型（Point/Area/Entity 等）
        /// </summary>
        public string ObjectType { get; set; } = "";

        /// <summary>
        /// 场景对象 ID（对应 SceneMarker.MarkerId）
        /// </summary>
        public string SceneObjectId { get; set; } = "";

        /// <summary>
        /// 显示名称（从场景对象同步）
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 是否断链（场景对象被删除）
        /// </summary>
        public bool IsBroken { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SceneObjectProxyData(string objectType, string sceneObjectId, string displayName)
        {
            ObjectType = objectType;
            SceneObjectId = sceneObjectId;
            DisplayName = displayName;
            IsBroken = false;
        }

        /// <summary>
        /// 默认构造函数（用于反序列化）
        /// </summary>
        public SceneObjectProxyData()
        {
        }
    }
}
