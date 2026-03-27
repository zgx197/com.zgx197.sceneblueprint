#nullable enable
namespace SceneBlueprint.Contract
{
    /// <summary>
    /// WaitSignal 最小语义工具。
    /// 收口 signalTag 规范化、wildcard 判定和 fallback 摘要拼装，
    /// 避免 editor compiler 与 runtime fallback 在不同位置重复维护这套规则。
    /// </summary>
    public static class WaitSignalSemanticUtility
    {
        public static string NormalizeSignalTag(string? signalTag)
        {
            return string.IsNullOrWhiteSpace(signalTag)
                ? string.Empty
                : signalTag.Trim();
        }

        public static bool IsWildcardPattern(string? signalTag)
        {
            return NormalizeSignalTag(signalTag).IndexOf('*') >= 0;
        }

        public static string BuildConditionSummary(
            string? signalTag,
            string? subjectFilterSummary)
        {
            var normalizedTag = NormalizeSignalTag(signalTag);
            return SemanticSummaryUtility.BuildWaitSignalSummary(
                normalizedTag,
                subjectFilterSummary,
                IsWildcardPattern(normalizedTag));
        }
    }
}
