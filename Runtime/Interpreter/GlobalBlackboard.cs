#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 游戏会话级全局黑板——跨蓝图实例共享的变量存储。
    /// <para>
    /// 生命周期：应用运行期间，不持久化到磁盘。
    /// 场景切换时不自动清空，由调用方在合适时机（如返回主菜单）主动调用 <see cref="Clear"/>。
    /// </para>
    /// <para>
    /// 策划变量按 <see cref="VariableDeclaration.Index"/> 访问（整型索引，O(1)）。
    /// 框架内部元数据走独立字符串路径（<see cref="SetInternal"/> / <see cref="GetInternal{T}"/>）。
    /// </para>
    /// </summary>
    public static class GlobalBlackboard
    {
        private static readonly Dictionary<int, IBlackboardEntry> _declared = new();
        private static readonly Dictionary<string, object>        _internal = new();

        // ── 策划变量 API（按整型索引）──

        /// <summary>设置声明变量值（写入时通过 <see cref="BlackboardEntry{T}"/> 捕获运行时类型）</summary>
        public static void Set<T>(int index, T value) where T : notnull
            => _declared[index] = new BlackboardEntry<T>(value);

        /// <summary>获取声明变量值（不存在则返回 default）</summary>
        public static T? Get<T>(int index)
        {
            if (_declared.TryGetValue(index, out var entry) && entry.BoxedValue is T typed)
                return typed;
            return default;
        }

        /// <summary>尝试获取声明变量值</summary>
        public static bool TryGet<T>(int index, out T? value)
        {
            if (_declared.TryGetValue(index, out var entry) && entry.BoxedValue is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>仅在 Key 不存在时写入（用于初始化，避免重复加载时覆盖已有值）</summary>
        public static void SetIfAbsent<T>(int index, T value) where T : notnull
        {
            if (!_declared.ContainsKey(index))
                _declared[index] = new BlackboardEntry<T>(value);
        }

        /// <summary>是否包含指定变量</summary>
        public static bool Has(int index) => _declared.ContainsKey(index);

        // ── 框架内部元数据 API（key 须以 _ 开头）──

        /// <summary>写入框架内部元数据</summary>
        public static void SetInternal(string key, object value) => _internal[key] = value;

        /// <summary>读取框架内部元数据</summary>
        public static T? GetInternal<T>(string key)
        {
            if (_internal.TryGetValue(key, out var val) && val is T typed)
                return typed;
            return default;
        }

        /// <summary>尝试读取框架内部元数据</summary>
        public static bool TryGetInternal<T>(string key, out T? value)
        {
            if (_internal.TryGetValue(key, out var val) && val is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        // ── 生命周期 ──

        /// <summary>清空所有变量（游戏会话结束时调用）</summary>
        public static void Clear()
        {
            _declared.Clear();
            _internal.Clear();
            Debug.Log("[GlobalBlackboard] 已清空所有变量");
        }

        /// <summary>当前声明变量数量</summary>
        public static int DeclaredCount => _declared.Count;
    }
}
