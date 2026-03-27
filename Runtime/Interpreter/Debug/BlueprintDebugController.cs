#nullable enable
using System;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter.Diagnostics
{
    /// <summary>
    /// 蓝图调试控制器——管理历史快照的暂停、步进与检视。
    /// <para>
    /// 使用方式：
    /// <code>
    /// runner.DebugController = new BlueprintDebugController();
    /// // 运行中随时调用：
    /// runner.DebugController.Pause();
    /// runner.DebugController.StepBack();
    /// var snap = runner.DebugController.GetDisplaySnapshot();
    /// </code>
    /// </para>
    /// </summary>
    public class BlueprintDebugController
    {
        /// <summary>历史帧快照缓冲区</summary>
        public BlueprintFrameHistory History { get; }

        /// <summary>
        /// 各节点历史上达到的最高 Phase（跨帧峰值）。
        /// 即使节点被 RecycleCompleted 回收为 Idle，此处仍保留其峰值。
        /// 用于蓝图完成后的"执行结果汇总"视图。
        /// </summary>
        public ActionPhase[] PeakPhases { get; private set; } = Array.Empty<ActionPhase>();

        /// <summary>各节点在峰值 Phase 下累计的最大 TicksInPhase</summary>
        public int[] PeakTicks { get; private set; } = Array.Empty<int>();

        /// <summary>是否开启历史记录（默认开启，可在运行中动态关闭以节省开销）</summary>
        public bool IsRecording { get; set; } = true;

        /// <summary>当前是否处于暂停检视模式</summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// 当前检视帧的相对索引（0 = 最新帧，数字越大表示越旧）。
        /// 仅在 <see cref="IsPaused"/> 为 true 时有意义。
        /// </summary>
        public int InspectIndex { get; private set; }

        /// <summary>蓝图进入暂停时触发（场景侧 Handler 订阅以冻结场景效果）</summary>
        public event Action? OnPaused;

        /// <summary>蓝图恢复执行时触发</summary>
        public event Action? OnResumed;

        /// <summary>每次记录完一帧历史后触发，供编辑器侧同步捕获结构化运行时状态。</summary>
        public event Action<int>? OnTickRecorded;

        // ── 构造 ──

        /// <param name="historyCapacity">保留的最大帧数（默认 300，约 5 秒 @ 60fps）</param>
        public BlueprintDebugController(int historyCapacity = 300)
        {
            History = new BlueprintFrameHistory(historyCapacity);
        }

        // ── 控制接口 ──

        /// <summary>暂停蓝图执行，并将检视帧设为最新帧</summary>
        public void Pause()
        {
            IsPaused     = true;
            InspectIndex = 0;
            OnPaused?.Invoke();
        }

        /// <summary>恢复蓝图执行</summary>
        public void Resume()
        {
            IsPaused     = false;
            InspectIndex = 0;
            OnResumed?.Invoke();
        }

        /// <summary>向历史方向步进一帧（查看更旧的帧）</summary>
        public void StepBack()
            => InspectIndex = Math.Min(InspectIndex + 1, History.Count - 1);

        /// <summary>向最新方向步进一帧（查看更新的帧）</summary>
        public void StepForward()
            => InspectIndex = Math.Max(InspectIndex - 1, 0);

        /// <summary>
        /// 跳转到指定绝对 TickCount（UI 时间轴滑条使用）。
        /// 超出历史范围时自动钳位。
        /// </summary>
        public void SeekToTick(int tick)
        {
            if (History.LatestTick < 0) return;
            int index    = History.LatestTick - tick;
            InspectIndex = Math.Clamp(index, 0, History.Count - 1);
        }

        /// <summary>
        /// 获取当前检视帧的只读快照（UI 渲染用）。
        /// 未暂停时返回最新帧（index=0）。
        /// </summary>
        public BlueprintFrameSnapshot? GetDisplaySnapshot()
            => History.GetByIndex(InspectIndex);

        /// <summary>当前检视帧的绝对 TickCount（-1 表示无历史）</summary>
        public int InspectTick
            => History.LatestTick >= 0
                ? History.LatestTick - InspectIndex
                : -1;

        // ── 由 BlueprintRunner 调用（internal） ──

        /// <summary>
        /// 每帧 Tick 执行完毕后由 <see cref="BlueprintRunner"/> 调用，记录快照并更新峰值。
        /// </summary>
        internal void OnTickCompleted(BlueprintFrame frame)
        {
            if (!IsRecording) return;
            History.Record(frame);
            UpdatePeakStates(frame);
            OnTickRecorded?.Invoke(frame.TickCount);
        }

        /// <summary>增量更新各节点峰值 Phase 和 Ticks</summary>
        private void UpdatePeakStates(BlueprintFrame frame)
        {
            int count = frame.States.Length;

            // 首次或 Action 数量变化时重新分配（不产生 GC 热路径）
            if (PeakPhases.Length != count)
            {
                PeakPhases = new ActionPhase[count];
                PeakTicks  = new int[count];
            }

            for (int i = 0; i < count; i++)
            {
                var phase = frame.States[i].Phase;
                var ticks = frame.States[i].TicksInPhase;

                // 峰值 Phase：使用显式优先级（不能直接比较枚举 byte 值，Listening=4 > Completed=3 会导致错误）
                if (PhasePriority(phase) > PhasePriority(PeakPhases[i]))
                    PeakPhases[i] = phase;

                // 记录最大 TicksInPhase（Running 阶段累计的最大值）
                if (ticks > PeakTicks[i])
                    PeakTicks[i] = ticks;
            }
        }

        /// <summary>
        /// Phase 峰值优先级（用于完成汇总视图）。
        /// Completed/Failed 是终态，优先级最高；Running 次之；Listening 表示等待重激活。
        /// </summary>
        private static int PhasePriority(ActionPhase phase) => phase switch
        {
            ActionPhase.Idle           => 0,
            ActionPhase.WaitingTrigger => 1,
            ActionPhase.Listening      => 2,
            ActionPhase.Running        => 3,
            ActionPhase.Completed      => 4,
            ActionPhase.Failed         => 5,
            _                          => 0,
        };

        /// <summary>
        /// 手动通知某节点的 Phase 变化（Tick 之外的状态修改，如测试窗口的"触发"按钮）。
        /// 确保峰值追踪器不会遗漏非 Tick 周期内的状态变更。
        /// </summary>
        public void NotifyManualPhaseChange(int actionIndex, ActionPhase phase, int ticksInPhase = 0)
        {
            if (actionIndex < 0 || actionIndex >= PeakPhases.Length) return;

            if (PhasePriority(phase) > PhasePriority(PeakPhases[actionIndex]))
                PeakPhases[actionIndex] = phase;

            if (ticksInPhase > PeakTicks[actionIndex])
                PeakTicks[actionIndex] = ticksInPhase;
        }

        /// <summary>重置峰值记录（蓝图重载时调用）</summary>
        internal void ResetPeakStates()
        {
            PeakPhases = Array.Empty<ActionPhase>();
            PeakTicks  = Array.Empty<int>();
        }
    }
}
