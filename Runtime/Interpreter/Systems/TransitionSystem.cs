#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 转换系统——负责端口事件路由和下游节点激活。
    /// <para>
    /// 核心职责：
    /// 1. 扫描所有 Completed 状态的 Action，根据 Transition 表生成 PortEvent
    /// 2. 消费 PendingEvents 队列，激活目标 Action（Idle → Running）
    /// 3. 评估 Transition 上的 Condition（Phase 1 仅支持 Immediate）
    /// </para>
    /// <para>
    /// 执行顺序：Order = 900（在所有业务 System 之后执行）。
    /// 原因：业务 System 在本帧可能把 Action 标记为 Completed，
    /// TransitionSystem 需要在同一帧内捕获并路由到下游，避免遗漏。
    /// </para>
    /// <para>
    /// 对齐 FrameSyncEngine 的信号传播机制：
    /// 类似于帧同步中 Signal/Event 的帧内路由。
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.PostProcess)]
    public class TransitionSystem : BlueprintSystemBase
    {
        public override string Name => "TransitionSystem";

        // 临时列表，避免每帧分配
        private readonly List<PortEvent> _newEvents = new();

        public override void Update(BlueprintFrame frame)
        {
            // ── 阶段1：扫描 Completed Action → 生成 PortEvent ──
            _newEvents.Clear();

            // TransitionPropagated 标记该 Completed Action 已经传播过出边，不再重复处理
            for (int i = 0; i < frame.States.Length; i++)
            {
                if (frame.States[i].Phase != ActionPhase.Completed)
                    continue;

                if (frame.States[i].TransitionPropagated)
                    continue; // 已传播过，跳过

                // 标记为已传播
                frame.States[i].TransitionPropagated = true;

                // 获取该 Action 的所有出边 Transition
                var transitionIndices = frame.GetOutgoingTransitionIndices(i);
                for (int t = 0; t < transitionIndices.Count; t++)
                {
                    var transition = frame.Transitions[transitionIndices[t]];

                    // Phase 1：仅处理 Immediate 条件（默认条件）
                    if (!EvaluateCondition(transition.Condition, frame))
                        continue;

                    // 查找目标 Action 索引
                    var toIndex = frame.GetActionIndex(transition.ToActionId);
                    if (toIndex < 0)
                    {
                        Debug.LogWarning($"[TransitionSystem] 目标 Action 未找到: {transition.ToActionId}");
                        continue;
                    }

                    _newEvents.Add(new PortEvent(i, transition.FromPortId, toIndex, transition.ToPortId));
                }
            }

            // 将新事件合入 PendingEvents
            if (_newEvents.Count > 0)
            {
                Debug.Log($"[TransitionSystem] 生成 {_newEvents.Count} 个新事件");
                frame.PendingEvents.AddRange(_newEvents);
            }

            // ── 阶段2：消费 PendingEvents → 激活目标 Action ──
            if (frame.PendingEvents.Count > 0)
                Debug.Log($"[TransitionSystem] 开始处理 {frame.PendingEvents.Count} 个待处理事件");
            
            for (int i = 0; i < frame.PendingEvents.Count; i++)
            {
                var evt = frame.PendingEvents[i];
                ref var targetState = ref frame.States[evt.ToActionIndex];
                var targetAction = frame.Actions[evt.ToActionIndex];

                Debug.Log($"[TransitionSystem] 处理事件 {i}: {evt}, 目标节点 TypeId={targetAction.TypeId}, 当前状态={targetState.Phase}");

                // Flow.Join 特殊处理：累加计数，收齐所有输入后才激活
                if (targetAction.TypeId == AT.Flow.Join)
                {
                    Debug.Log($"[TransitionSystem] 检测到 Flow.Join 节点 (index={evt.ToActionIndex})");
                    
                    // 使用 CustomInt 记录已收到的输入数量
                    targetState.CustomInt++;

                    // 读取需要的入边数量
                    int requiredCount = frame.GetProperty(evt.ToActionIndex, ActionPortIds.FlowJoin.InEdgeCount, 1);

                    Debug.Log($"[TransitionSystem] Flow.Join 当前收到输入: {targetState.CustomInt}/{requiredCount}");

                    if (targetState.CustomInt >= requiredCount)
                    {
                        // 收齐所有输入，激活节点
                        if (targetState.Phase == ActionPhase.Idle || targetState.Phase == ActionPhase.WaitingTrigger)
                        {
                            targetState.Phase = ActionPhase.Running;
                            targetState.TicksInPhase = 0;
                            targetState.IsFirstEntry = true;
                            Debug.Log($"[TransitionSystem] ✓ 激活: Flow.Join (index={evt.ToActionIndex}) ← 收齐 {targetState.CustomInt}/{requiredCount} 输入");
                        }
                        else
                        {
                            Debug.LogWarning($"[TransitionSystem] Flow.Join (index={evt.ToActionIndex}) 已收齐输入但状态异常: {targetState.Phase}");
                        }
                    }
                    else
                    {
                        // 还没收齐，保持 Idle 或 WaitingTrigger
                        if (targetState.Phase == ActionPhase.Idle)
                        {
                            targetState.Phase = ActionPhase.WaitingTrigger;
                        }
                        Debug.Log($"[TransitionSystem] Flow.Join (index={evt.ToActionIndex}) 等待中: {targetState.CustomInt}/{requiredCount}");
                    }
                }
                else
                {
                    // 普通节点激活逻辑
                    if (targetState.Phase == ActionPhase.Idle)
                    {
                        // 首次激活：Idle → Running
                        targetState.Phase = ActionPhase.Running;
                        targetState.TicksInPhase = 0;
                        targetState.IsFirstEntry = true;

                        // 记录激活来源（供 Flow.Filter 等节点自动推断数据来源）
                        var sourceActionId = frame.Actions[evt.FromActionIndex].Id;
                        var targetActionId = frame.Actions[evt.ToActionIndex].Id;
                        frame.Blackboard.SetInternal($"_activatedBy.{targetActionId}", sourceActionId);

                        var typeId = frame.GetTypeId(evt.ToActionIndex);
                        Debug.Log($"[TransitionSystem] ✓ 激活: {typeId} (index={evt.ToActionIndex}) ← {evt}");
                    }
                    else if (targetState.Phase == ActionPhase.Listening)
                    {
                        // 重入激活：Listening → Running（软重置）
                        // 保留 CustomInt/CustomFloat，重置执行计时和传播标记
                        targetState.SoftReset();

                        // 更新激活来源（新的事件可能来自不同的上游节点）
                        var sourceActionId = frame.Actions[evt.FromActionIndex].Id;
                        var targetActionId = frame.Actions[evt.ToActionIndex].Id;
                        frame.Blackboard.SetInternal($"_activatedBy.{targetActionId}", sourceActionId);

                        var typeId = frame.GetTypeId(evt.ToActionIndex);
                        Debug.Log($"[TransitionSystem] ✓ 重入激活: {typeId} (index={evt.ToActionIndex}) Listening → Running ← {evt}");
                    }
                }
            }

            // 清空已消费的事件
            frame.ClearEvents();
        }

        /// <summary>
        /// 评估 Transition 条件。
        /// Phase 1 仅支持 Immediate（始终通过）；后续扩展 Delay、Expression 等。
        /// </summary>
        private static bool EvaluateCondition(ConditionData? condition, BlueprintFrame frame)
        {
            if (condition == null) return true;

            switch (condition.Type)
            {
                case "Immediate":
                case "":
                    return true;

                // Phase 2+ 扩展：
                // case "Delay":     return frame.TickCount >= delayTicks;
                // case "Expression": return EvaluateExpression(condition.Expression, frame);
                // case "AllOf":     return condition.Children.All(c => EvaluateCondition(c, frame));
                // case "AnyOf":     return condition.Children.Any(c => EvaluateCondition(c, frame));

                default:
                    Debug.LogWarning($"[TransitionSystem] 未支持的条件类型: {condition.Type}，默认通过");
                    return true;
            }
        }
    }
}
