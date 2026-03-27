#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// CompiledSceneBindingInfo 最小语义工具。
    /// 统一收口 scene binding 的摘要与 compiled metadata 构造，避免这层 fallback 语义继续散在 compiler/runtime 内部。
    /// </summary>
    public static class CompiledSceneBindingSemanticUtility
    {
        public static CompiledSceneBindingInfo BuildInfo(
            string? bindingKey,
            string? bindingType,
            string? sceneObjectId,
            string? stableObjectId,
            string? spatialPayloadJson)
        {
            return new CompiledSceneBindingInfo
            {
                BindingKey = bindingKey?.Trim() ?? string.Empty,
                BindingType = bindingType?.Trim() ?? string.Empty,
                SceneObjectId = sceneObjectId?.Trim() ?? string.Empty,
                StableObjectId = stableObjectId?.Trim() ?? string.Empty,
                SpatialPayloadJson = spatialPayloadJson?.Trim() ?? string.Empty,
                Summary = BuildSummary(bindingType, stableObjectId, sceneObjectId),
            };
        }

        public static string GetSummary(
            CompiledSceneBindingInfo? compiledBindingInfo,
            string? fallbackBindingType,
            string? fallbackStableObjectId,
            string? fallbackSceneObjectId,
            string fallbackText = "")
        {
            return !string.IsNullOrWhiteSpace(compiledBindingInfo?.Summary)
                ? compiledBindingInfo.Summary.Trim()
                : BuildSummary(
                    fallbackBindingType,
                    fallbackStableObjectId,
                    fallbackSceneObjectId,
                    fallbackText);
        }

        public static string BuildSummary(
            string? bindingType,
            string? stableObjectId,
            string? sceneObjectId,
            string fallbackText = "")
        {
            return SemanticSummaryUtility.DescribeSceneBinding(
                bindingType,
                stableObjectId,
                sceneObjectId,
                fallbackText);
        }
    }
}
