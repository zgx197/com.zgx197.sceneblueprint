#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    public static class RuntimeStateHostBootstrapSupport
    {
        public static RuntimeStateHost? EnsureRuntimeStateHost(BlueprintFrame frame)
        {
            return EnsureRuntimeStateHost(frame, out _, out _, out _);
        }

        public static RuntimeStateHost? EnsureRuntimeStateHost(
            BlueprintFrame? frame,
            out RuntimeSnapshotExporterRegistry? exporterRegistry,
            out RuntimeSnapshotSchemaRegistry? schemaRegistry,
            out RuntimeSnapshotReplayRegistry? replayRegistry)
        {
            return EnsureRuntimeStateHost(frame?.Runner, out exporterRegistry, out schemaRegistry, out replayRegistry);
        }

        public static RuntimeStateHost? EnsureRuntimeStateHost(BlueprintRunner? runner)
        {
            return EnsureRuntimeStateHost(runner, out _, out _, out _);
        }

        public static RuntimeStateHost? EnsureRuntimeStateHost(
            BlueprintRunner? runner,
            out RuntimeSnapshotExporterRegistry? exporterRegistry,
            out RuntimeSnapshotSchemaRegistry? schemaRegistry,
            out RuntimeSnapshotReplayRegistry? replayRegistry)
        {
            exporterRegistry = null;
            schemaRegistry = null;
            replayRegistry = null;

            if (runner == null)
            {
                return null;
            }

            var host = runner.GetService<RuntimeStateHost>();
            EventHistoryStateDomain? eventHistoryDomain = null;
            if (host == null)
            {
                host = CreateRuntimeStateHost(
                    runner,
                    out eventHistoryDomain,
                    out exporterRegistry,
                    out schemaRegistry,
                    out replayRegistry);
            }
            else
            {
                exporterRegistry = runner.GetService<RuntimeSnapshotExporterRegistry>() ?? host.SnapshotExporterRegistry;
                schemaRegistry = runner.GetService<RuntimeSnapshotSchemaRegistry>() ?? host.SnapshotSchemaRegistry;
                replayRegistry = runner.GetService<RuntimeSnapshotReplayRegistry>() ?? host.SnapshotReplayRegistry;
                EnsureSnapshotRegistryServices(runner, exporterRegistry, schemaRegistry, replayRegistry);
                EnsureRuntimeStateHostServices(runner, host);
            }

            BlueprintEventHistoryRuntimeSupport.EnsureRegistered(
                runner,
                eventHistoryDomain,
                exporterRegistry,
                schemaRegistry,
                replayRegistry);
            return host;
        }

        private static RuntimeStateHost CreateRuntimeStateHost(
            BlueprintRunner runner,
            out EventHistoryStateDomain eventHistoryDomain,
            out RuntimeSnapshotExporterRegistry exporterRegistry,
            out RuntimeSnapshotSchemaRegistry schemaRegistry,
            out RuntimeSnapshotReplayRegistry replayRegistry)
        {
            var backend = runner.GetService<ObjectStateBackend>() ?? new ObjectStateBackend();
            var lifecycleCoordinator = runner.GetService<StateLifecycleCoordinator>() ?? new StateLifecycleCoordinator(backend);
            var nodePrivateDomain = runner.GetService<NodePrivateStateDomain>() ?? new NodePrivateStateDomain(backend);
            var reactiveStateDomain = runner.GetService<ReactiveStateDomain>() ?? new ReactiveStateDomain(backend);
            var schedulingStateDomain = runner.GetService<SchedulingStateDomain>() ?? new SchedulingStateDomain(backend);
            var portStateDomain = runner.GetService<PortStateDomain>() ?? new PortStateDomain(backend, reactiveStateDomain);
            eventHistoryDomain = runner.GetService<EventHistoryStateDomain>() ?? new EventHistoryStateDomain(backend);
            exporterRegistry = runner.GetService<RuntimeSnapshotExporterRegistry>() ?? new RuntimeSnapshotExporterRegistry();
            schemaRegistry = runner.GetService<RuntimeSnapshotSchemaRegistry>() ?? new RuntimeSnapshotSchemaRegistry();
            replayRegistry = runner.GetService<RuntimeSnapshotReplayRegistry>() ?? new RuntimeSnapshotReplayRegistry();

            var host = new RuntimeStateHost(
                backend,
                lifecycleCoordinator,
                new IRuntimeStateDomain[] { nodePrivateDomain, reactiveStateDomain, schedulingStateDomain, portStateDomain, eventHistoryDomain },
                snapshotExporterRegistry: exporterRegistry,
                snapshotSchemaRegistry: schemaRegistry,
                snapshotReplayRegistry: replayRegistry);

            RegisterMissingService(runner, backend);
            RegisterMissingService(runner, lifecycleCoordinator);
            RegisterMissingService(runner, nodePrivateDomain);
            RegisterMissingService(runner, reactiveStateDomain);
            RegisterMissingService<IReactiveStateDomain>(runner, reactiveStateDomain);
            RegisterMissingService(runner, schedulingStateDomain);
            RegisterMissingService<ISchedulingStateDomain>(runner, schedulingStateDomain);
            RegisterMissingService(runner, portStateDomain);
            RegisterMissingService<IPortStateDomain>(runner, portStateDomain);
            RegisterMissingService(runner, eventHistoryDomain);
            EnsureSnapshotRegistryServices(runner, exporterRegistry, schemaRegistry, replayRegistry);
            RegisterMissingService(runner, host);
            RegisterMissingService<SceneBlueprint.Contract.IRuntimeStateHost>(runner, host);
            return host;
        }

        private static void EnsureRuntimeStateHostServices(BlueprintRunner runner, RuntimeStateHost host)
        {
            RegisterMissingService(runner, host);
            RegisterMissingService<SceneBlueprint.Contract.IRuntimeStateHost>(runner, host);
        }

        private static void EnsureSnapshotRegistryServices(
            BlueprintRunner runner,
            RuntimeSnapshotExporterRegistry exporterRegistry,
            RuntimeSnapshotSchemaRegistry schemaRegistry,
            RuntimeSnapshotReplayRegistry replayRegistry)
        {
            RegisterMissingService(runner, exporterRegistry);
            RegisterMissingService(runner, schemaRegistry);
            RegisterMissingService(runner, replayRegistry);
        }

        private static void RegisterMissingService<TService>(BlueprintRunner runner, TService service)
            where TService : class
        {
            if (runner.GetService<TService>() == null)
            {
                runner.RegisterService(service);
            }
        }
    }
}
