#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 信号标签——层级路径命名的信号标识符。
    /// <para>
    /// 命名约定：使用点分路径，如 "Combat.Monster.Died"、"Spawn.Entity.Created"。
    /// Phase 1 使用精确匹配，Phase 2 支持通配匹配（如 "Combat.*"）。
    /// </para>
    /// <para>
    /// 值类型，零分配。可用作字典 Key（实现了 IEquatable）。
    /// </para>
    /// </summary>
    public readonly struct SignalTag : IEquatable<SignalTag>
    {
        /// <summary>信号路径（如 "Combat.Monster.Died"）</summary>
        public readonly string Path;

        public SignalTag(string path)
        {
            Path = path ?? "";
        }

        /// <summary>是否为空标签</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Path);

        /// <summary>
        /// 层级通配匹配（Phase 2）。
        /// <para>
        /// 支持以下匹配模式：
        /// <list type="bullet">
        ///   <item>精确匹配："Combat.Monster.Died" matches "Combat.Monster.Died"</item>
        ///   <item>尾部通配："Combat.*" matches "Combat.Monster.Died"（匹配 Combat 下的任意子路径）</item>
        ///   <item>单层通配："Combat.*.Died" matches "Combat.Monster.Died"（* 匹配恰好一个层级段）</item>
        ///   <item>空 pattern 不匹配任何标签</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="pattern">匹配模式（如 "Combat.*"、"Combat.*.Died"）</param>
        /// <returns>当前标签是否匹配给定模式</returns>
        public bool MatchesPattern(SignalTag pattern)
        {
            if (pattern.IsEmpty || IsEmpty) return false;

            var patternPath = pattern.Path;
            var tagPath = Path;

            // 快速路径：精确匹配
            if (string.Equals(tagPath, patternPath, StringComparison.Ordinal))
                return true;

            // 尾部通配：pattern 以 ".*" 结尾，表示匹配该前缀下的所有子路径
            if (patternPath.Length >= 2 && patternPath[patternPath.Length - 1] == '*' && patternPath[patternPath.Length - 2] == '.')
            {
                // "Combat.*" → 前缀 = "Combat."
                var prefix = patternPath.Substring(0, patternPath.Length - 1); // 包含末尾的 '.'
                return tagPath.StartsWith(prefix, StringComparison.Ordinal);
            }

            // 单层通配：pattern 中包含 * 但不在末尾，* 匹配恰好一个层级段（不含点号）
            if (patternPath.IndexOf('*') >= 0)
            {
                return MatchesSegmentWildcard(tagPath, patternPath);
            }

            return false;
        }

        /// <summary>
        /// 前缀匹配——检查当前标签是否以指定前缀开头。
        /// <para>用于维度查询，如 HasPrefix("CombatRole") 匹配 "CombatRole.Support"。</para>
        /// </summary>
        /// <param name="prefix">维度前缀（不含末尾点号），如 "CombatRole"</param>
        public bool HasPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix) || IsEmpty) return false;
            // 必须 Path == prefix 或 Path 以 prefix + "." 开头
            return Path.Length > prefix.Length
                   && Path[prefix.Length] == '.'
                   && Path.StartsWith(prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取当前标签在指定维度下的值段。
        /// <para>例如 "CombatRole.Support".GetDimensionValue("CombatRole") → "Support"</para>
        /// </summary>
        /// <param name="dimension">维度前缀，如 "CombatRole"</param>
        /// <returns>值段字符串，不匹配时返回 null</returns>
        public string? GetDimensionValue(string dimension)
        {
            if (!HasPrefix(dimension)) return null;
            return Path.Substring(dimension.Length + 1); // 跳过 "CombatRole."
        }

        /// <summary>段级通配匹配：pattern 中的 * 匹配恰好一个层级段</summary>
        private static bool MatchesSegmentWildcard(string tagPath, string patternPath)
        {
            var tagSegments = tagPath.Split('.');
            var patSegments = patternPath.Split('.');

            if (tagSegments.Length != patSegments.Length)
                return false;

            for (int i = 0; i < patSegments.Length; i++)
            {
                if (patSegments[i] == "*") continue;
                if (!string.Equals(tagSegments[i], patSegments[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        public bool Equals(SignalTag other) => string.Equals(Path, other.Path, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SignalTag other && Equals(other);
        public override int GetHashCode() => Path?.GetHashCode() ?? 0;
        public override string ToString() => Path ?? "";

        public static bool operator ==(SignalTag left, SignalTag right) => left.Equals(right);
        public static bool operator !=(SignalTag left, SignalTag right) => !left.Equals(right);

        /// <summary>从字符串隐式转换</summary>
        public static implicit operator SignalTag(string path) => new SignalTag(path);
    }
}
