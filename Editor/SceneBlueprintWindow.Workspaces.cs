#nullable enable
using System;
using System.Linq;
using SceneBlueprint.Core;
using UnityEditor;

namespace SceneBlueprint.Editor
{
    public partial class SceneBlueprintWindow
    {
        public static bool TryGetSelectedNodeWorkspaceContext(out BlueprintNodeWorkspaceContext context)
        {
            if (TryResolvePreferredWindow(out var preferredWindow)
                && preferredWindow.TryGetSelectedNodeWorkspaceContextCore(out context))
            {
                return true;
            }

            var windows = UnityEngine.Resources.FindObjectsOfTypeAll<SceneBlueprintWindow>();
            for (var index = 0; index < windows.Length; index++)
            {
                var window = windows[index];
                if (window == null || ReferenceEquals(window, preferredWindow))
                {
                    continue;
                }

                if (window.TryGetSelectedNodeWorkspaceContextCore(out context))
                {
                    return true;
                }
            }

            context = default;
            return false;
        }

        public static bool TryGetNodeWorkspaceContext(string nodeId, out BlueprintNodeWorkspaceContext context)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                context = default;
                return false;
            }

            if (TryResolvePreferredWindow(out var preferredWindow)
                && preferredWindow.TryCreateWorkspaceContext(nodeId, out context))
            {
                return true;
            }

            var windows = UnityEngine.Resources.FindObjectsOfTypeAll<SceneBlueprintWindow>();
            for (var index = 0; index < windows.Length; index++)
            {
                var window = windows[index];
                if (window == null || ReferenceEquals(window, preferredWindow))
                {
                    continue;
                }

                if (window.TryCreateWorkspaceContext(nodeId, out context))
                {
                    return true;
                }
            }

            context = default;
            return false;
        }

        public static bool TryFocusNodeInEditor(string nodeId)
        {
            if (!TryResolveWindowContainingNode(nodeId, out var window))
            {
                return false;
            }

            var selection = window._session?.ViewModel.Selection;
            if (selection == null)
            {
                return false;
            }

            selection.Select(nodeId);
            window._session?.ViewModel.RequestRepaint();
            window.Repaint();
            return true;
        }

        public static bool NotifyWorkspaceNodeChanged(string nodeId)
        {
            if (!TryResolveWindowContainingNode(nodeId, out var window)
                || window._session == null)
            {
                return false;
            }

            var node = window._session.ViewModel.Graph.FindNode(nodeId);
            if (node?.UserData is not ActionNodeData nodeData)
            {
                return false;
            }

            window._session.NotifyNodePropertyChanged(nodeId, nodeData);
            window._session.ScheduleAnalysis();
            window.MarkAutosaveGraphDirty();
            window._session.ViewModel.RequestRepaint();
            window.Repaint();
            return true;
        }

        private static bool TryResolvePreferredWindow(out SceneBlueprintWindow? window)
        {
            window = EditorWindow.focusedWindow as SceneBlueprintWindow;
            if (window != null && window._session != null)
            {
                return true;
            }

            window = UnityEngine.Resources.FindObjectsOfTypeAll<SceneBlueprintWindow>()
                .FirstOrDefault(candidate => candidate != null && candidate._session != null);
            return window != null;
        }

        private static bool TryResolveWindowContainingNode(string nodeId, out SceneBlueprintWindow? window)
        {
            window = null;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (TryResolvePreferredWindow(out var preferredWindow)
                && preferredWindow.TryContainsNode(nodeId))
            {
                window = preferredWindow;
                return true;
            }

            var windows = UnityEngine.Resources.FindObjectsOfTypeAll<SceneBlueprintWindow>();
            for (var index = 0; index < windows.Length; index++)
            {
                var candidate = windows[index];
                if (candidate == null || ReferenceEquals(candidate, preferredWindow))
                {
                    continue;
                }

                if (!candidate.TryContainsNode(nodeId))
                {
                    continue;
                }

                window = candidate;
                return true;
            }

            return false;
        }

        private bool TryGetSelectedNodeWorkspaceContextCore(out BlueprintNodeWorkspaceContext context)
        {
            var selectedNodeIds = _session?.ViewModel.Selection.SelectedNodeIds;
            if (selectedNodeIds == null)
            {
                context = default;
                return false;
            }

            foreach (var nodeId in selectedNodeIds)
            {
                if (TryCreateWorkspaceContext(nodeId, out context))
                {
                    return true;
                }
            }

            context = default;
            return false;
        }

        private bool TryCreateWorkspaceContext(string nodeId, out BlueprintNodeWorkspaceContext context)
        {
            context = default;
            if (_session == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var node = _session.ViewModel.Graph.FindNode(nodeId);
            if (node?.UserData is not ActionNodeData actionData)
            {
                return false;
            }

            if (!_session.ActionRegistry.TryGet(actionData.ActionTypeId, out var definition))
            {
                return false;
            }

            context = new BlueprintNodeWorkspaceContext(
                nodeId,
                node,
                _session.ViewModel.Graph,
                actionData,
                definition,
                _session.ActionRegistry,
                _session.BindingContextPublic,
                _currentAsset,
                _currentAsset?.Variables);
            return true;
        }

        private bool TryContainsNode(string nodeId)
        {
            return _session != null
                && !string.IsNullOrWhiteSpace(nodeId)
                && _session.ViewModel.Graph.FindNode(nodeId) != null;
        }
    }
}
