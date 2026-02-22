#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 场景绑定上下文——在编辑器内存中维护 SceneBinding 属性到 GameObject 的映射。
    /// 
    /// 绑定数据不存储在 PropertyBag（Core 层无 Unity 引用）中，
    /// 而是由此上下文持有，保存时写入场景绑定存储，加载时从存储恢复。
    /// 
    /// 键格式："scopedBindingKey"（nodeId/bindingKey）。
    /// 每个节点实例拥有独立的绑定作用域，同名 bindingKey 在不同节点间互不冲突。
    /// </summary>
    public class BindingContext
    {
        private readonly Dictionary<string, GameObject?> _bindings = new Dictionary<string, GameObject?>();

        /// <summary>获取绑定的 GameObject，未绑定时返回 null</summary>
        public GameObject? Get(string bindingKey)
        {
            _bindings.TryGetValue(bindingKey, out var obj);
            return obj;
        }

        /// <summary>设置绑定</summary>
        public void Set(string bindingKey, GameObject? obj)
        {
            _bindings[bindingKey] = obj;
        }

        /// <summary>移除绑定</summary>
        public void Remove(string bindingKey)
        {
            _bindings.Remove(bindingKey);
        }

        /// <summary>清空所有绑定</summary>
        public void Clear()
        {
            _bindings.Clear();
        }

        /// <summary>获取所有绑定（只读遍历）</summary>
        public IEnumerable<KeyValuePair<string, GameObject?>> All => _bindings;

        /// <summary>绑定数量</summary>
        public int Count => _bindings.Count;

        /// <summary>已配置的绑定数量（GameObject 不为 null）</summary>
        public int BoundCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _bindings)
                {
                    if (kvp.Value != null) count++;
                }
                return count;
            }
        }
    }
}
