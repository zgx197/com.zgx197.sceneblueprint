#nullable enable
using System;
using System.Globalization;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 类型化属性键——将属性字符串 Key 与预期返回类型 <typeparamref name="T"/> 绑定。
    /// <para>
    /// 替代直接使用 <c>string</c> 常量访问 <c>BlueprintFrame.GetProperty</c>，
    /// 消除"键拼写正确但类型转换错误"的静默失败风险。
    /// </para>
    /// <para>
    /// 用法示例：
    /// <code>
    /// // 声明（在 ActionPortIds 中）
    /// public static readonly PropertyKey&lt;float&gt; Duration = new("duration");
    ///
    /// // 使用（在 System 中）
    /// float dur = frame.GetProperty(idx, ActionPortIds.FlowDelay.Duration);
    /// </code>
    /// </para>
    /// </summary>
    public readonly struct PropertyKey<T>
    {
        /// <summary>底层字符串键（与 ActionEntry.Properties 中的 Key 一一对应）</summary>
        public readonly string Key;

        public PropertyKey(string key) => Key = key;

        /// <summary>允许向需要 string 的旧 API 隐式传递（向后兼容）</summary>
        public static implicit operator string(PropertyKey<T> pk) => pk.Key;

        public override string ToString() => Key;
    }

    /// <summary>
    /// <see cref="PropertyKey{T}"/> 的内置类型解析器。
    /// 提供 float / int / bool / string 四种基础类型的字符串→值转换，
    /// 解析失败时返回对应类型的 default，而非抛出异常。
    /// </summary>
    public static class PropertyKeyParser
    {
        /// <summary>将 raw 字符串解析为 <typeparamref name="T"/>；无法解析时返回 default。</summary>
        public static T Parse<T>(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return default!;

            Type t = typeof(T);

            if (t == typeof(string))  return (T)(object)raw;
            if (t == typeof(float))   return (T)(object)ParseFloat(raw);
            if (t == typeof(int))     return (T)(object)ParseInt(raw);
            if (t == typeof(bool))    return (T)(object)ParseBool(raw);
            if (t == typeof(double))  return (T)(object)ParseDouble(raw);

            return default!;
        }

        private static float ParseFloat(string s)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

        private static int ParseInt(string s)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static bool ParseBool(string s)
            => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";

        private static double ParseDouble(string s)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;
    }
}
