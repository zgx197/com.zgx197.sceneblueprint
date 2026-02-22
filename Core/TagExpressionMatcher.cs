#nullable enable
using System;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Tag 表达式匹配器（Phase 5 - M13）。
    /// </summary>
    public static class TagExpressionMatcher
    {
        private static readonly char[] ExpressionSeparators = { ',', ';', '|', '\n', '\r' };

        /// <summary>
        /// 评估表达式是否匹配指定 tag。
        /// 语法：多个模式用逗号/分号/竖线分隔，任一命中即返回 true。
        /// </summary>
        public static bool Evaluate(string? expression, string? tag)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            if (string.IsNullOrWhiteSpace(tag))
                return false;

            var patterns = expression.Split(ExpressionSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawPattern in patterns)
            {
                var pattern = rawPattern.Trim();
                if (pattern.Length == 0)
                    continue;

                if (IsPatternMatch(tag, pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 单个模式匹配。
        /// 规则：
        /// - "*" 匹配任意 tag；
        /// - 不含 '*' 时：精确或前缀匹配（Combat.Spawn 匹配 Combat.Spawn.Point）；
        /// - 含 '*' 时：支持分段通配（Combat.*.Elite）。
        /// </summary>
        public static bool IsPatternMatch(string? tag, string? pattern)
        {
            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(pattern))
                return false;

            var normalizedTag = tag.Trim();
            var normalizedPattern = pattern.Trim();

            if (normalizedPattern == "*")
                return true;

            if (normalizedPattern.IndexOf('*') < 0)
                return IsExactOrPrefixMatch(normalizedTag, normalizedPattern);

            if (normalizedPattern.EndsWith(".*", StringComparison.Ordinal))
            {
                var prefix = normalizedPattern.Substring(0, normalizedPattern.Length - 2);
                return IsExactOrPrefixMatch(normalizedTag, prefix);
            }

            return IsWildcardSegmentMatch(normalizedTag, normalizedPattern);
        }

        /// <summary>
        /// 精确或前缀匹配（大小写不敏感）。
        /// </summary>
        public static bool IsExactOrPrefixMatch(string? tag, string? prefixOrExact)
        {
            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(prefixOrExact))
                return false;

            var normalizedTag = tag.Trim();
            var normalizedPrefix = prefixOrExact.Trim().TrimEnd('.');
            if (normalizedPrefix.Length == 0)
                return false;

            if (normalizedTag.Equals(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            return normalizedTag.StartsWith(normalizedPrefix + ".", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWildcardSegmentMatch(string tag, string pattern)
        {
            var tagSegments = tag.Split('.');
            var patternSegments = pattern.Split('.');

            if (tagSegments.Length != patternSegments.Length)
                return false;

            for (int i = 0; i < patternSegments.Length; i++)
            {
                var patternSegment = patternSegments[i].Trim();
                var tagSegment = tagSegments[i].Trim();

                if (patternSegment.Length == 0 || tagSegment.Length == 0)
                    return false;

                if (patternSegment == "*")
                    continue;

                if (!tagSegment.Equals(patternSegment, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }
}
