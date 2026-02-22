#nullable enable
using System;

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
        /// 每帧 Tick 执行完毕后由 <see cref="BlueprintRunner"/> 调用，记录快照。
        /// </summary>
        internal void OnTickCompleted(BlueprintFrame frame)
        {
            if (!IsRecording) return;
            History.Record(frame);
        }
    }
}
