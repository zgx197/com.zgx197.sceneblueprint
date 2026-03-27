#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Editor.Drawers
{
    /// <summary>
    /// 统一静态场景对象引用在 Editor 蓝图定义侧的约定。
    /// 规则分成两层：
    /// 1. 人类可读候选 / 摘要，只服务蓝图定义层显示。
    /// 2. 稳定运行时身份，始终回写 SceneObjectId，并以 BySceneRef:SceneObjectId 进入运行时主链。
    /// </summary>
    public static class EntityRefSceneIdentityConventions
    {
        public const string SceneMarkerStableIdPrefix = "marker:";

        public const string StableIdentityHelpText =
            "下拉候选只负责蓝图定义层可读性；真正写回并参与运行时解析的是稳定 SceneObjectId。";

        public const string SceneBindingStableIdentityHelpText =
            "当前字段保存的是 MarkerId；真正进入对象语义主链时，会统一映射为 BySceneRef:marker:<MarkerId>。";

        public static string NormalizeSceneObjectId(string? sceneObjectId)
        {
            return string.IsNullOrWhiteSpace(sceneObjectId)
                ? string.Empty
                : sceneObjectId.Trim();
        }

        public static string NormalizeObjectType(string? objectType)
        {
            return string.IsNullOrWhiteSpace(objectType) ? "SceneObject" : objectType.Trim();
        }

        public static string BuildHumanReadableAlias(string sceneObjectId, string? displayName)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            var normalizedSceneObjectId = NormalizeSceneObjectId(sceneObjectId);
            if (string.IsNullOrEmpty(normalizedSceneObjectId))
            {
                return "未命名场景对象";
            }

            var separatorIndex = normalizedSceneObjectId.IndexOf(':');
            if (separatorIndex >= 0 && separatorIndex + 1 < normalizedSceneObjectId.Length)
            {
                return normalizedSceneObjectId[(separatorIndex + 1)..];
            }

            return normalizedSceneObjectId;
        }

        public static string BuildSubjectSummary(
            string sceneObjectId,
            string? displayName,
            string? objectType)
        {
            return $"{BuildHumanReadableAlias(sceneObjectId, displayName)} [{NormalizeObjectType(objectType)}]";
        }

        public static string BuildRuntimeIdentity(string sceneObjectId)
        {
            var normalizedSceneObjectId = NormalizeSceneObjectId(sceneObjectId);
            if (string.IsNullOrEmpty(normalizedSceneObjectId))
            {
                return string.Empty;
            }

            return EntityRefCodec.Serialize(EntityRef.FromSceneRef(normalizedSceneObjectId));
        }

        public static string BuildSceneMarkerStableObjectId(string? markerId)
        {
            var normalizedMarkerId = NormalizeSceneObjectId(markerId);
            if (string.IsNullOrEmpty(normalizedMarkerId))
            {
                return string.Empty;
            }

            return normalizedMarkerId.StartsWith(SceneMarkerStableIdPrefix, StringComparison.Ordinal)
                ? normalizedMarkerId
                : $"{SceneMarkerStableIdPrefix}{normalizedMarkerId}";
        }

        public static EntityRefAuthoringSummary BuildSceneBindingAuthoringSummary(
            string markerId,
            string? displayName,
            string? objectType)
        {
            var stableSceneObjectId = BuildSceneMarkerStableObjectId(markerId);
            if (string.IsNullOrEmpty(stableSceneObjectId))
            {
                return new EntityRefAuthoringSummary(
                    "场景对象",
                    string.Empty,
                    string.Empty,
                    "请选择场景标记；导出时会根据 MarkerId 生成稳定场景对象身份。");
            }

            return new EntityRefAuthoringSummary(
                "场景对象",
                BuildSubjectSummary(stableSceneObjectId, displayName, objectType),
                BuildRuntimeIdentity(stableSceneObjectId),
                SceneBindingStableIdentityHelpText);
        }

        public static int FindCandidateIndex(
            string sceneObjectId,
            IReadOnlyList<EntityRefSceneCandidate>? candidates)
        {
            var normalizedSceneObjectId = NormalizeSceneObjectId(sceneObjectId);
            if (string.IsNullOrEmpty(normalizedSceneObjectId) || candidates == null)
            {
                return -1;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                if (string.Equals(candidates[index].SceneObjectId, normalizedSceneObjectId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        public static bool TryFindCandidate(
            string sceneObjectId,
            IReadOnlyList<EntityRefSceneCandidate>? candidates,
            out EntityRefSceneCandidate candidate)
        {
            candidate = default;
            var candidateIndex = FindCandidateIndex(sceneObjectId, candidates);
            if (candidateIndex < 0 || candidates == null)
            {
                return false;
            }

            candidate = candidates[candidateIndex];
            return true;
        }
    }

    public readonly struct EntityRefAuthoringSummary
    {
        public string ModeLabel { get; }
        public string SummaryText { get; }
        public string RuntimeIdentityText { get; }
        public string HelpText { get; }

        public bool HasSummary => !string.IsNullOrWhiteSpace(SummaryText);
        public bool HasRuntimeIdentity => !string.IsNullOrWhiteSpace(RuntimeIdentityText);
        public bool HasHelpText => !string.IsNullOrWhiteSpace(HelpText);

        public EntityRefAuthoringSummary(
            string modeLabel,
            string summaryText,
            string runtimeIdentityText,
            string helpText)
        {
            ModeLabel = modeLabel ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            RuntimeIdentityText = runtimeIdentityText ?? string.Empty;
            HelpText = helpText ?? string.Empty;
        }
    }
}
