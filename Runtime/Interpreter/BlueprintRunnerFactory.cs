#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Interpreter.Adapters;
using SceneBlueprint.Runtime.Interpreter.Systems;
using SceneBlueprint.Runtime.State;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// BlueprintRunner 工厂——提供标准化的 Runner 创建方式。
    /// <para>
    /// framework 默认 Runner 与项目基线 bootstrap 在这里显式分层：
    /// 框架层只负责内置 System / state / signal bridge，
    /// 业务层 bootstrap 通过显式的 framework/project-baseline 注册入口叠加。
    /// </para>
    /// </summary>
    public static class BlueprintRunnerFactory
    {
        internal static void RegisterFrameworkDefaultConfigurator(IBlueprintRunnerConfigurator configurator)
        {
            BlueprintRunnerConfiguratorRegistry.RegisterFrameworkDefault(configurator);
        }

        internal static void RegisterProjectBaselineConfigurator(IBlueprintRunnerConfigurator configurator)
        {
            BlueprintRunnerConfiguratorRegistry.RegisterProjectBaseline(configurator);
        }

        internal static void ApplyProjectBaseline(BlueprintRunner runner)
        {
            BlueprintRunnerConfiguratorRegistry.ConfigureProjectBaseline(runner);
        }

        internal static IReadOnlyList<string> GetProjectBaselineConfiguratorTypeNames()
        {
            return BlueprintRunnerConfiguratorRegistry.GetRegisteredProjectBaselineConfiguratorTypeNames();
        }

        internal static void ClearRegisteredConfigurators()
        {
            BlueprintRunnerConfiguratorRegistry.Clear();
        }

        /// <summary>
        /// 创建 framework 默认 Runner。
        /// </summary>
        public static BlueprintRunner CreateFrameworkDefault()
        {
            var runner = new BlueprintRunner();

            // 框架内置 System（始终注册）
            // TransitionSystem 必须在所有业务 System 之前执行（SystemGroup.Transition）
            runner.RegisterSystem(new TransitionSystem());
            runner.RegisterSystem(new FlowSystem());
            runner.RegisterSystem(new FlowFilterSystem());
            runner.RegisterSystem(new SignalSystem());
            runner.RegisterSystem(new CompositeConditionSystem());
            runner.RegisterSystem(new BlackboardGetSystem());
            runner.RegisterSystem(new BlackboardSetSystem());

            // 框架内置 Signal Bus：InMemorySignalBus（纯 C# 堆内存队列）
            // 业务层可通过 runner.SetSignalBus(customBus) 覆盖
            var signalBus = new InMemorySignalBus();
            runner.SetSignalBus(signalBus);

            // 信号桥接器：包装 SignalBus，提供外部系统双向通信入口
            var bridge = new InMemorySignalBridge(signalBus);
            runner.SetSignalBridge(bridge);
            runner.RegisterService<IEntityObjectResolver>(new DefaultEntityObjectResolver());

            var stateBackend = new ObjectStateBackend();
            var lifecycleCoordinator = new StateLifecycleCoordinator(stateBackend);
            var nodePrivateDomain = new NodePrivateStateDomain(stateBackend);
            var reactiveStateDomain = new ReactiveStateDomain(stateBackend);
            var schedulingStateDomain = new SchedulingStateDomain(stateBackend);
            var portStateDomain = new PortStateDomain(stateBackend, reactiveStateDomain);
            var eventHistoryDomain = new EventHistoryStateDomain(stateBackend);
            var snapshotExporterRegistry = new RuntimeSnapshotExporterRegistry();
            var snapshotSchemaRegistry = new RuntimeSnapshotSchemaRegistry();
            var snapshotReplayRegistry = new RuntimeSnapshotReplayRegistry();
            var runtimeStateHost = new RuntimeStateHost(
                stateBackend,
                lifecycleCoordinator,
                new IRuntimeStateDomain[] { nodePrivateDomain, reactiveStateDomain, schedulingStateDomain, portStateDomain, eventHistoryDomain },
                snapshotExporterRegistry: snapshotExporterRegistry,
                snapshotSchemaRegistry: snapshotSchemaRegistry,
                snapshotReplayRegistry: snapshotReplayRegistry);

            runner.RegisterService<ObjectStateBackend>(stateBackend);
            runner.RegisterService<StateLifecycleCoordinator>(lifecycleCoordinator);
            runner.RegisterService<NodePrivateStateDomain>(nodePrivateDomain);
            runner.RegisterService<ReactiveStateDomain>(reactiveStateDomain);
            runner.RegisterService<IReactiveStateDomain>(reactiveStateDomain);
            runner.RegisterService<SchedulingStateDomain>(schedulingStateDomain);
            runner.RegisterService<ISchedulingStateDomain>(schedulingStateDomain);
            runner.RegisterService<PortStateDomain>(portStateDomain);
            runner.RegisterService<IPortStateDomain>(portStateDomain);
            runner.RegisterService<EventHistoryStateDomain>(eventHistoryDomain);
            runner.RegisterService<RuntimeSnapshotExporterRegistry>(snapshotExporterRegistry);
            runner.RegisterService<RuntimeSnapshotSchemaRegistry>(snapshotSchemaRegistry);
            runner.RegisterService<RuntimeSnapshotReplayRegistry>(snapshotReplayRegistry);
            runner.RegisterService<RuntimeStateHost>(runtimeStateHost);
            runner.RegisterService<SceneBlueprint.Contract.IRuntimeStateHost>(runtimeStateHost);
            BlueprintEventHistoryRuntimeSupport.EnsureRegistered(
                runner,
                eventHistoryDomain,
                snapshotExporterRegistry,
                snapshotSchemaRegistry,
                snapshotReplayRegistry);
            Debug.Log("[BlueprintRunnerFactory] 已注册默认 InMemorySignalBus + InMemorySignalBridge");

            // 用户层 System（通过 IBlueprintSystemProvider 注册）
            BlueprintSystemRegistry.RegisterSystemsTo(runner);
            BlueprintRunnerConfiguratorRegistry.ConfigureFrameworkDefault(runner);

            return runner;
        }

        /// <summary>
        /// 创建 framework 默认 Runner，再显式叠加项目基线 bootstrap。
        /// </summary>
        public static BlueprintRunner CreateProjectBaselineDefault()
        {
            var runner = CreateFrameworkDefault();
            ApplyProjectBaseline(runner);
            return runner;
        }

        /// <summary>
        /// 兼容旧调用口径。
        /// 新代码应优先显式使用 <see cref="CreateFrameworkDefault"/> 或
        /// <see cref="CreateProjectBaselineDefault"/>。
        /// </summary>
        public static BlueprintRunner CreateDefault()
        {
            return CreateProjectBaselineDefault();
        }
    }
}
