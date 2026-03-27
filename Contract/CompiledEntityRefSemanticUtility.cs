#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// CompiledEntityRefInfo 最小语义工具。
    /// 统一收口 entity ref 的规范化、compiled-or-fallback 解析与摘要生成，
    /// 避免 editor compiler、runtime fallback 和 event context 在不同位置重复维护同一套逻辑。
    /// </summary>
    public static class CompiledEntityRefSemanticUtility
    {
        public static CompiledEntityRefInfo BuildInfo(string? serialized)
        {
            var normalizedSerialized = NormalizeSerialized(serialized);
            if (string.IsNullOrEmpty(normalizedSerialized))
            {
                return new CompiledEntityRefInfo();
            }

            var entityRef = EntityRefCodec.Parse(normalizedSerialized);
            return new CompiledEntityRefInfo
            {
                Serialized = normalizedSerialized,
                Summary = SemanticSummaryUtility.DescribeEntityRef(entityRef),
            };
        }

        public static ResolvedEntityRefSemantic Resolve(
            CompiledEntityRefInfo? compiledRefInfo,
            string? fallbackSerialized,
            string fallbackText = "")
        {
            var serialized = !string.IsNullOrWhiteSpace(compiledRefInfo?.Serialized)
                ? NormalizeSerialized(compiledRefInfo.Serialized)
                : NormalizeSerialized(fallbackSerialized);
            var entityRef = string.IsNullOrEmpty(serialized)
                ? new EntityRef()
                : EntityRefCodec.Parse(serialized);
            var summary = !string.IsNullOrWhiteSpace(compiledRefInfo?.Summary)
                ? compiledRefInfo.Summary.Trim()
                : SemanticSummaryUtility.DescribeEntityRef(entityRef, fallbackText);

            return new ResolvedEntityRefSemantic
            {
                Serialized = serialized,
                Summary = summary,
                EntityRef = entityRef,
            };
        }

        public static string NormalizeSerialized(string? serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return string.Empty;
            }

            return EntityRefCodec.Serialize(EntityRefCodec.Parse(serialized));
        }
    }

    public struct ResolvedEntityRefSemantic
    {
        public string Serialized;
        public string Summary;
        public EntityRef EntityRef;
    }
}
