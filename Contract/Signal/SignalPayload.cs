#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 信号载荷——键值对形式的信号附带数据。
    /// <para>
    /// 所有值以字符串存储，消费方按需解析。
    /// 设计理念：保持框架层与业务层解耦，框架不知道具体的载荷结构。
    /// </para>
    /// <para>
    /// 使用示例：
    /// <code>
    /// var payload = new SignalPayload { ["EntityId"] = "42", ["Damage"] = "100" };
    /// string entityId = payload["EntityId"];
    /// int damage = payload.GetInt("Damage");
    /// </code>
    /// </para>
    /// </summary>
    public class SignalPayload
    {
        /// <summary>空载荷单例（避免频繁创建空字典）</summary>
        public static readonly SignalPayload Empty = new();

        private Dictionary<string, string>? _data;

        /// <summary>键值对数量</summary>
        public int Count => _data?.Count ?? 0;

        /// <summary>是否为空</summary>
        public bool IsEmpty => _data == null || _data.Count == 0;

        /// <summary>索引器——读写载荷值</summary>
        public string this[string key]
        {
            get => _data != null && _data.TryGetValue(key, out var val) ? val : "";
            set
            {
                _data ??= new Dictionary<string, string>();
                _data[key] = value;
            }
        }

        /// <summary>尝试获取指定 Key 的值</summary>
        public bool TryGetValue(string key, out string value)
        {
            if (_data != null && _data.TryGetValue(key, out value!))
                return true;
            value = "";
            return false;
        }

        /// <summary>获取整数值（解析失败返回 defaultValue）</summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            if (TryGetValue(key, out var raw) && int.TryParse(raw, out var result))
                return result;
            return defaultValue;
        }

        /// <summary>获取浮点值（解析失败返回 defaultValue）</summary>
        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (TryGetValue(key, out var raw) && float.TryParse(raw, out var result))
                return result;
            return defaultValue;
        }

        /// <summary>获取布尔值（解析失败返回 defaultValue）</summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            if (TryGetValue(key, out var raw) && bool.TryParse(raw, out var result))
                return result;
            return defaultValue;
        }

        /// <summary>是否包含指定 Key</summary>
        public bool ContainsKey(string key) => _data != null && _data.ContainsKey(key);

        /// <summary>获取所有键（用于遍历）</summary>
        public IEnumerable<string> Keys => _data != null ? _data.Keys : (IEnumerable<string>)Array.Empty<string>();

        public override string ToString()
        {
            if (_data == null || _data.Count == 0) return "{}";
            var parts = new string[_data.Count];
            int i = 0;
            foreach (var kv in _data)
                parts[i++] = $"{kv.Key}={kv.Value}";
            return "{" + string.Join(", ", parts) + "}";
        }
    }
}
