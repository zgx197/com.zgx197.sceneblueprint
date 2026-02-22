#nullable enable
using UnityEngine;
using SceneBlueprint.Core;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Runtime.Interpreter.Systems
{
    /// <summary>
    /// 屏幕警告系统——处理 VFX.ShowWarning 节点的运行时执行。
    /// <para>
    /// 通过 IShowWarningHandler 接口与 UI 表现层解耦：
    /// - 测试场景：注入 ShowWarningHandler（OnGUI 绘制）
    /// - 正式运行时：注入游戏 UI 框架实现
    /// - 未注入时：仅输出日志
    /// </para>
    /// <para>
    /// 状态管理：
    ///   IsFirstEntry → 首次进入检测
    ///   CustomInt → 目标持续 Tick 数（从 duration 属性换算）
    /// </para>
    /// </summary>
    [UpdateInGroup(SystemGroup.Business)]
    [UpdateAfter(typeof(CameraShakeSystem))]
    public class ShowWarningSystem : BlueprintSystemBase
    {
        public override string Name => "ShowWarningSystem";

        /// <summary>屏幕警告处理器（外部注入）</summary>
        public IShowWarningHandler? WarningHandler { get; set; }

        public override void Update(BlueprintFrame frame)
        {
            var indices = frame.GetActionIndices(AT.Vfx.ShowWarning);
            for (int i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                ref var state = ref frame.States[idx];

                if (state.Phase != ActionPhase.Running)
                    continue;

                // 首次进入：读取属性，触发显示
                if (state.IsFirstEntry)
                {
                    state.IsFirstEntry = false;

                    string text      = frame.GetProperty(idx, ActionPortIds.VFXShowWarning.Text,     "");
                    float durationSec = frame.GetProperty(idx, ActionPortIds.VFXShowWarning.Duration, 2f);
                    string style      = frame.GetProperty(idx, ActionPortIds.VFXShowWarning.Style,    "");
                    int fontSizeInt   = frame.GetProperty(idx, ActionPortIds.VFXShowWarning.FontSize,  0);

                    if (string.IsNullOrEmpty(text))  text  = "警告！";
                    if (string.IsNullOrEmpty(style)) style = "Warning";
                    if (durationSec <= 0f) durationSec = 2f;
                    float fontSize = fontSizeInt > 0 ? (float)fontSizeInt : 48f;

                    // 1 秒 ≈ 60 Tick
                    state.CustomInt = Mathf.Max(1, Mathf.RoundToInt(durationSec * 60f));

                    Debug.Log($"[ShowWarningSystem] ═══ 屏幕警告开始 (index={idx}) ═══");
                    Debug.Log($"[ShowWarningSystem]   文字=\"{text}\", 时长={durationSec}秒, 样式={style}, 字号={fontSize}");

                    // 通过 Handler 触发实际显示（未注入时仅日志）
                    WarningHandler?.OnShow(new ShowWarningData
                    {
                        Text = text,
                        Duration = durationSec,
                        Style = style,
                        FontSize = fontSize
                    });
                }

                // 达到目标 Tick 数 → Completed
                if (state.TicksInPhase >= state.CustomInt)
                {
                    state.Phase = ActionPhase.Completed;
                    Debug.Log($"[ShowWarningSystem] ═══ 屏幕警告结束 (index={idx}, 持续 {state.TicksInPhase} Ticks) ═══");
                }
            }
        }
    }
}
