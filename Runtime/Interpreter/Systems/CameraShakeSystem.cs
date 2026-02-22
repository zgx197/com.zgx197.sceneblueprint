#nullable enable
using System;
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 摄像机震动系统——处理 VFX.CameraShake 节点的运行时执行。
    /// <para>
    /// 通过 ICameraShakeHandler 接口与视觉表现层解耦：
    /// - 测试场景：注入 CameraShakeHandler（Perlin Noise 驱动 Camera 偏移）
    /// - 帧同步运行时：注入 Cinemachine Impulse 或自定义实现
    /// - 未注入时：仅输出日志（编辑器一键测试模式）
    /// </para>
    /// <para>
    /// 状态管理：
    ///   IsFirstEntry → 首次进入检测（由 TransitionSystem 激活时设置）
    ///   CustomInt → 目标持续 Tick 数（从 duration 属性换算）
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Business)]
    [UpdateAfter(typeof(SpawnWaveSystem))]
    public class CameraShakeSystem : BlueprintSystemBase
    {
        public override string Name => "CameraShakeSystem";

        /// <summary>摄像机震动处理器（外部注入，与 SpawnWaveSystem.SpawnHandler 模式一致）</summary>
        public ICameraShakeHandler? ShakeHandler { get; set; }

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Vfx.CameraShake);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                // 首次进入：读取属性，计算目标 Tick 数，触发震动
                if (state.IsFirstEntry)
                {
                    state.IsFirstEntry = false;

                    float durationSec = frame.GetProperty(idx, ActionPortIds.VFXCameraShake.Duration,  0.5f);
                    float intensity   = frame.GetProperty(idx, ActionPortIds.VFXCameraShake.Intensity,  1f);
                    float frequency   = frame.GetProperty(idx, ActionPortIds.VFXCameraShake.Frequency, 20f);

                    if (durationSec <= 0f) durationSec = 0.5f;

                    // 1 秒 ≈ 60 Tick（与 FlowSystem.Flow.Delay 保持一致的换算规则）
                    state.CustomInt = Mathf.Max(1, Mathf.RoundToInt(durationSec * 60f));

                    Debug.Log($"[CameraShakeSystem] ═══ 摄像机震动开始 (index={idx}) ═══");
                    Debug.Log($"[CameraShakeSystem]   强度={intensity}, 时长={durationSec}秒 ({state.CustomInt} Ticks), 频率={frequency}");

                    // 通过 Handler 触发实际震动（未注入时仅日志）
                    ShakeHandler?.OnShakeStart(new CameraShakeData
                    {
                        Intensity = intensity,
                        Duration = durationSec,
                        Frequency = frequency
                    });
                }

                // 达到目标 Tick 数 → Completed
                if (state.TicksInPhase >= state.CustomInt)
                {
                    state.Phase = ActionPhase.Completed;
                    Debug.Log($"[CameraShakeSystem] ═══ 摄像机震动结束 (index={idx}, 持续 {state.TicksInPhase} Ticks) ═══");
                }
            }
        }
    }
}
