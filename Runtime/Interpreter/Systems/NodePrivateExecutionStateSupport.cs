#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    public sealed class TimedNodeState : IGraphTimedNodeState
    {
        public int StartTick { get; set; }

        public int TargetTick { get; set; }

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class InstantEventNodeState : IGraphTimedNodeState
    {
        public int StartTick { get; set; }

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public string EventKind { get; set; } = string.Empty;

        public string EventValue { get; set; } = string.Empty;

        public bool IsTerminal { get; set; }

        public string SubjectRefSerialized { get; set; } = string.Empty;

        public string SubjectSummary { get; set; } = string.Empty;

        public string InstigatorRefSerialized { get; set; } = string.Empty;

        public string InstigatorSummary { get; set; } = string.Empty;

        public string TargetRefSerialized { get; set; } = string.Empty;

        public string TargetSummary { get; set; } = string.Empty;

        public string PayloadSummary { get; set; } = string.Empty;

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class BlackboardAccessNodeState
    {
        public int StartTick { get; set; }

        public string AccessKind { get; set; } = string.Empty;

        public string Scope { get; set; } = string.Empty;

        public int VariableIndex { get; set; } = -1;

        public string VariableName { get; set; } = string.Empty;

        public string VariableType { get; set; } = string.Empty;

        public string VariableSummary { get; set; } = string.Empty;

        public string AccessSummary { get; set; } = string.Empty;

        public string ValueText { get; set; } = string.Empty;

        public bool HasValue { get; set; }

        public bool Succeeded { get; set; }

        public string FailureReason { get; set; } = string.Empty;

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class FlowFilterNodeState : IGraphTimedNodeState
    {
        public int StartTick { get; set; }

        public int LastEvaluationTick { get; set; }

        public int EvaluationCount { get; set; }

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public string CompareValueText { get; set; } = string.Empty;

        public string Operator { get; set; } = string.Empty;

        public string ConstValueText { get; set; } = string.Empty;

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public bool ConditionMet { get; set; }

        public bool WasUnconditionalPass { get; set; }

        public string RoutedPort { get; set; } = string.Empty;

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class WaitSignalNodeState : IGraphTimedNodeState
    {
        public int StartTick { get; set; }

        public int TimeoutTargetTick { get; set; } = BlueprintTime.NoDeadline;

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public string SignalTag { get; set; } = string.Empty;

        public string SubjectRefFilterSerialized { get; set; } = string.Empty;

        public string SubjectRefFilterSummary { get; set; } = string.Empty;

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public bool IsWildcardPattern { get; set; }

        public bool HasLoggedReleaseWildcardWarning { get; set; }

        public RuntimeEntryRef ReactiveWaitEntryRef { get; set; }

        public RuntimeEntryRef ReactiveSubscriptionEntryRef { get; set; }

        public RuntimeEntryRef SchedulingEntryRef { get; set; }

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class WatchConditionNodeState : IGraphTimedNodeState
    {
        public int StartTick { get; set; }

        public int TimeoutTargetTick { get; set; } = BlueprintTime.NoDeadline;

        public float TimeoutSeconds { get; set; }

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public string ConditionType { get; set; } = string.Empty;

        public string TargetRefSerialized { get; set; } = string.Empty;

        public string TargetSummary { get; set; } = string.Empty;

        public string ParametersRaw { get; set; } = string.Empty;

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public bool Repeat { get; set; }

        public ConditionWatchDescriptor Descriptor { get; set; } = new();

        public ConditionWatchHandle WatchHandle { get; set; }

        public RuntimeEntryRef ReactiveWaitEntryRef { get; set; }

        public RuntimeEntryRef ReactiveSubscriptionEntryRef { get; set; }

        public RuntimeEntryRef SchedulingEntryRef { get; set; }

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class CompositeConditionNodeState : IGraphTimedNodeState
    {
        public int StartTick { get; set; }

        public int TimeoutTargetTick { get; set; } = BlueprintTime.NoDeadline;

        public string Mode { get; set; } = "AND";

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public int ConnectedMask { get; set; }

        public int TriggeredMask { get; set; }

        public string ConnectedPortSummary { get; set; } = string.Empty;

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public RuntimeEntryRef ReactiveWaitEntryRef { get; set; }

        public RuntimeEntryRef ReactiveCond0SubscriptionEntryRef { get; set; }

        public RuntimeEntryRef ReactiveCond1SubscriptionEntryRef { get; set; }

        public RuntimeEntryRef ReactiveCond2SubscriptionEntryRef { get; set; }

        public RuntimeEntryRef ReactiveCond3SubscriptionEntryRef { get; set; }

        public RuntimeEntryRef SchedulingEntryRef { get; set; }

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class JoinNodeState : IGraphTimedNodeState
    {
        public int StartTick { get; set; }

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public int RequiredCount { get; set; }

        public int ReceivedCount { get; set; }

        public string IncomingActionSummary { get; set; } = string.Empty;

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class SpawnPresetNodeState
    {
        public int StartTick { get; set; }

        public string PlanSource { get; set; } = string.Empty;

        public string PlanSummary { get; set; } = string.Empty;

        public string ConditionSummary { get; set; } = string.Empty;

        public string ExecutionSummary { get; set; } = string.Empty;

        public Task<SpawnPresetExecutionResult>? InFlightTask { get; set; }

        public int RequestedSpawnCount { get; set; }

        public int PublicSubjectCount { get; set; }

        public string SubjectIdentitySummary { get; set; } = string.Empty;

        public SubjectSemanticDescriptor[] Subjects { get; set; } = Array.Empty<SubjectSemanticDescriptor>();

        public int LastSpawnCount { get; set; }

        public string LastErrorMessage { get; set; } = string.Empty;

        public int GetElapsedTicks(int currentTick)
        {
            return Math.Max(0, currentTick - StartTick);
        }
    }

    public sealed class SpawnPresetExecutionResult
    {
        public static readonly SpawnPresetExecutionResult Empty =
            new(0, Array.Empty<SpawnPresetEntityRegistration>());

        public SpawnPresetExecutionResult(
            int spawnCount,
            IReadOnlyList<SpawnPresetEntityRegistration> registrations,
            string? errorMessage = null)
        {
            SpawnCount = spawnCount;
            Registrations = registrations ?? Array.Empty<SpawnPresetEntityRegistration>();
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public int SpawnCount { get; }

        public IReadOnlyList<SpawnPresetEntityRegistration> Registrations { get; }

        public string ErrorMessage { get; }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    }

    public sealed class SpawnPresetEntityRegistration
    {
        public SpawnPresetEntityRegistration(
            string role,
            string alias,
            string entityId,
            string tag = "",
            string spawnMode = "",
            string compiledSubjectId = "",
            string publicSubjectId = "")
        {
            Role = role ?? string.Empty;
            Alias = alias ?? string.Empty;
            EntityId = entityId ?? string.Empty;
            Tag = tag ?? string.Empty;
            SpawnMode = spawnMode ?? string.Empty;
            CompiledSubjectId = compiledSubjectId ?? string.Empty;
            PublicSubjectId = publicSubjectId ?? string.Empty;
        }

        public string Role { get; }

        public string Alias { get; }

        public string EntityId { get; }

        public string Tag { get; }

        public string SpawnMode { get; }

        public string CompiledSubjectId { get; }

        public string PublicSubjectId { get; }
    }

    public static class NodePrivateExecutionStateSupport
    {
        public static bool EnsureLifecycleBinding<TState>(
            BlueprintFrame frame,
            NodeStateDescriptor<TState> descriptor,
            ref bool bindingRegistered)
            where TState : class
        {
            if (bindingRegistered)
            {
                return true;
            }

            var host = TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return false;
            }

            host.RegisterLifecycleBinding(descriptor.Descriptor, descriptor.DefaultSlotKey);
            bindingRegistered = true;
            return true;
        }

        public static TState GetOrCreateExecutionState<TState>(
            BlueprintFrame frame,
            int actionIndex,
            NodeStateDescriptor<TState> descriptor,
            bool enterRequested,
            out bool createdFresh)
            where TState : class
        {
            var host = RequireRuntimeStateHost(frame);
            var domain = host.GetRequiredDomain<NodePrivateStateDomain>();
            var ownerRef = CreateActionOwnerRef(frame, actionIndex);
            var entryRefBeforeEnter = TryGetEntryRef(domain, ownerRef, descriptor);
            var shouldEnter = enterRequested || !entryRefBeforeEnter.HasValue;

            if (shouldEnter)
            {
                host.HandleLifecycle(RuntimeLifecycleEvent.Enter(
                    ownerRef,
                    descriptorId: descriptor.Descriptor.Id,
                    entryRef: entryRefBeforeEnter));
            }

            var state = domain.GetOrCreateState(ownerRef, descriptor);
            var entryRefAfterEnter = TryGetEntryRef(domain, ownerRef, descriptor);
            createdFresh = !entryRefBeforeEnter.HasValue || entryRefBeforeEnter != entryRefAfterEnter;
            return state;
        }

        public static void CompleteExecutionState<TState>(
            BlueprintFrame frame,
            int actionIndex,
            NodeStateDescriptor<TState> descriptor)
            where TState : class
        {
            var host = TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return;
            }

            var domain = host.GetRequiredDomain<NodePrivateStateDomain>();
            var ownerRef = CreateActionOwnerRef(frame, actionIndex);
            var entryRef = TryGetEntryRef(domain, ownerRef, descriptor);
            host.HandleLifecycle(RuntimeLifecycleEvent.Complete(
                ownerRef,
                descriptorId: descriptor.Descriptor.Id,
                entryRef: entryRef));
        }

        public static void DisposeExecutionState<TState>(
            BlueprintFrame frame,
            int actionIndex,
            NodeStateDescriptor<TState> descriptor)
            where TState : class
        {
            var host = TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return;
            }

            var domain = host.GetRequiredDomain<NodePrivateStateDomain>();
            var ownerRef = CreateActionOwnerRef(frame, actionIndex);
            var entryRef = TryGetEntryRef(domain, ownerRef, descriptor);
            if (!entryRef.HasValue)
            {
                return;
            }

            host.HandleLifecycle(RuntimeLifecycleEvent.Dispose(
                ownerRef,
                descriptorId: descriptor.Descriptor.Id,
                entryRef: entryRef));
        }

        public static RuntimeEntryRef? TryGetEntryRef<TState>(
            NodePrivateStateDomain domain,
            StateOwnerRef ownerRef,
            NodeStateDescriptor<TState> descriptor)
            where TState : class
        {
            return domain.TryLocateEntry(ownerRef, descriptor.DefaultSlotKey, out var entryRef)
                ? entryRef
                : (RuntimeEntryRef?)null;
        }

        public static RuntimeEntryRef? TryGetEntryRef<TState>(
            BlueprintFrame frame,
            int actionIndex,
            NodeStateDescriptor<TState> descriptor)
            where TState : class
        {
            var host = TryGetRuntimeStateHost(frame);
            if (host == null)
            {
                return null;
            }

            var domain = host.GetRequiredDomain<NodePrivateStateDomain>();
            return TryGetEntryRef(domain, CreateActionOwnerRef(frame, actionIndex), descriptor);
        }

        public static StateOwnerRef CreateActionOwnerRef(BlueprintFrame frame, int actionIndex)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            if (actionIndex < 0 || actionIndex >= frame.Actions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(actionIndex), actionIndex, "Action index is out of range.");
            }

            return new StateOwnerRef(OwnerKind.Action, $"action:{frame.Actions[actionIndex].Id}");
        }

        public static RuntimeStateHost? TryGetRuntimeStateHost(BlueprintFrame frame)
        {
            return frame?.Runner?.GetService<RuntimeStateHost>();
        }

        public static RuntimeStateHost RequireRuntimeStateHost(BlueprintFrame frame)
        {
            return TryGetRuntimeStateHost(frame)
                ?? throw new InvalidOperationException("RuntimeStateHost is required but was not found on the current BlueprintRunner.");
        }
    }
}
