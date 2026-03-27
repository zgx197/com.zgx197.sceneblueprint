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
    /// 黑板读取系统——处理 Blackboard.Get 节点。
    /// <para>
    /// 运行时逻辑：
    /// 1. 读取 variableName 属性，在 frame.Variables 中查找声明
    /// 2. 根据声明的 Scope 从 Local 或 Global 黑板读取值
    /// 3. 将读取快照写入 NodePrivateStateDomain，供调试 / snapshot / 回归测试消费
    /// 4. 立即完成（ActionPhase.Completed），控制流经 out 端口继续
    /// </para>
    /// <para>
    /// Phase 3 升级方向：改为通过强类型数据端口输出，进一步替代字符串值快照。
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    [UpdateAfter(typeof(BlackboardSetSystem))]
    public class BlackboardGetSystem : BlueprintSystemBase, IFrameAware
    {
        internal static readonly NodeStateDescriptor<BlackboardAccessNodeState> BlackboardGetStateDescriptor =
            new(
                "blackboard.get.state",
                StateLifetime.Execution,
                static () => new BlackboardAccessNodeState(),
                debugName: "Blackboard.Get State");

        private bool _lifecycleBindingRegistered;

        public override string Name  => "BlackboardGetSystem";

        /// <summary>BlueprintFrame 引用——用于 Blackboard/Variable 访问（FrameView 尚未包含 Blackboard 机制）</summary>
        public BlueprintFrame? Frame { get; set; }

        public override void OnInit(BlueprintFrame frame)
        {
            _lifecycleBindingRegistered = false;
            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(frame, BlackboardGetStateDescriptor, ref _lifecycleBindingRegistered);
        }

        public override void OnDisabled(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Blackboard.Get);
            for (var index = 0; index < indices.Count; index++)
            {
                NodePrivateExecutionStateSupport.DisposeExecutionState(frame, indices[index], BlackboardGetStateDescriptor);
            }
        }

        public override void Update(ref FrameView view)
        {
            var indices = view.Query.GetActionIndices(AT.Blackboard.Get);
            if (indices == null || Frame == null) return;

            NodePrivateExecutionStateSupport.EnsureLifecycleBinding(Frame, BlackboardGetStateDescriptor, ref _lifecycleBindingRegistered);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref view.States[idx];
                if (state.Phase != ActionPhase.Running) continue;
                ProcessGet(ref view, Frame, idx, ref state);
            }
        }

        private static void ProcessGet(ref FrameView view, BlueprintFrame frame, int actionIndex, ref ActionRuntimeState state)
        {
            bool enterRequested = state.IsFirstEntry;
            if (enterRequested)
            {
                state.IsFirstEntry = false;
            }

            var accessState = NodePrivateExecutionStateSupport.GetOrCreateExecutionState(
                frame,
                actionIndex,
                BlackboardGetStateDescriptor,
                enterRequested,
                out var createdFresh);

            var authoring = BlackboardAuthoringUtility.ReadGet(frame.Actions[actionIndex]);
            var compiledGet = TryGetCompiledGet(frame, actionIndex);
            int varIdx = compiledGet?.Variable?.VariableIndex >= 0
                ? compiledGet.Variable.VariableIndex
                : authoring.VariableIndex;
            if (createdFresh)
            {
                state.CustomInt0 = 0;
                state.CustomInt1 = 0;
                accessState.StartTick = view.CurrentTick;
                accessState.AccessKind = "get";
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
                Debug.LogWarning($"[BlackboardGetSystem] Blackboard.Get (index={actionIndex}) variableIndex 未配置，跳过");
                EmitOutEvent(ref view, actionIndex);
                NodePrivateExecutionStateSupport.CompleteExecutionState(frame, actionIndex, BlackboardGetStateDescriptor);
                state.Phase = ActionPhase.Completed;
                return;
            }

            var variable = frame.FindVariable(varIdx);
            if (variable == null)
            {
                accessState.FailureReason = "variable.not-found";
                Debug.LogWarning($"[BlackboardGetSystem] Blackboard.Get (index={actionIndex}) 未找到 Index={varIdx} 的声明变量");
                EmitOutEvent(ref view, actionIndex);
                NodePrivateExecutionStateSupport.CompleteExecutionState(frame, actionIndex, BlackboardGetStateDescriptor);
                state.Phase = ActionPhase.Completed;
                return;
            }

            accessState.Scope = variable.Scope ?? string.Empty;
            accessState.VariableName = variable.Name ?? string.Empty;
            accessState.VariableType = variable.Type ?? string.Empty;
            accessState.VariableSummary = !string.IsNullOrWhiteSpace(compiledGet?.Variable?.VariableSummary)
                ? compiledGet.Variable.VariableSummary
                : BlackboardAuthoringUtility.BuildVariableSummary(variable, varIdx);

            object? value;
            if (variable.Scope == "Global")
                GlobalBlackboard.TryGet<object>(variable.Index, out value);
            else
                frame.Blackboard.TryGet<object>(variable.Index, out value);

            accessState.HasValue = value != null;
            accessState.ValueText = BlackboardAuthoringUtility.FormatValueText(value);
            accessState.AccessSummary = !string.IsNullOrWhiteSpace(compiledGet?.AccessSummary)
                ? compiledGet.AccessSummary
                : SemanticSummaryUtility.BuildBlackboardAccessSummary(
                    accessState.AccessKind,
                    accessState.VariableSummary,
                    accessState.ValueText);
            accessState.Succeeded = true;

            Debug.Log($"[BlackboardGetSystem] {variable.Scope}.{variable.Name}[{variable.Index}] → {value ?? "null"}");

            EmitOutEvent(ref view, actionIndex);
            NodePrivateExecutionStateSupport.CompleteExecutionState(frame, actionIndex, BlackboardGetStateDescriptor);
            state.Phase = ActionPhase.Completed;
        }

        private static BlackboardGetCompiledData? TryGetCompiledGet(BlueprintFrame frame, int actionIndex)
        {
            return CompiledActionResolver.TryGetBlackboardGet(frame, actionIndex);
        }
    }
}
