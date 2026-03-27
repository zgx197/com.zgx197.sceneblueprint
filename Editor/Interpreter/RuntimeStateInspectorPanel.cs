#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Editor.Interpreter
{
    public sealed class RuntimeStateInspectorPanelModel
    {
        public RuntimeStateInspectorPanelModel(
            string filterText,
            string selectedLogicalEntryKey,
            IReadOnlyList<RuntimeStatePresentationViewModel> visiblePresentations,
            RuntimeStatePresentationViewModel? selectedPresentation,
            int totalEntryCount,
            int supportedEntryCount)
        {
            FilterText = filterText ?? string.Empty;
            SelectedLogicalEntryKey = selectedLogicalEntryKey ?? string.Empty;
            VisiblePresentations = visiblePresentations ?? Array.Empty<RuntimeStatePresentationViewModel>();
            SelectedPresentation = selectedPresentation;
            TotalEntryCount = Math.Max(0, totalEntryCount);
            SupportedEntryCount = Math.Max(0, supportedEntryCount);
        }

        public string FilterText { get; }

        public string SelectedLogicalEntryKey { get; }

        public IReadOnlyList<RuntimeStatePresentationViewModel> VisiblePresentations { get; }

        public RuntimeStatePresentationViewModel? SelectedPresentation { get; }

        public int TotalEntryCount { get; }

        public int SupportedEntryCount { get; }

        public int VisibleEntryCount => VisiblePresentations.Count;

        public bool HasVisiblePresentations => VisiblePresentations.Count > 0;
    }

    public static class RuntimeStateInspectorPanelBuilder
    {
        public static RuntimeStateInspectorPanelModel Build(
            RuntimeStatePresentationResult presentationResult,
            string? filterText,
            string? selectedLogicalEntryKey)
        {
            if (presentationResult == null)
            {
                throw new ArgumentNullException(nameof(presentationResult));
            }

            var normalizedFilter = (filterText ?? string.Empty).Trim();
            var visiblePresentations = new List<RuntimeStatePresentationViewModel>(presentationResult.Presentations.Count);
            for (var index = 0; index < presentationResult.Presentations.Count; index++)
            {
                var presentation = presentationResult.Presentations[index];
                if (MatchesFilter(presentation, normalizedFilter))
                {
                    visiblePresentations.Add(presentation);
                }
            }

            RuntimeStatePresentationViewModel? selectedPresentation = null;
            var normalizedSelectedKey = selectedLogicalEntryKey ?? string.Empty;
            if (visiblePresentations.Count > 0)
            {
                for (var index = 0; index < visiblePresentations.Count; index++)
                {
                    var candidate = visiblePresentations[index];
                    if (!string.Equals(candidate.Summary.LogicalEntryKey, normalizedSelectedKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    selectedPresentation = candidate;
                    break;
                }

                if (selectedPresentation == null)
                {
                    selectedPresentation = visiblePresentations[0];
                    normalizedSelectedKey = selectedPresentation.Summary.LogicalEntryKey;
                }
            }
            else
            {
                normalizedSelectedKey = string.Empty;
            }

            return new RuntimeStateInspectorPanelModel(
                normalizedFilter,
                normalizedSelectedKey,
                visiblePresentations,
                selectedPresentation,
                presentationResult.TotalEntryCount,
                presentationResult.SupportedEntryCount);
        }

        private static bool MatchesFilter(RuntimeStatePresentationViewModel presentation, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                return true;
            }

            return ContainsIgnoreCase(presentation.Summary.Title, filterText)
                || ContainsIgnoreCase(presentation.Summary.Subtitle, filterText)
                || ContainsIgnoreCase(presentation.Summary.SummaryText, filterText)
                || ContainsIgnoreCase(presentation.Summary.ActionId, filterText)
                || ContainsIgnoreCase(presentation.Summary.ActionTypeId, filterText)
                || ContainsIgnoreCase(presentation.Summary.LogicalEntryKey, filterText)
                || MatchesDetailFields(presentation.Detail.Fields, filterText);
        }

        private static bool ContainsIgnoreCase(string? source, string filterText)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesDetailFields(
            IReadOnlyList<RuntimeStateDetailFieldViewModel> fields,
            string filterText)
        {
            for (var index = 0; index < fields.Count; index++)
            {
                if (ContainsIgnoreCase(fields[index].Label, filterText)
                    || ContainsIgnoreCase(fields[index].Value, filterText))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
