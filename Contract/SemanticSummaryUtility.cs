#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 结构化语义摘要工具。
    /// 统一收口 EntityRef / WatchCondition 的可读摘要，供 compiler / runtime / inspector 共用，
    /// 避免这层语义在各节点系统里继续散成重复字符串拼接。
    /// </summary>
    public static class SemanticSummaryUtility
    {
        public static string DescribeSceneBinding(
            string? bindingType,
            string? stableObjectId,
            string? sceneObjectId = null,
            string fallbackText = "")
        {
            var normalizedObjectId = string.IsNullOrWhiteSpace(stableObjectId)
                ? sceneObjectId?.Trim() ?? string.Empty
                : stableObjectId.Trim();
            var normalizedBindingType = string.IsNullOrWhiteSpace(bindingType)
                ? "Binding"
                : bindingType.Trim();

            if (string.IsNullOrWhiteSpace(normalizedObjectId))
            {
                return string.IsNullOrWhiteSpace(fallbackText)
                    ? normalizedBindingType
                    : fallbackText;
            }

            return $"{normalizedBindingType}({normalizedObjectId})";
        }

        public static string DescribeEntityRef(EntityRef? entityRef, string fallbackText = "")
        {
            if (entityRef == null)
            {
                return fallbackText ?? string.Empty;
            }

            return entityRef.Mode switch
            {
                EntityRefMode.ByAlias => string.IsNullOrWhiteSpace(entityRef.Alias)
                    ? "PublicSubject()"
                    : $"PublicSubject({entityRef.Alias.Trim()})",
                EntityRefMode.BySceneRef => string.IsNullOrWhiteSpace(entityRef.SceneObjectId)
                    ? fallbackText ?? string.Empty
                    : $"SceneRef({entityRef.SceneObjectId.Trim()})",
                EntityRefMode.ByRole => string.IsNullOrWhiteSpace(entityRef.Role)
                    ? fallbackText ?? string.Empty
                    : $"Role({entityRef.Role.Trim()})",
                EntityRefMode.ByTag => string.IsNullOrWhiteSpace(entityRef.TagFilter)
                    ? fallbackText ?? string.Empty
                    : $"Tag({entityRef.TagFilter.Trim()})",
                EntityRefMode.ByTags => entityRef.TagFilters == null || entityRef.TagFilters.Length == 0
                    ? fallbackText ?? string.Empty
                    : $"Tags({string.Join("+", entityRef.TagFilters)})",
                EntityRefMode.All => "All",
                EntityRefMode.Any => "Any",
                _ => entityRef.ToString() ?? fallbackText ?? string.Empty
            };
        }

        public static string DescribeEntityRef(string? serialized, string fallbackText = "")
        {
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return fallbackText ?? string.Empty;
            }

            return DescribeEntityRef(EntityRefCodec.Parse(serialized), fallbackText);
        }

        public static string GetCompiledOrFallbackSerializedRef(CompiledEntityRefInfo? compiledRefInfo, string fallbackSerialized)
        {
            return CompiledEntityRefSemanticUtility.Resolve(compiledRefInfo, fallbackSerialized).Serialized;
        }

        public static string GetCompiledOrFallbackSummary(
            CompiledEntityRefInfo? compiledRefInfo,
            string fallbackSerialized,
            string fallbackText = "")
        {
            return CompiledEntityRefSemanticUtility.Resolve(compiledRefInfo, fallbackSerialized, fallbackText).Summary;
        }

        public static string BuildWatchConditionSummary(
            string? conditionType,
            string? targetSummary,
            string? parameterSummary)
        {
            var normalizedConditionType = string.IsNullOrWhiteSpace(conditionType)
                ? "未配置条件"
                : conditionType.Trim();
            var normalizedTarget = string.IsNullOrWhiteSpace(targetSummary)
                ? string.Empty
                : targetSummary.Trim();
            var normalizedParameterSummary = string.IsNullOrWhiteSpace(parameterSummary)
                ? string.Empty
                : parameterSummary.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedTarget) && !string.IsNullOrWhiteSpace(normalizedParameterSummary))
            {
                return $"{normalizedConditionType} | {normalizedTarget} | {normalizedParameterSummary}";
            }

            if (!string.IsNullOrWhiteSpace(normalizedTarget))
            {
                return $"{normalizedConditionType} | {normalizedTarget}";
            }

            if (!string.IsNullOrWhiteSpace(normalizedParameterSummary))
            {
                return $"{normalizedConditionType} | {normalizedParameterSummary}";
            }

            return normalizedConditionType;
        }

        public static string BuildWaitSignalSummary(
            string? signalTag,
            string? subjectFilterSummary,
            bool isWildcardPattern)
        {
            var normalizedTag = string.IsNullOrWhiteSpace(signalTag)
                ? "未配置信号"
                : signalTag.Trim();
            var matchMode = isWildcardPattern ? "通配匹配" : "精确匹配";
            var normalizedSubjectFilter = NormalizeSummaryPart(subjectFilterSummary);

            return string.IsNullOrWhiteSpace(normalizedSubjectFilter)
                ? $"{normalizedTag} | {matchMode}"
                : $"{normalizedTag} | {matchMode} | 主体 {normalizedSubjectFilter}";
        }

        public static string BuildTriggerEnterAreaSummary(
            string? subjectSummary,
            string? triggerAreaSummary,
            bool requireFullyInside)
        {
            var normalizedSubject = NormalizeSummaryPart(subjectSummary, "Player");
            var normalizedTriggerArea = NormalizeSummaryPart(triggerAreaSummary, "未绑定区域");
            var modeSummary = requireFullyInside ? "完全进入" : "中心点进入";
            return $"等待 {normalizedSubject} 进入 {normalizedTriggerArea} | {modeSummary}";
        }

        public static string BuildInteractionApproachSummary(
            string? subjectSummary,
            string? targetSummary,
            float range)
        {
            var normalizedSubject = NormalizeSummaryPart(subjectSummary, "Player");
            var normalizedTarget = NormalizeSummaryPart(targetSummary, "未配置目标");
            return $"等待 {normalizedSubject} 靠近 {normalizedTarget} | 距离 {Math.Max(0f, range).ToString("0.###", CultureInfo.InvariantCulture)}";
        }

        public static string BuildCompositeConditionSummary(
            string? mode,
            IReadOnlyList<string>? connectedInputs)
        {
            var normalizedMode = NormalizeCompositeConditionMode(mode);
            if (connectedInputs == null || connectedInputs.Count == 0)
            {
                return $"{normalizedMode} | 无已连接条件";
            }

            return $"{normalizedMode} | {string.Join(" + ", connectedInputs)}";
        }

        public static string NormalizeCompositeConditionMode(string? mode)
        {
            return string.Equals(mode, "OR", StringComparison.OrdinalIgnoreCase)
                ? "OR"
                : "AND";
        }

        public static string DescribeCompositeConditionMode(string? mode)
        {
            return NormalizeCompositeConditionMode(mode) == "OR"
                ? "任一满足 OR"
                : "全部满足 AND";
        }

        public static string BuildFlowJoinConditionSummary(int requiredCount)
        {
            var normalizedCount = Math.Max(1, requiredCount);
            return $"等待 {normalizedCount} 路输入汇合";
        }

        public static string BuildFlowJoinIncomingSummary(IReadOnlyList<string>? incomingActionLabels)
        {
            if (incomingActionLabels == null || incomingActionLabels.Count == 0)
            {
                return "无上游输入";
            }

            if (incomingActionLabels.Count <= 3)
            {
                return string.Join(" + ", incomingActionLabels);
            }

            return $"{incomingActionLabels.Count} 路输入";
        }

        public static string BuildFlowFilterConditionSummary(string? op, string? constValue)
        {
            var normalizedOperator = string.IsNullOrWhiteSpace(op)
                ? "=="
                : op.Trim();
            var normalizedConstValue = string.IsNullOrWhiteSpace(constValue)
                ? "0"
                : constValue.Trim();
            return $"比较值 {normalizedOperator} {normalizedConstValue}";
        }

        public static string BuildFlowBranchConditionSummary(bool conditionValue)
        {
            return conditionValue
                ? "条件结果 = 真"
                : "条件结果 = 假";
        }

        public static string BuildFlowBranchRouteSummary(bool conditionValue, string? routedPort)
        {
            var normalizedRoute = NormalizeSummaryPart(routedPort, conditionValue ? "true" : "false");
            return $"{BuildFlowBranchConditionSummary(conditionValue)} -> {normalizedRoute}";
        }

        public static string BuildBlackboardAccessSummary(
            string? accessKind,
            string? variableSummary,
            string? valueText = null)
        {
            var normalizedAccessKind = NormalizeBlackboardAccessKind(accessKind);
            var normalizedVariableSummary = NormalizeSummaryPart(variableSummary, "未配置变量");
            var normalizedValueText = NormalizeSummaryPart(valueText);

            if (string.IsNullOrWhiteSpace(normalizedValueText))
            {
                return $"{normalizedAccessKind} {normalizedVariableSummary}";
            }

            return string.Equals(normalizedAccessKind, "读取", StringComparison.Ordinal)
                ? $"{normalizedAccessKind} {normalizedVariableSummary} -> {normalizedValueText}"
                : $"{normalizedAccessKind} {normalizedVariableSummary} <- {normalizedValueText}";
        }

        public static string NormalizeBlackboardAccessKind(string? accessKind)
        {
            return accessKind?.Trim().ToLowerInvariant() switch
            {
                "get" => "读取",
                "set" => "写入",
                _ => string.IsNullOrWhiteSpace(accessKind) ? "访问" : accessKind.Trim(),
            };
        }

        public static string DescribeCompositeConditionPort(string? portId)
        {
            return portId switch
            {
                "cond0" => "条件0",
                "cond1" => "条件1",
                "cond2" => "条件2",
                "cond3" => "条件3",
                _ => string.IsNullOrWhiteSpace(portId) ? "条件" : portId.Trim(),
            };
        }

        private static string NormalizeSummaryPart(string? value, string fallbackText = "")
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallbackText ?? string.Empty
                : value.Trim();
        }
    }
}
