#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime
{
    /// <summary>
    /// [已废弃] 旧版蓝图槽位——已被 SubGraphBindingGroup 替代。
    /// 
    /// 旧模型：多个 Slot 各引用独立 SO。
    /// 新模型：一个关卡一个 SO + SubGraphBindingGroup 按子蓝图 ID 分组绑定。
    /// 
    /// 保留此文件仅用于数据迁移兼容，新代码请使用 SubGraphBindingGroup。
    /// </summary>
    [Obsolete("使用 SubGraphBindingGroup 替代。参见 Phase 4B 设计文档。")]
    [Serializable]
    public class BlueprintSlot
    {
        [Tooltip("槽位名称（仅用于编辑器显示）")]
        public string SlotName = "";

        [Tooltip("蓝图资产")]
        public BlueprintAsset? BlueprintAsset;

        [Tooltip("激活触发条件（如 \"enter_corridor\"、\"boss_defeated\"）")]
        public string Trigger = "";

        [Tooltip("场景绑定列表（自动从蓝图中提取，策划拖入场景对象）")]
        public List<SceneBindingSlot> Bindings = new List<SceneBindingSlot>();

        /// <summary>蓝图是否已分配</summary>
        public bool HasBlueprint => BlueprintAsset != null && !BlueprintAsset.IsEmpty;

        /// <summary>所有绑定是否都已配置</summary>
        public bool AllBindingsConfigured
        {
            get
            {
                foreach (var binding in Bindings)
                {
                    if (!binding.IsBound) return false;
                }
                return true;
            }
        }

        /// <summary>获取未配置的绑定数量</summary>
        public int UnboundCount
        {
            get
            {
                int count = 0;
                foreach (var binding in Bindings)
                {
                    if (!binding.IsBound) count++;
                }
                return count;
            }
        }
    }
}
