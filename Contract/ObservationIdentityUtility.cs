#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    public static class ObservationIdentityUtility
    {
        public static string NormalizeObservationText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (normalized.Contains("  "))
            {
                normalized = normalized.Replace("  ", " ");
            }

            return normalized;
        }

        public static bool AreEquivalentObservationValues(string? left, string? right)
        {
            return string.Equals(
                NormalizeObservationText(left),
                NormalizeObservationText(right),
                StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildParticipantIdentityValue(
            string? summary,
            string? identitySummary,
            string? publicSubjectId,
            string? compiledSubjectId,
            string? runtimeEntityId)
        {
            if (!string.IsNullOrWhiteSpace(identitySummary)
                && !AreEquivalentObservationValues(summary, identitySummary))
            {
                return identitySummary.Trim();
            }

            if (!string.IsNullOrWhiteSpace(publicSubjectId))
            {
                return publicSubjectId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(compiledSubjectId))
            {
                return compiledSubjectId.Trim();
            }

            return runtimeEntityId?.Trim() ?? string.Empty;
        }
    }
}
