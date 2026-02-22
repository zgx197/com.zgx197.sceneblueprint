#nullable enable
using System;
using UnityEngine;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime
{
    /// <summary>
    /// 单个场景绑定槽位。
    /// 将蓝图中声明的 SceneBinding 属性（如 "spawnArea"）映射到场景中的具体 GameObject。
    /// 
    /// 设计原则：
    /// - bindingKey 使用 scoped 形式（nodeId/bindingKey），每个节点实例拥有独立作用域
    /// - boundObject 由策划在 Inspector 中拖入场景对象
    /// - 导出时根据 bindingType 从 boundObject 提取数据（坐标/顶点/路径点/ID）
    /// </summary>
    [Serializable]
    public class SceneBindingSlot
    {
        [Tooltip("绑定键名（scopedBindingKey：nodeId/bindingKey）")]
        public string BindingKey = "";

        [Tooltip("绑定类型（决定导出时如何提取数据）")]
        public BindingType BindingType;

        [Tooltip("显示名称（来自 PropertyDefinition.DisplayName）")]
        public string DisplayName = "";

        [Tooltip("所属节点的 ActionTypeId（用于定位来源）")]
        public string SourceActionTypeId = "";

        [Tooltip("绑定的场景对象")]
        public GameObject? BoundObject;

        /// <summary>绑定是否已配置</summary>
        public bool IsBound => BoundObject != null;
    }
}
