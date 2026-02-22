#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime
{
    /// <summary>
    /// 子蓝图绑定分组——一个子蓝图（SubGraphFrame）中所有 SceneBinding 的集合。
    /// 
    /// 由蓝图编辑器的"同步到场景"功能自动创建和维护，策划无需手动操作。
    /// </summary>
    [Serializable]
    public class SubGraphBindingGroup
    {
        [Tooltip("子蓝图框的 ID（对应 SubGraphFrame.Id）")]
        public string SubGraphFrameId = "";

        [Tooltip("子蓝图名称（用于编辑器显示）")]
        public string SubGraphTitle = "";

        [Tooltip("场景绑定列表")]
        public List<SceneBindingSlot> Bindings = new List<SceneBindingSlot>();

        /// <summary>
        /// 根据 scopedBindingKey（nodeId/bindingKey）精确查找绑定。
        /// </summary>
        public SceneBindingSlot? FindBinding(string bindingKey)
        {
            if (string.IsNullOrEmpty(bindingKey))
                return null;

            foreach (var b in Bindings)
            {
                if (b.BindingKey == bindingKey)
                    return b;
            }

            return null;
        }

        /// <summary>所有绑定是否都已配置</summary>
        public bool AllBindingsConfigured
        {
            get
            {
                foreach (var b in Bindings)
                {
                    if (!b.IsBound) return false;
                }
                return true;
            }
        }
    }
}
