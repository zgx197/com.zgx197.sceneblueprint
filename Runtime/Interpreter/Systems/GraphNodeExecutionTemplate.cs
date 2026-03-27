#nullable enable
using System;
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    internal interface IGraphNodePlanState
    {
        string PlanSource { get; set; }

        string PlanSummary { get; set; }

        string ConditionSummary { get; set; }

        string ExecutionSummary { get; set; }
    }

    internal interface IGraphTimedNodeState : IGraphNodePlanState
    {
        int StartTick { get; set; }

        int GetElapsedTicks(int currentTick);
    }

    internal readonly struct GraphNodePlanHeader
    {
        public GraphNodePlanHeader(
            string planSource,
            string planSummary,
            string conditionSummary)
        {
            PlanSource = planSource ?? string.Empty;
            PlanSummary = planSummary ?? string.Empty;
            ConditionSummary = conditionSummary ?? string.Empty;
        }

        public string PlanSource { get; }

        public string PlanSummary { get; }

        public string ConditionSummary { get; }
    }

    internal readonly struct GraphNodeEventEmissionOptions
    {
        public GraphNodeEventEmissionOptions(
            SemanticDescriptorSet? semantics)
        {
            Semantics = semantics;
        }

        public SemanticDescriptorSet? Semantics { get; }
    }

    internal readonly struct GraphNodeEventContract
    {
        public GraphNodeEventContract(
            string eventKind,
            string outputPortId,
            GraphNodeEventEmissionOptions emissionOptions)
        {
            EventKind = eventKind ?? string.Empty;
            OutputPortId = outputPortId ?? string.Empty;
            EmissionOptions = emissionOptions;
        }

        public string EventKind { get; }

        public string OutputPortId { get; }

        public GraphNodeEventEmissionOptions EmissionOptions { get; }
    }

    internal enum GraphNodeExecutionResultKind
    {
        Running = 0,
        Completed = 1,
        TimedOut = 2,
    }

    internal readonly struct GraphNodeExecutionResult
    {
        public GraphNodeExecutionResult(
            GraphNodeExecutionResultKind kind,
            string eventKind,
            string outputPortId)
        {
            Kind = kind;
            EventKind = eventKind ?? string.Empty;
            OutputPortId = outputPortId ?? string.Empty;
        }

        public GraphNodeExecutionResultKind Kind { get; }

        public string EventKind { get; }

        public string OutputPortId { get; }

        public bool ShouldComplete => Kind != GraphNodeExecutionResultKind.Running;
    }

    internal readonly struct GraphNodeCompletionContract
    {
        public GraphNodeCompletionContract(
            GraphNodeExecutionResult completionResult,
            GraphNodeEventEmissionOptions emissionOptions)
        {
            CompletionResult = completionResult;
            EmissionOptions = emissionOptions;
        }

        public GraphNodeExecutionResult CompletionResult { get; }

        public GraphNodeEventEmissionOptions EmissionOptions { get; }
    }

    internal readonly struct GraphNodeActivationProgress
    {
        public GraphNodeActivationProgress(
            int currentCount,
            int requiredCount,
            string planSummary)
        {
            CurrentCount = Math.Max(0, currentCount);
            RequiredCount = Math.Max(1, requiredCount);
            PlanSummary = planSummary ?? string.Empty;
        }

        public int CurrentCount { get; }

        public int RequiredCount { get; }

        public string PlanSummary { get; }

        public bool IsSatisfied => CurrentCount >= RequiredCount;
    }

    internal enum GraphNodeActivationStepKind
    {
        Unchanged = 0,
        MovedToWaiting = 1,
        PromotedToRunning = 2,
        AlreadySatisfied = 3,
        ActivatedFromIdle = 4,
        ReactivatedFromListening = 5,
    }

    internal readonly struct GraphNodeActivationStep
    {
        public GraphNodeActivationStep(
            GraphNodeActivationStepKind kind,
            GraphNodeActivationProgress progress)
        {
            Kind = kind;
            Progress = progress;
        }

        public GraphNodeActivationStepKind Kind { get; }

        public GraphNodeActivationProgress Progress { get; }

        public bool IsWaitingTransition => Kind == GraphNodeActivationStepKind.MovedToWaiting;

        public bool IsRunningTransition =>
            Kind == GraphNodeActivationStepKind.PromotedToRunning
            || Kind == GraphNodeActivationStepKind.ActivatedFromIdle
            || Kind == GraphNodeActivationStepKind.ReactivatedFromListening;

        public bool WasReactivated => Kind == GraphNodeActivationStepKind.ReactivatedFromListening;
    }

    internal readonly struct GraphNodeStateAccess<TState>
        where TState : class
    {
        public GraphNodeStateAccess(
            TState state,
            bool enterRequested,
            bool createdFresh)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            EnterRequested = enterRequested;
            CreatedFresh = createdFresh;
        }

        public TState State { get; }

        public bool EnterRequested { get; }

        public bool CreatedFresh { get; }
    }

    internal static class GraphNodeExecutionTemplate
    {
        public static GraphNodeExecutionResult RunningResult { get; } =
            new(GraphNodeExecutionResultKind.Running, string.Empty, string.Empty);

        public static GraphNodeExecutionResult CreateCompletionResult(
            GraphNodeExecutionResultKind kind,
            string eventKind,
            string outputPortId)
        {
            return new GraphNodeExecutionResult(kind, eventKind, outputPortId);
        }

        public static GraphNodeEventContract CreateEventContract(
            string eventKind,
            string outputPortId = "",
            GraphNodeEventEmissionOptions emissionOptions = default)
        {
            return new GraphNodeEventContract(eventKind, outputPortId, emissionOptions);
        }

        public static GraphNodeCompletionContract CreateCompletionContract(
            GraphNodeExecutionResultKind kind,
            string eventKind,
            string outputPortId,
            GraphNodeEventEmissionOptions emissionOptions = default)
        {
            return new GraphNodeCompletionContract(
                CreateCompletionResult(kind, eventKind, outputPortId),
                emissionOptions);
        }

        public static GraphNodeCompletionContract CreateCompletionContract(
            GraphNodeExecutionResult completionResult,
            GraphNodeEventEmissionOptions emissionOptions = default)
        {
            return new GraphNodeCompletionContract(completionResult, emissionOptions);
        }

        public static GraphNodeActivationProgress CreateActivationProgress(
            int currentCount,
            int requiredCount,
            string planSummary,
            GraphSemanticDescriptor? graphDescriptor)
        {
            return new GraphNodeActivationProgress(
                currentCount,
                ResolveRequiredCount(graphDescriptor, requiredCount),
                planSummary);
        }

        public static GraphNodePlanHeader CreatePlanHeader(
            string planSource,
            string planSummary,
            string conditionSummary)
        {
            return new GraphNodePlanHeader(planSource, planSummary, conditionSummary);
        }

        public static void ApplyPlanHeader(
            IGraphNodePlanState state,
            GraphNodePlanHeader planHeader)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.PlanSource = planHeader.PlanSource;
            state.PlanSummary = planHeader.PlanSummary;
            state.ConditionSummary = planHeader.ConditionSummary;
        }

        public static void BackfillPlanHeaderIfMissing(
            IGraphNodePlanState state,
            GraphNodePlanHeader planHeader)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (string.IsNullOrWhiteSpace(state.PlanSource))
            {
                state.PlanSource = planHeader.PlanSource;
            }

            if (string.IsNullOrWhiteSpace(state.PlanSummary))
            {
                state.PlanSummary = planHeader.PlanSummary;
            }

            if (string.IsNullOrWhiteSpace(state.ConditionSummary))
            {
                state.ConditionSummary = planHeader.ConditionSummary;
            }
        }

        public static void InitializeTimedState(
            IGraphTimedNodeState state,
            int currentTick,
            GraphNodePlanHeader planHeader,
            string executionSummary)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.StartTick = currentTick;
            ApplyPlanHeader(state, planHeader);
            state.ExecutionSummary = executionSummary ?? string.Empty;
        }

        public static void InitializeTimedState(
            TimedNodeState state,
            int currentTick,
            int targetTick,
            GraphNodePlanHeader planHeader,
            string executionSummary)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            InitializeTimedState(
                (IGraphTimedNodeState)state,
                currentTick,
                planHeader,
                executionSummary);
            state.TargetTick = targetTick;
        }

        public static void InitializeInstantState(
            InstantEventNodeState state,
            int currentTick,
            GraphNodePlanHeader planHeader,
            string executionSummary,
            string eventKind,
            string eventValue,
            bool isTerminal,
            string payloadSummary = "")
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            InitializeTimedState(
                (IGraphTimedNodeState)state,
                currentTick,
                planHeader,
                executionSummary);
            state.EventKind = eventKind ?? string.Empty;
            state.EventValue = eventValue ?? string.Empty;
            state.IsTerminal = isTerminal;
            state.PayloadSummary = payloadSummary ?? string.Empty;
        }

        public static SignalPayload CreatePlanPayload(IGraphNodePlanState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new SignalPayload
            {
                ["PlanSource"] = state.PlanSource,
                ["PlanSummary"] = state.PlanSummary,
                ["ConditionSummary"] = state.ConditionSummary,
                ["ExecutionSummary"] = state.ExecutionSummary,
            };
        }

        public static SignalPayload CreateTimedPayload(
            IGraphTimedNodeState state,
            int currentTick,
            string outputPortId = "")
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var payload = CreatePlanPayload(state);
            payload["StartTick"] = state.StartTick.ToString();
            payload["ElapsedTicks"] = state.GetElapsedTicks(currentTick).ToString();
            if (!string.IsNullOrWhiteSpace(outputPortId))
            {
                payload["OutputPortId"] = outputPortId;
            }

            return payload;
        }

        public static SignalPayload CreateCompletionPayload(
            IGraphTimedNodeState state,
            int currentTick,
            GraphNodeExecutionResult completionResult,
            Action<SignalPayload>? enrichPayload = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var payload = CreateTimedPayload(state, currentTick, completionResult.OutputPortId);
            payload["CompletionKind"] = completionResult.Kind.ToString();
            enrichPayload?.Invoke(payload);
            return payload;
        }

        public static void RecordSemanticEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            string eventKind,
            SignalPayload payload)
        {
            RecordSemanticEvent(
                frame,
                actionIndex,
                currentTick,
                eventKind,
                payload,
                default);
        }

        public static void RecordSemanticEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            string eventKind,
            SignalPayload payload,
            GraphNodeEventEmissionOptions emissionOptions)
        {
            if (frame?.Runner == null)
            {
                return;
            }

            ActionEventHistoryRuntimeSupport.RecordActionEvent(
                frame,
                actionIndex,
                currentTick,
                eventKind,
                payload,
                new ActionEventRecordOptions(emissionOptions.Semantics));
        }

        public static void RecordPlanEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            IGraphNodePlanState state,
            GraphNodeEventContract eventContract,
            Action<SignalPayload>? enrichPayload = null)
        {
            var payload = CreatePlanPayload(state);
            if (!string.IsNullOrWhiteSpace(eventContract.OutputPortId))
            {
                payload["OutputPortId"] = eventContract.OutputPortId;
            }

            enrichPayload?.Invoke(payload);
            RecordSemanticEvent(
                frame,
                actionIndex,
                currentTick,
                eventContract.EventKind,
                payload,
                eventContract.EmissionOptions);
        }

        public static void RecordTimedEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            IGraphTimedNodeState state,
            GraphNodeEventContract eventContract,
            Action<SignalPayload>? enrichPayload = null)
        {
            var payload = CreateTimedPayload(state, currentTick, eventContract.OutputPortId);
            enrichPayload?.Invoke(payload);
            RecordSemanticEvent(
                frame,
                actionIndex,
                currentTick,
                eventContract.EventKind,
                payload,
                eventContract.EmissionOptions);
        }

        public static void RecordCompletionEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            IGraphTimedNodeState state,
            GraphNodeExecutionResult completionResult,
            Action<SignalPayload>? enrichPayload = null)
        {
            RecordCompletionEvent(
                frame,
                actionIndex,
                currentTick,
                state,
                completionResult,
                enrichPayload,
                default);
        }

        public static void RecordCompletionEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            IGraphTimedNodeState state,
            GraphNodeExecutionResult completionResult,
            Action<SignalPayload>? enrichPayload,
            GraphNodeEventEmissionOptions emissionOptions)
        {
            if (!completionResult.ShouldComplete || string.IsNullOrWhiteSpace(completionResult.EventKind))
            {
                return;
            }

            RecordSemanticEvent(
                frame,
                actionIndex,
                currentTick,
                completionResult.EventKind,
                CreateCompletionPayload(state, currentTick, completionResult, enrichPayload),
                emissionOptions);
        }

        public static void RecordCompletionEvent(
            BlueprintFrame? frame,
            int actionIndex,
            int currentTick,
            IGraphTimedNodeState state,
            GraphNodeCompletionContract completionContract,
            Action<SignalPayload>? enrichPayload = null)
        {
            RecordCompletionEvent(
                frame,
                actionIndex,
                currentTick,
                state,
                completionContract.CompletionResult,
                enrichPayload,
                completionContract.EmissionOptions);
        }

        public static int ResolveRequiredCount(
            GraphSemanticDescriptor? graphDescriptor,
            int fallbackRequiredCount)
        {
            return graphDescriptor?.RequiredCount > 0
                ? graphDescriptor.RequiredCount
                : Math.Max(1, fallbackRequiredCount);
        }

        public static bool TryPromoteToRunning(ref ActionRuntimeState runtimeState)
        {
            if (runtimeState.Phase != ActionPhase.Idle
                && runtimeState.Phase != ActionPhase.WaitingTrigger)
            {
                return false;
            }

            runtimeState.Phase = ActionPhase.Running;
            runtimeState.TicksInPhase = 0;
            runtimeState.IsFirstEntry = true;
            return true;
        }

        public static bool ConsumeFirstEntry(ref ActionRuntimeState runtimeState)
        {
            var enterRequested = runtimeState.IsFirstEntry;
            if (enterRequested)
            {
                runtimeState.IsFirstEntry = false;
            }

            return enterRequested;
        }

        public static GraphNodeStateAccess<TState> AcquireState<TState>(
            BlueprintFrame frame,
            int actionIndex,
            NodeStateDescriptor<TState> descriptor,
            bool enterRequested,
            ref ActionRuntimeState runtimeState,
            bool resetCustomCountersOnFresh = true)
            where TState : class
        {
            var state = NodePrivateExecutionStateSupport.GetOrCreateExecutionState(
                frame,
                actionIndex,
                descriptor,
                enterRequested,
                out var createdFresh);

            if (createdFresh && resetCustomCountersOnFresh)
            {
                ResetCustomCounters(ref runtimeState);
            }

            return new GraphNodeStateAccess<TState>(state, enterRequested, createdFresh);
        }

        public static GraphNodeStateAccess<TState> AcquireRunningState<TState>(
            BlueprintFrame frame,
            int actionIndex,
            NodeStateDescriptor<TState> descriptor,
            ref ActionRuntimeState runtimeState,
            bool resetCustomCountersOnFresh = true)
            where TState : class
        {
            return AcquireState(
                frame,
                actionIndex,
                descriptor,
                ConsumeFirstEntry(ref runtimeState),
                ref runtimeState,
                resetCustomCountersOnFresh);
        }

        public static GraphNodeActivationStep ApplyBarrierActivation(
            ref ActionRuntimeState runtimeState,
            GraphNodeActivationProgress progress)
        {
            if (progress.IsSatisfied)
            {
                return new GraphNodeActivationStep(
                    TryPromoteToRunning(ref runtimeState)
                        ? GraphNodeActivationStepKind.PromotedToRunning
                        : GraphNodeActivationStepKind.AlreadySatisfied,
                    progress);
            }

            var movedToWaiting = runtimeState.Phase == ActionPhase.Idle;
            MoveToWaitingIfIdle(ref runtimeState);
            return new GraphNodeActivationStep(
                movedToWaiting
                    ? GraphNodeActivationStepKind.MovedToWaiting
                    : GraphNodeActivationStepKind.Unchanged,
                progress);
        }

        public static GraphNodeActivationStep TryActivateFromTransition(
            ref ActionRuntimeState runtimeState)
        {
            switch (runtimeState.Phase)
            {
                case ActionPhase.Idle:
                    runtimeState.Phase = ActionPhase.Running;
                    runtimeState.TicksInPhase = 0;
                    runtimeState.IsFirstEntry = true;
                    runtimeState.EventEmitted = false;
                    return new GraphNodeActivationStep(
                        GraphNodeActivationStepKind.ActivatedFromIdle,
                        default);

                case ActionPhase.Listening:
                    runtimeState.SoftReset();
                    return new GraphNodeActivationStep(
                        GraphNodeActivationStepKind.ReactivatedFromListening,
                        default);

                default:
                    return new GraphNodeActivationStep(
                        GraphNodeActivationStepKind.Unchanged,
                        default);
            }
        }

        public static void ResetCustomCounters(ref ActionRuntimeState runtimeState)
        {
            runtimeState.CustomInt0 = 0;
            runtimeState.CustomInt1 = 0;
        }

        public static bool EmitFlowPort(
            ref FrameView view,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            string outputPortId,
            bool resetEventEmitted = false)
        {
            if (string.IsNullOrWhiteSpace(outputPortId))
            {
                return false;
            }

            view.Router.EmitFlowEvent(ref view, actionIndex, outputPortId);
            if (resetEventEmitted)
            {
                runtimeState.EventEmitted = false;
            }

            return true;
        }

        public static void MoveToWaitingIfIdle(ref ActionRuntimeState runtimeState)
        {
            if (runtimeState.Phase == ActionPhase.Idle)
            {
                runtimeState.Phase = ActionPhase.WaitingTrigger;
            }
        }

        public static void MoveToListening(ref ActionRuntimeState runtimeState)
        {
            runtimeState.Phase = ActionPhase.Listening;
            runtimeState.TicksInPhase = 0;
        }

        public static string BuildPlanSummarySuffix(string planSummary)
        {
            return string.IsNullOrWhiteSpace(planSummary)
                ? string.Empty
                : $" | {planSummary}";
        }

        public static bool EmitFlowAndMoveToListening(
            ref FrameView view,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            string outputPortId,
            bool resetEventEmitted = false)
        {
            if (!EmitFlowPort(
                    ref view,
                    actionIndex,
                    ref runtimeState,
                    outputPortId,
                    resetEventEmitted))
            {
                return false;
            }

            MoveToListening(ref runtimeState);
            return true;
        }

        public static bool TryFinalizeCompletion<TState>(
            BlueprintFrame frame,
            ref FrameView view,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            NodeStateDescriptor<TState> descriptor,
            GraphNodeExecutionResult completionResult)
            where TState : class
        {
            if (!completionResult.ShouldComplete)
            {
                return false;
            }

            EmitFlowPort(ref view, actionIndex, ref runtimeState, completionResult.OutputPortId);

            NodePrivateExecutionStateSupport.CompleteExecutionState(frame, actionIndex, descriptor);
            runtimeState.Phase = ActionPhase.Completed;
            return true;
        }

        public static bool TryFinalizeCompletion<TState>(
            BlueprintFrame frame,
            ref FrameView view,
            int actionIndex,
            ref ActionRuntimeState runtimeState,
            NodeStateDescriptor<TState> descriptor,
            GraphNodeCompletionContract completionContract)
            where TState : class
        {
            return TryFinalizeCompletion(
                frame,
                ref view,
                actionIndex,
                ref runtimeState,
                descriptor,
                completionContract.CompletionResult);
        }
    }
}
