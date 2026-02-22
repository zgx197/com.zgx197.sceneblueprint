#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  属性值存储容器 (PropertyBag)
    //
    //  PropertyBag 是一个简单的键值对存储，保存一个行动节点的所有属性值。
    //  它的角色类似于 GAS 中的 AttributeSet——存储属性的实际数据。
    //
    //  为什么不用强类型字段？
    //  因为行动类型是动态定义的（通过 ActionDefinition），
    //  不同行动有不同的属性集合，无法用固定的 C# 类表示。
    //  PropertyBag 用 Dictionary<string, object> 提供灵活的存储。
    //
    //  数据流向：
    //  ActionDefinition.Properties    →  “属性应该长什么样”（元数据）
    //  PropertyBag                    →  “属性实际是什么值”（实例数据）
    //  PropertyBagSerializer          →  JSON 序列化/反序列化
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 属性值存储容器——节点的属性值存储在此，而非引用外部 ScriptableObject。
    /// <para>
    /// 使用示例：
    /// <code>
    /// var bag = new PropertyBag();
    /// bag.Set("monstersPerWave", 5);           // 设置整数
    /// bag.Set("interval", 2.5f);               // 设置浮点数
    /// bag.Set("template", "elite_group_01");   // 设置字符串
    /// 
    /// int count = bag.Get&lt;int&gt;("monstersPerWave");         // 读取
    /// float rate = bag.Get&lt;float&gt;("missing", 1.0f);        // 读取不存在的键，返回默认值
    /// string json = PropertyBagSerializer.ToJson(bag);      // 序列化为 JSON
    /// </code>
    /// </para>
    /// </summary>
    public class PropertyBag
    {
        /// <summary>内部存储——用 Dictionary 存储所有属性的键值对</summary>
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        /// <summary>
        /// 设置属性值。如果键已存在则覆盖。
        /// </summary>
        /// <param name="key">属性键名，对应 PropertyDefinition.Key</param>
        /// <param name="value">属性值，可以是 int/float/bool/string 等基本类型</param>
        public void Set(string key, object value)
        {
            _values[key] = value;
        }

        /// <summary>
        /// 获取属性值，并转换为指定类型。
        /// <para>如果键不存在或类型不匹配，返回 defaultValue。</para>
        /// <para>支持数值类型的隐式转换（如 int → float），
        /// 这在 JSON 反序列化后特别有用。</para>
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="key">属性键名</param>
        /// <param name="defaultValue">键不存在时的回退值</param>
        public T Get<T>(string key, T defaultValue = default!)
        {
            if (_values.TryGetValue(key, out var value))
            {
                // 类型完全匹配，直接返回
                if (value is T typed)
                    return typed;

                // 处理数值类型的隐式转换（如 int → float, long → int 等）
                // 场景：JSON 反序列化后，整数可能被解析为 int，但调用方期望读取为 float
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // 类型无法转换，返回默认值
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>检查是否包含指定键（不关心值的类型）</summary>
        public bool Has(string key) => _values.ContainsKey(key);

        /// <summary>移除指定键。返回 true 表示成功移除，false 表示键不存在。</summary>
        public bool Remove(string key) => _values.Remove(key);

        /// <summary>获取所有键值对的只读视图——用于序列化和调试</summary>
        public IReadOnlyDictionary<string, object> All => _values;

        /// <summary>当前存储的属性数量</summary>
        public int Count => _values.Count;

        /// <summary>清空所有属性值</summary>
        public void Clear() => _values.Clear();

        /// <summary>
        /// 获取属性值的原始 object，不存在时返回 null。
        /// <para>主要用于 VisibleWhenEvaluator 等需要处理多种类型的场景。</para>
        /// </summary>
        public object? GetRaw(string key)
        {
            return _values.TryGetValue(key, out var value) ? value : null;
        }
    }
}
