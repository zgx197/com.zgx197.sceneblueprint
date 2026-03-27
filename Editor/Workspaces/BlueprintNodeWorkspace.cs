#nullable enable
using System;
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Runtime;
using UnityEditor;

namespace SceneBlueprint.Editor
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BlueprintNodeWorkspaceAttribute : Attribute
    {
        public BlueprintNodeWorkspaceAttribute(int order = 0)
        {
            Order = order;
        }

        public int Order { get; }
    }

    public readonly struct BlueprintNodeWorkspaceContext
    {
        public BlueprintNodeWorkspaceContext(
            string nodeId,
            Node node,
            Graph? graph,
            ActionNodeData data,
            ActionDefinition definition,
            IActionRegistry actionRegistry,
            BindingContext? bindingContext,
            BlueprintAsset? blueprintAsset,
            VariableDeclaration[]? variables)
        {
            NodeId = nodeId ?? string.Empty;
            Node = node;
            Graph = graph;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Definition = definition;
            ActionRegistry = actionRegistry ?? throw new ArgumentNullException(nameof(actionRegistry));
            BindingContext = bindingContext;
            BlueprintAsset = blueprintAsset;
            Variables = variables ?? Array.Empty<VariableDeclaration>();
        }

        public string NodeId { get; }

        public Node Node { get; }

        public Graph? Graph { get; }

        public ActionNodeData Data { get; }

        public ActionDefinition Definition { get; }

        public IActionRegistry ActionRegistry { get; }

        public BindingContext? BindingContext { get; }

        public BlueprintAsset? BlueprintAsset { get; }

        public VariableDeclaration[] Variables { get; }
    }

    public readonly struct BlueprintNodeWorkspaceDrawContext
    {
        public BlueprintNodeWorkspaceDrawContext(EditorWindow window, BlueprintNodeWorkspaceContext nodeContext)
        {
            Window = window;
            NodeContext = nodeContext;
        }

        public EditorWindow Window { get; }

        public BlueprintNodeWorkspaceContext NodeContext { get; }

        public string NodeId => NodeContext.NodeId;

        public ActionNodeData Data => NodeContext.Data;

        public PropertyBag Properties => NodeContext.Data.Properties;
    }

    public interface IBlueprintNodeWorkspaceProvider
    {
        string WorkspaceId { get; }

        bool Supports(BlueprintNodeWorkspaceContext context);

        string GetTitle(BlueprintNodeWorkspaceContext context);

        void Draw(BlueprintNodeWorkspaceDrawContext context, out bool changed);
    }

    internal static class BlueprintNodeWorkspaceRegistry
    {
        private readonly struct WorkspaceRegistration
        {
            public WorkspaceRegistration(IBlueprintNodeWorkspaceProvider provider, int order)
            {
                Provider = provider;
                Order = order;
            }

            public IBlueprintNodeWorkspaceProvider Provider { get; }

            public int Order { get; }
        }

        private static List<WorkspaceRegistration>? s_registrations;

        public static bool TryResolve(
            BlueprintNodeWorkspaceContext context,
            string? preferredWorkspaceId,
            out IBlueprintNodeWorkspaceProvider provider)
        {
            EnsureLoaded();

            if (!string.IsNullOrWhiteSpace(preferredWorkspaceId))
            {
                for (var index = 0; index < s_registrations!.Count; index++)
                {
                    var registration = s_registrations[index];
                    if (!string.Equals(registration.Provider.WorkspaceId, preferredWorkspaceId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (registration.Provider.Supports(context))
                    {
                        provider = registration.Provider;
                        return true;
                    }
                }
            }

            for (var index = 0; index < s_registrations!.Count; index++)
            {
                var registration = s_registrations[index];
                if (!registration.Provider.Supports(context))
                {
                    continue;
                }

                provider = registration.Provider;
                return true;
            }

            provider = null!;
            return false;
        }

        private static void EnsureLoaded()
        {
            if (s_registrations != null)
            {
                return;
            }

            s_registrations = new List<WorkspaceRegistration>();
            var providerTypes = TypeCache.GetTypesDerivedFrom<IBlueprintNodeWorkspaceProvider>();
            for (var index = 0; index < providerTypes.Count; index++)
            {
                var type = providerTypes[index];
                if (type == null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(type) is not IBlueprintNodeWorkspaceProvider provider)
                    {
                        continue;
                    }

                    var attribute = (BlueprintNodeWorkspaceAttribute?)Attribute.GetCustomAttribute(type, typeof(BlueprintNodeWorkspaceAttribute));
                    s_registrations.Add(new WorkspaceRegistration(provider, attribute?.Order ?? 0));
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[BlueprintNodeWorkspaceRegistry] 无法加载工作台 provider {type.FullName}: {ex.Message}");
                }
            }

            s_registrations.Sort(static (left, right) =>
            {
                var orderCompare = left.Order.CompareTo(right.Order);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                return string.Compare(left.Provider.WorkspaceId, right.Provider.WorkspaceId, StringComparison.Ordinal);
            });
        }
    }
}
