#nullable enable
using System;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter
{
    public interface IBlueprintEventHistoryRecorder
    {
        void RecordEmitted(BlueprintEventContext eventContext);

        void RecordInjected(BlueprintEventContext eventContext);
    }

    public interface IBlueprintEventHistorySignalBus
    {
        IBlueprintEventHistoryRecorder? EventHistoryRecorder { get; set; }
    }

    public sealed class BlueprintEventHistoryRecorder : IBlueprintEventHistoryRecorder
    {
        private readonly EventHistoryStateDomain _domain;

        public BlueprintEventHistoryRecorder(EventHistoryStateDomain domain)
        {
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        }

        public void RecordEmitted(BlueprintEventContext eventContext)
        {
            if (eventContext == null)
            {
                throw new ArgumentNullException(nameof(eventContext));
            }

            _domain.RecordEmitted(eventContext);
        }

        public void RecordInjected(BlueprintEventContext eventContext)
        {
            if (eventContext == null)
            {
                throw new ArgumentNullException(nameof(eventContext));
            }

            _domain.RecordInjected(eventContext);
        }
    }

    public static class BlueprintEventHistoryRuntimeSupport
    {
        public static void RecordEvent(
            BlueprintRunner? runner,
            BlueprintEventContext? eventContext,
            EventHistoryRecordKind recordKind = EventHistoryRecordKind.Emit)
        {
            if (runner == null || eventContext == null)
            {
                return;
            }

            runner.GetService<IBlueprintEventObserver>()?.OnEventRecorded(eventContext);

            var recorder = runner.GetService<IBlueprintEventHistoryRecorder>();
            if (recorder == null)
            {
                return;
            }

            if (recordKind == EventHistoryRecordKind.Inject)
            {
                recorder.RecordInjected(eventContext);
            }
            else
            {
                recorder.RecordEmitted(eventContext);
            }
        }

        public static void EnsureRegistered(
            BlueprintRunner runner,
            EventHistoryStateDomain? domain = null,
            RuntimeSnapshotExporterRegistry? exporterRegistry = null,
            RuntimeSnapshotSchemaRegistry? schemaRegistry = null,
            RuntimeSnapshotReplayRegistry? replayRegistry = null)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            var eventHistoryDomain = runner.GetService<EventHistoryStateDomain>() ?? domain;
            if (eventHistoryDomain == null)
            {
                var host = runner.GetService<RuntimeStateHost>();
                if (host != null
                    && host.TryGetDomain(EventHistoryStateDomain.EventHistoryDomainId, out var registeredDomain)
                    && registeredDomain is EventHistoryStateDomain typedDomain)
                {
                    eventHistoryDomain = typedDomain;
                }
            }

            if (eventHistoryDomain != null && runner.GetService<EventHistoryStateDomain>() == null)
            {
                runner.RegisterService<EventHistoryStateDomain>(eventHistoryDomain);
            }

            if (eventHistoryDomain != null)
            {
                var recorder = runner.GetService<IBlueprintEventHistoryRecorder>();
                if (recorder == null)
                {
                    var createdRecorder = new BlueprintEventHistoryRecorder(eventHistoryDomain);
                    runner.RegisterService<BlueprintEventHistoryRecorder>(createdRecorder);
                    runner.RegisterService<IBlueprintEventHistoryRecorder>(createdRecorder);
                    recorder = createdRecorder;
                }
                else if (recorder is BlueprintEventHistoryRecorder concreteRecorder
                         && runner.GetService<BlueprintEventHistoryRecorder>() == null)
                {
                    runner.RegisterService<BlueprintEventHistoryRecorder>(concreteRecorder);
                }

                if (runner.SignalBus is IBlueprintEventHistorySignalBus historyAwareBus)
                {
                    historyAwareBus.EventHistoryRecorder = recorder;
                }
            }

            exporterRegistry ??= runner.GetService<RuntimeSnapshotExporterRegistry>()
                                ?? runner.GetService<RuntimeStateHost>()?.SnapshotExporterRegistry;
            replayRegistry ??= runner.GetService<RuntimeSnapshotReplayRegistry>()
                              ?? runner.GetService<RuntimeStateHost>()?.SnapshotReplayRegistry;
            schemaRegistry ??= runner.GetService<RuntimeSnapshotSchemaRegistry>()
                              ?? runner.GetService<RuntimeStateHost>()?.SnapshotSchemaRegistry;

            if (exporterRegistry != null && runner.GetService<EventHistoryStateSnapshotExporter>() == null)
            {
                var exporter = new EventHistoryStateSnapshotExporter();
                exporterRegistry.RegisterUnique(exporter);
                runner.RegisterService<EventHistoryStateSnapshotExporter>(exporter);
            }

            if (replayRegistry != null && runner.GetService<EventHistoryStateSnapshotReplayer>() == null)
            {
                var replayer = new EventHistoryStateSnapshotReplayer();
                replayRegistry.RegisterUnique(replayer);
                runner.RegisterService<EventHistoryStateSnapshotReplayer>(replayer);
            }

            if (schemaRegistry != null)
            {
                schemaRegistry.RegisterSchema(
                    EventHistoryStateSnapshotExporter.SchemaId,
                    EventHistoryStateSnapshotExporter.SchemaVersion);
            }
        }
    }
}
