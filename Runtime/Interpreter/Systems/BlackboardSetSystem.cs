#nullable enable
using System;
using UnityEngine;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;
using SceneBlueprint.Runtime.State;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 黑板写入系统——处理 Blackboard.Set 节点。
    /// <para>
    /// 运行时逻辑：
    /// 1. 读取 variableName 属性，在 frame.Variables 中查找声明
    /// 2. 根据声明的 Scope 路由：Local → frame.Blackboard，Global → GlobalBlackboard
    /// 3. 根据声明的 Type 解析 value 属性字符串，写入对应 index
    /// 4. 将写入快照写入 NodePrivateStateDomain，随后立即完成（ActionPhase.Completed）
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    [UpdateAfter(typeof(FlowSystem))]
    public class BlackboardSetSystem : BlueprintSystemBase, IFrameAware
    {
        internal static readonly NodeStateDescriptor<BlackboardAccessNodeState> BlackboardSetStateDescriptor =
            new(
                "blackboard.set.state",
                StateLifetime.Execution,
                static () => new BlackboardAccessNodeState(),
                debugName: "Blackboard.Set State");

        private bool _lifecycleBindingRegistered;

        public override string Name  => "BlackboardSetSystem";

        /// <summary>BlueprintFrame 引用——用于 Blackboard/Variable 访问（FrameView 尚未包含 Blackboard 机制）</summary>
        public BlueprintFrame? Frame { get; set; }

        public override void OnInit(BlueprintFrame frame)
        {
            _lifecycleBindingRegistered = false;
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, BlackboardSetStateDescriptor, ref _lifecycleBindingRegistered);
        }

        public override void OnDisabled(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Blackboard.Set);
            for (var index = 0; index < indices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], BlackboardSetStateDescriptor);
            }
        }

        public override void Update(ref FrameView view)
        {
            var indices = view.Query.GetActionIndices(AT.Blackboard.Set);
            if (indices == null || Frame == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, BlackboardSetStateDescriptor, ref _lifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running) continue;
                ProcessSet(ref view, Frame, idx, ref state);
            }
        }

        private static void ProcessSet(ref FrameView view, BlueprintFrame frame, int actionIndex, ref ActionRuntimeState state)
        {
            bool enterRequested = state.IsFirstEntry;
            if (enterRequested)
            {
                state.IsFirstEntry = false;
            }

            var accessState = NodePrivateExecutionStateSupport.GetOrCreateExecutionState(
                frame,
                actionIndex,
                BlackboardSetStateDescriptor,
                enterRequested,
                out var createdFresh);

            var authoring = BlackboardAuthoringUtility.ReadSet(frame.Actions[actionIndex]);
            var compiledSet = TryGetCompiledSet(frame, actionIndex);
            int varIdx = compiledSet?.Variable?.VariableIndex >= 0
                ? compiledSet.Variable.VariableIndex
                : authoring.VariableIndex;
            string valueStr = !string.IsNullOrWhiteSpace(compiledSet?.NormalizedValueText)
                ? compiledSet.NormalizedValueText
                : authoring.ValueText;
            if (createdFresh)
            {
                state.CustomInt0 = 0;
                state.CustomInt1 = 0;
                accessState.StartTick = view.CurrentTick;
                accessState.AccessKind = "set";
            }

            accessState.VariableIndex = varIdx;
            accessState.Scope = string.Empty;
            accessState.VariableName = string.Empty;
            accessState.VariableType = string.Empty;
            accessState.VariableSummary = string.Empty;
            accessState.AccessSummary = string.Empty;
            accessState.ValueText = string.Empty;
            accessState.HasValue = false;
            accessState.Succeeded = false;
            accessState.FailureReason = string.Empty;

            if (varIdx < 0)
            {
                accessState.FailureReason = "variableIndex.not-configured";
                Debug.LogWarning($"[BlackboardSetSystem] Blackboard.Set (index={actionIndex}) variableIndex 未配置，跳过");
                EmitOutEvent(ref view, actionIndex);
                NodePrivateExecutionStateSupport.CompleteExecutionState(frame, actionIndex, BlackboardSetStateDescriptor);
                state.Phase = ActionPhase.Completed;
                return;
            }

            var variable = frame.FindVariable(varIdx);
            if (variable == null)
            {
                accessState.FailureReason = "variable.not-found";
                Debug.LogWarning($"[BlackboardSetSystem] Blackboard.Set (index={actionIndex}) 未找到 Index={varIdx} 的声明变量");
                EmitOutEvent(ref view, actionIndex);
                NodePrivateExecutionStateSupport.CompleteExecutionState(frame, actionIndex, BlackboardSetStateDescriptor);
                state.Phase = ActionPhase.Completed;
                return;
            }

            var parsed = ParseValue(variable.Type, valueStr);

            accessState.Scope = variable.Scope ?? string.Empty;
            accessState.VariableName = variable.Name ?? string.Empty;
            accessState.VariableType = variable.Type ?? string.Empty;
            accessState.VariableSummary = !string.IsNullOrWhiteSpace(compiledSet?.Variable?.VariableSummary)
                ? compiledSet.Variable.VariableSummary
                : BlackboardAuthoringUtility.BuildVariableSummary(variable, varIdx);
            accessState.ValueText = BlackboardAuthoringUtility.FormatValueText(parsed);
            accessState.AccessSummary = !string.IsNullOrWhiteSpace(compiledSet?.AccessSummary)
                ? compiledSet.AccessSummary
                : SemanticSummaryUtility.BuildBlackboardAccessSummary(
                    accessState.AccessKind,
                    accessState.VariableSummary,
                    accessState.ValueText);
            accessState.HasValue = parsed != null;
            accessState.Succeeded = true;

            if (variable.Scope == "Global")
                GlobalBlackboard.Set(variable.Index, parsed);
            else
                frame.Blackboard.Set(variable.Index, parsed);

            Debug.Log($"[BlackboardSetSystem] {variable.Scope}.{variable.Name}[{variable.Index}] ← {parsed}");

            EmitOutEvent(ref view, actionIndex);
            NodePrivateExecutionStateSupport.CompleteExecutionState(frame, actionIndex, BlackboardSetStateDescriptor);
            state.Phase = ActionPhase.Completed;
        }

        private static object ParseValue(string type, string value)
        {
            return BlackboardAuthoringUtility.ParseValue(type, value);
        }

        private static BlackboardSetCompiledData? TryGetCompiledSet(BlueprintFrame frame, int actionIndex)
        {
            return CompiledActionResolver.TryGetBlackboardSet(frame, actionIndex);
        }
    }
}
