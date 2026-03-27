#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class RuntimeStateHost : IRuntimeStateHost
    {
        private readonly IRuntimeStateBackend _backend;
        private readonly StateLifecycleCoordinator _lifecycleCoordinator;
        private readonly List<IRuntimeStateDomain> _domains = new();
        private readonly Dictionary<StateDomainId, IRuntimeStateDomain> _domainsById = new();
        private readonly List<RuntimeStateLifecycleBinding> _lifecycleBindings = new();
        private readonly HashSet<RuntimeStateLifecycleBinding> _lifecycleBindingSet = new();
        private readonly RuntimeSnapshotService? _snapshotService;

        public RuntimeStateHost(
            IRuntimeStateBackend backend,
            StateLifecycleCoordinator lifecycleCoordinator,
            IEnumerable<IRuntimeStateDomain>? domains = null,
            IRuntimeStateInspector? inspector = null,
            IRuntimeSnapshotService? snapshot = null,
            RuntimeSnapshotExporterRegistry? snapshotExporterRegistry = null,
            RuntimeSnapshotSchemaRegistry? snapshotSchemaRegistry = null,
            RuntimeSnapshotReplayRegistry? snapshotReplayRegistry = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _lifecycleCoordinator = lifecycleCoordinator ?? throw new ArgumentNullException(nameof(lifecycleCoordinator));
            SnapshotExporterRegistry = snapshotExporterRegistry ?? new RuntimeSnapshotExporterRegistry();
            SnapshotSchemaRegistry = snapshotSchemaRegistry ?? new RuntimeSnapshotSchemaRegistry();
            SnapshotReplayRegistry = snapshotReplayRegistry ?? new RuntimeSnapshotReplayRegistry();

            if (domains is not null)
            {
                foreach (var domain in domains)
                {
                    RegisterDomain(domain);
                }
            }

            Inspector = inspector ?? new RuntimeStateInspector(_backend, () => _domains);
            if (snapshot is RuntimeSnapshotService concreteSnapshotService)
            {
                _snapshotService = concreteSnapshotService;
                Snapshot = concreteSnapshotService;
            }
            else if (snapshot is not null)
            {
                Snapshot = snapshot;
            }
            else
            {
                _snapshotService = new RuntimeSnapshotService(
                    _backend,
                    () => _domains,
                    SnapshotExporterRegistry,
                    SnapshotSchemaRegistry,
                    SnapshotReplayRegistry);
                Snapshot = _snapshotService;
            }
        }

        public IReadOnlyList<IRuntimeStateDomain> Domains => _domains;

        public IRuntimeStateInspector Inspector { get; }

        public IRuntimeSnapshotService Snapshot { get; }

        public RuntimeSnapshotExporterRegistry SnapshotExporterRegistry { get; }

        public RuntimeSnapshotSchemaRegistry SnapshotSchemaRegistry { get; }

        public RuntimeSnapshotReplayRegistry SnapshotReplayRegistry { get; }

        public IReadOnlyList<RuntimeStateLifecycleBinding> LifecycleBindings => _lifecycleBindings;

        public RuntimeSnapshotReplayResult ReplaySnapshot(RuntimeSnapshot snapshot)
        {
            if (_snapshotService is null)
            {
                throw new InvalidOperationException("Snapshot replay requires a concrete RuntimeSnapshotService instance.");
            }

            return _snapshotService.Replay(snapshot);
        }

        public bool RegisterLifecycleBinding(StateDescriptor descriptor, string slotKey)
        {
            var binding = new RuntimeStateLifecycleBinding(descriptor, slotKey);
            if (!_domainsById.ContainsKey(descriptor.DomainId))
            {
                throw new InvalidOperationException(
                    $"Runtime state domain is not registered: {descriptor.DomainId}");
            }

            if (!_lifecycleBindingSet.Add(binding))
            {
                return false;
            }

            _lifecycleBindings.Add(binding);
            return true;
        }

        public bool TryGetDomain(StateDomainId domainId, out IRuntimeStateDomain domain)
        {
            return _domainsById.TryGetValue(domainId, out domain!);
        }

        public TDomain GetRequiredDomain<TDomain>()
            where TDomain : class, IRuntimeStateDomain
        {
            for (var index = 0; index < _domains.Count; index++)
            {
                if (_domains[index] is TDomain typedDomain)
                {
                    return typedDomain;
                }
            }

            throw new InvalidOperationException($"Required runtime state domain was not found: {typeof(TDomain).FullName}");
        }

        public void HandleLifecycle(RuntimeLifecycleEvent runtimeEvent)
        {
            if (!runtimeEvent.IsValid)
            {
                throw new ArgumentException("Runtime lifecycle event must be valid.", nameof(runtimeEvent));
            }

            for (var index = 0; index < _lifecycleBindings.Count; index++)
            {
                var binding = _lifecycleBindings[index];
                if (!MatchesBinding(runtimeEvent, binding))
                {
                    continue;
                }

                _lifecycleCoordinator.Handle(runtimeEvent, binding.Descriptor, binding.SlotKey);
            }
        }

        private void RegisterDomain(IRuntimeStateDomain? domain)
        {
            if (domain is null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            if (!domain.DomainId.IsValid)
            {
                throw new ArgumentException("Runtime state domain id must be valid.", nameof(domain));
            }

            if (_domainsById.ContainsKey(domain.DomainId))
            {
                throw new InvalidOperationException($"Duplicate runtime state domain id: {domain.DomainId}");
            }

            _domains.Add(domain);
            _domainsById.Add(domain.DomainId, domain);
        }

        private bool MatchesBinding(RuntimeLifecycleEvent runtimeEvent, RuntimeStateLifecycleBinding binding)
        {
            if (!string.IsNullOrWhiteSpace(runtimeEvent.DescriptorId)
                && !string.Equals(binding.Descriptor.Id, runtimeEvent.DescriptorId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!runtimeEvent.EntryRef.HasValue)
            {
                return true;
            }

            if (!_backend.TryGetEntry(runtimeEvent.EntryRef.Value, out var entry) || entry is null)
            {
                return false;
            }

            return binding.Descriptor.DomainId == entry.EntryRef.DomainId
                && string.Equals(binding.Descriptor.Id, entry.Descriptor.Id, StringComparison.Ordinal)
                && string.Equals(binding.SlotKey, entry.SlotKey, StringComparison.Ordinal);
        }
    }
}
