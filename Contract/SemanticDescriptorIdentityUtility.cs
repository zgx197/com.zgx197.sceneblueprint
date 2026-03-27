#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    public static class SemanticDescriptorIdentityUtility
    {
        public static string BuildSubjectIdentitySummary(
            IReadOnlyList<SubjectSemanticDescriptor>? subjects,
            bool includeSlot = true)
        {
            if (subjects == null || subjects.Count == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>(subjects.Count);
            for (var index = 0; index < subjects.Count; index++)
            {
                var line = BuildSubjectIdentityLine(subjects[index], includeSlot);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        public static string BuildTargetIdentitySummary(
            IReadOnlyList<TargetSemanticDescriptor>? targets,
            bool includeSlot = true)
        {
            if (targets == null || targets.Count == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>(targets.Count);
            for (var index = 0; index < targets.Count; index++)
            {
                var line = BuildTargetIdentityLine(targets[index], includeSlot);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        public static string BuildSubjectIdentityLine(
            SubjectSemanticDescriptor? descriptor,
            bool includeSlot = true)
        {
            if (descriptor == null)
            {
                return string.Empty;
            }

            return BuildIdentityLine(
                descriptor.Slot,
                descriptor.Summary,
                descriptor.Reference,
                descriptor.CompiledSubjectId,
                descriptor.PublicSubjectId,
                descriptor.RuntimeEntityId,
                includeSlot);
        }

        public static string BuildTargetIdentityLine(
            TargetSemanticDescriptor? descriptor,
            bool includeSlot = true)
        {
            if (descriptor == null)
            {
                return string.Empty;
            }

            return BuildIdentityLine(
                descriptor.Slot,
                descriptor.Summary,
                descriptor.Reference,
                descriptor.CompiledSubjectId,
                descriptor.PublicSubjectId,
                descriptor.RuntimeEntityId,
                includeSlot);
        }

        private static string BuildIdentityLine(
            string? slot,
            string? summary,
            string? reference,
            string? compiledSubjectId,
            string? publicSubjectId,
            string? runtimeEntityId,
            bool includeSlot)
        {
            var parts = new List<string>(5);
            if (includeSlot && !string.IsNullOrWhiteSpace(slot))
            {
                parts.Add(slot.Trim());
            }

            var resolvedSummary = !string.IsNullOrWhiteSpace(summary)
                ? summary.Trim()
                : !string.IsNullOrWhiteSpace(reference)
                    ? reference.Trim()
                    : string.Empty;
            if (!string.IsNullOrWhiteSpace(resolvedSummary))
            {
                parts.Add(resolvedSummary);
            }

            AddIdentityPart(parts, "public", publicSubjectId);
            AddIdentityPart(parts, "compiled", compiledSubjectId);
            AddIdentityPart(parts, "runtime", runtimeEntityId);

            return parts.Count == 0 ? string.Empty : string.Join(" | ", parts);
        }

        private static void AddIdentityPart(List<string> parts, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{label}={value.Trim()}");
            }
        }
    }
}
