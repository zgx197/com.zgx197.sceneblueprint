#nullable enable
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 流程控制系统——处理 Flow.Start / Flow.End / Flow.Branch / Flow.Delay 等流程节点。
    /// <para>
    /// Phase 1 实现：
    /// - Flow.Start：进入 Running 后立即 Completed（由 Runner 在 Load 时设为 Running）
    /// - Flow.End：进入 Running 后标记蓝图完成
    /// </para>
    /// <para>
    /// Phase 2+ 扩展：
    /// - Flow.Branch：根据条件选择输出端口
    /// - Flow.Delay：等待指定 Tick 数后 Complete
    /// - Flow.Join：等待所有输入端口都被触发后 Complete
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Framework)]
    public class FlowSystem : BlueprintSystemBase
    {
        public override string Name => "FlowSystem";

        public override void Update(BlueprintFrame frame)
        {
            ProcessFlowStart(frame);
            ProcessFlowEnd(frame);
            ProcessFlowDelay(frame);
            ProcessFlowJoin(frame);
        }

        /// <summary>处理 Flow.Start：Running → 立即 Completed</summary>
        private void ProcessFlowStart(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Flow.Start);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase == ActionPhase.Running)
                {
                    state.Phase = ActionPhase.Completed;
                    Debug.Log($"[FlowSystem] {AT.Flow.Start} (index={idx}) → Completed");
                }
            }
        }

        /// <summary>处理 Flow.End：Running → Completed，标记蓝图执行结束</summary>
        private void ProcessFlowEnd(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Flow.End);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase == ActionPhase.Running)
                {
                    state.Phase = ActionPhase.Completed;
                    frame.IsCompleted = true;
                    Debug.Log($"[FlowSystem] {AT.Flow.End} (index={idx}) → Completed，蓝图执行结束");
                }
            }
        }

        /// <summary>
        /// 处理 Flow.Delay：Running 状态下计数 Tick，达到延迟后 Completed。
        /// <para>Phase 1 基础实现，从 Properties 读取 "delay" 属性（秒数 → Tick 数近似）。</para>
        /// </summary>
        private void ProcessFlowDelay(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Flow.Delay);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                // 首次进入：从属性读取延迟 Tick 数
                if (state.IsFirstEntry)
                {
                    state.IsFirstEntry = false;

                    float delaySec = frame.GetProperty(idx, ActionPortIds.FlowDelay.Duration, 1f);
                    // 简单映射：1 秒 ≈ 60 Tick（假设 60fps，后续迁移时使用确定性时间）
                    state.CustomInt = delaySec > 0f
                        ? Mathf.Max(1, Mathf.RoundToInt(delaySec * 60f))
                        : 60; // 无配置时默认 1 秒
                    Debug.Log($"[FlowSystem] Flow.Delay (index={idx}) 开始等待 {state.CustomInt} Ticks");
                }

                // 达到目标 Tick 数 → Completed
                if (state.TicksInPhase >= state.CustomInt)
                {
                    state.Phase = ActionPhase.Completed;
                    Debug.Log($"[FlowSystem] Flow.Delay (index={idx}) → Completed (waited {state.TicksInPhase} ticks)");
                }
            }
        }

        /// <summary>
        /// 处理 Flow.Join：Running → 立即 Completed。
        /// <para>
        /// Flow.Join 由 TransitionSystem 激活（收齐所有输入后），
        /// FlowSystem 负责将其立即标记为 Completed 并触发输出。
        /// </para>
        /// </summary>
        private void ProcessFlowJoin(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Flow.Join);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase == ActionPhase.Running)
                {
                    state.Phase = ActionPhase.Completed;
                    Debug.Log($"[FlowSystem] Flow.Join (index={idx}) → Completed");
                }
            }
        }
    }
}
