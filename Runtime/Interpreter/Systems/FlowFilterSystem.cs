#nullable enable
using System;
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 条件过滤系统——处理 Flow.Filter 节点。
    /// <para>
    /// 运行时逻辑：
    /// 1. 读取 compareValue DataIn 端口的值（由生产者节点在触发 onWaveStart 等事件前写入）
    /// 2. 与属性 constValue 做比较（op：==、!=、&gt;、&lt;、&gt;=、&lt;=）
    /// 3. 条件满足 → 发射 pass 端口事件；不满足 → 发射 reject 端口事件
    /// 4. compareValue 无连线 → 无条件发射 pass（视为"过滤已关闭"）
    /// 5. 进入 Listening 状态等待下一次事件（支持多次重入）
    /// 6. TransitionPropagated=true 防止 TransitionSystem 对本次完成重复传播出边
    /// </para>
    /// <para>
    /// 生命周期：Idle → Running → Listening → (收到新事件) → Running → Listening → ...
    /// 节点自身不会主动进入 Completed，由蓝图结束时统一清理。
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    [UpdateAfter(typeof(BlackboardGetSystem))]
    public class FlowFilterSystem : BlueprintSystemBase
    {
        public override string Name => "FlowFilterSystem";

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Flow.Filter);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                ProcessFilter(frame, idx, ref state);
            }
        }

        private static void ProcessFilter(BlueprintFrame frame, int actionIndex, ref ActionRuntimeState state)
        {
            var op         = frame.GetProperty(actionIndex, ActionPortIds.FlowFilter.Op);
            var constValue = frame.GetProperty(actionIndex, ActionPortIds.FlowFilter.ConstValue);

            // 读取 compareValue DataIn 端口的值
            string? compareValue = frame.GetDataPortValue(actionIndex, ActionPortIds.FlowFilter.CompareValue);

            if (compareValue == null)
            {
                // DataIn 端口无连线：无条件 pass（过滤功能关闭）
                Debug.LogWarning($"[FlowFilterSystem] Flow.Filter (index={actionIndex}) compareValue 端口无连线，无条件 pass");
                EmitPortEvents(frame, actionIndex, ActionPortIds.FlowFilter.Pass);
                state.Phase = ActionPhase.Listening;
                state.TransitionPropagated = true;
                return;
            }

            bool conditionMet = EvaluateCondition(compareValue, op, constValue);
            string portId = conditionMet ? ActionPortIds.FlowFilter.Pass : ActionPortIds.FlowFilter.Reject;

            Debug.Log($"[FlowFilterSystem] Flow.Filter (index={actionIndex}): " +
                      $"compareValue={compareValue} {op} constValue={constValue} → {conditionMet} → {portId}");

            EmitPortEvents(frame, actionIndex, portId);

            // 进入 Listening 状态等待下一次事件（支持多次重入）
            // TransitionPropagated=true 防止 TransitionSystem 对本次执行重复传播出边
            state.Phase = ActionPhase.Listening;
            state.TransitionPropagated = true;
        }

        /// <summary>
        /// 发射指定端口的所有出边事件。
        /// </summary>
        private static void EmitPortEvents(BlueprintFrame frame, int actionIndex, string portId)
        {
            var transitionIndices = frame.GetOutgoingTransitionIndices(actionIndex);
            for (int t = 0; t < transitionIndices.Count; t++)
            {
                var transition = frame.Transitions[transitionIndices[t]];
                if (transition.FromPortId == portId) // portId 已是常量
                {
                    var toIndex = frame.GetActionIndex(transition.ToActionId);
                    if (toIndex >= 0)
                    {
                        frame.PendingEvents.Add(new PortEvent(
                            actionIndex, portId, toIndex, transition.ToPortId));
                    }
                }
            }
        }

        /// <summary>
        /// 条件比较：先尝试数字比较，失败则字符串比较。
        /// </summary>
        private static bool EvaluateCondition(object? bbValue, string op, string targetValue)
        {
            if (bbValue == null)
                return op == "!="; // null != 任何值 为 true

            string bbStr = bbValue.ToString() ?? "";

            // 尝试数字比较
            if (double.TryParse(bbStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double bbNum) &&
                double.TryParse(targetValue, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double targetNum))
            {
                return op switch
                {
                    "==" => Math.Abs(bbNum - targetNum) < 0.0001,
                    "!=" => Math.Abs(bbNum - targetNum) >= 0.0001,
                    ">"  => bbNum > targetNum,
                    "<"  => bbNum < targetNum,
                    ">=" => bbNum >= targetNum,
                    "<=" => bbNum <= targetNum,
                    _    => false
                };
            }

            // 回退到字符串比较（仅支持 == 和 !=）
            return op switch
            {
                "==" => bbStr == targetValue,
                "!=" => bbStr != targetValue,
                _    => false
            };
        }
    }
}
