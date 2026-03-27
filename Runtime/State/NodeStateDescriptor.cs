#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class NodeStateDescriptor<TState>
        where TState : class
    {
        public NodeStateDescriptor(
            string id,
            StateLifetime lifetime,
            Func<TState> factory,
            string? debugName = null,
            bool isInspectable = true,
            bool allowSnapshot = true,
            string? defaultSlotKey = null)
        {
            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            Descriptor = new StateDescriptor(
                id,
                NodePrivateStateDomain.NodePrivateDomainId,
                lifetime,
                debugName,
                isInspectable,
                allowSnapshot);
            Factory = factory;
            DefaultSlotKey = string.IsNullOrWhiteSpace(defaultSlotKey) ? Descriptor.Id : defaultSlotKey;
        }

        public StateDescriptor Descriptor { get; }

        public Func<TState> Factory { get; }

        public string DefaultSlotKey { get; }
    }
}
