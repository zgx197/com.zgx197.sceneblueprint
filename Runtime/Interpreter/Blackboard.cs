#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 黑板——蓝图实例（Local）的变量存储。
    /// <para>
    /// 两条路径严格分离：
    /// • _declared（整型索引）：策划在变量面板声明的变量，O(1) 访问，类型安全。
    /// • _internal（字符串 Key）：框架内部元数据（_activatedBy 等），以 _ 开头，策划不可见。
    /// </para>
    /// <para>
    /// 游戏会话级 Global 变量存储在 <see cref="GlobalBlackboard"/>（静态单例）。
    /// </para>
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<int, IBlackboardEntry> _declared = new();
        private readonly Dictionary<string, object>        _internal = new();

        // ── 策划变量 API（按整型索引）──

        /// <summary>设置声明变量值（写入时通过 <see cref="BlackboardEntry{T}"/> 捕获运行时类型）</summary>
        public void Set<T>(int index, T value) where T : notnull
            => _declared[index] = new BlackboardEntry<T>(value);

        /// <summary>获取声明变量值（不存在则返回 default）</summary>
        public T? Get<T>(int index)
        {
            if (_declared.TryGetValue(index, out var entry) && entry.BoxedValue is T typed)
                return typed;
            return default;
        }

        /// <summary>尝试获取声明变量值</summary>
        public bool TryGet<T>(int index, out T? value)
        {
            if (_declared.TryGetValue(index, out var entry) && entry.BoxedValue is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>是否包含指定声明变量</summary>
        public bool Has(int index) => _declared.ContainsKey(index);

        /// <summary>移除声明变量</summary>
        public bool Remove(int index) => _declared.Remove(index);

        // ── 框架内部元数据 API（key 须以 _ 开头）──

        /// <summary>写入框架内部元数据</summary>
        public void SetInternal(string key, object value) => _internal[key] = value;

        /// <summary>读取框架内部元数据</summary>
        public T? GetInternal<T>(string key)
        {
            if (_internal.TryGetValue(key, out var val) && val is T typed)
                return typed;
            return default;
        }

        /// <summary>尝试读取框架内部元数据</summary>
        public bool TryGetInternal<T>(string key, out T? value)
        {
            if (_internal.TryGetValue(key, out var val) && val is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>是否包含指定内部元数据 key</summary>
        public bool HasInternal(string key) => _internal.ContainsKey(key);

        // ── 生命周期 ──

        /// <summary>清空所有变量（含内部元数据）</summary>
        public void Clear()
        {
            _declared.Clear();
            _internal.Clear();
        }

        /// <summary>已声明变量数量</summary>
        public int DeclaredCount => _declared.Count;

        /// <summary>声明变量的只读视图（快照/调试用，包含值和运行时类型，不用于运行时热路径）</summary>
        public IReadOnlyDictionary<int, IBlackboardEntry> DeclaredEntries => _declared;
    }
}
