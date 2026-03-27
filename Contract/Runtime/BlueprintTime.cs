#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 蓝图统一时间工具。
    /// <para>
    /// 该工具类负责承载 SceneBlueprint 中所有与“秒 / Tick / deadline”相关的公共语义，
    /// 用来取代散落在各个系统里的手写乘法、四舍五入和超时判断逻辑。
    /// </para>
    /// <para>
    /// 设计目标：
    /// 1. 所有秒 → Tick 的换算都从这里走，统一 tickRate 与舍入规则。
    /// 2. 所有“延迟/超时/持续时间”节点统一使用绝对目标 Tick（deadline）模型。
    /// 3. 所有“是否到时”的判断统一使用同一套语义，避免不同系统出现边界差异。
    /// </para>
    /// </summary>
    public static class BlueprintTime
    {
        /// <summary>
        /// 表示“没有 deadline”的哨兵值。
        /// <para>
        /// 统一使用 -1，而不是 0，原因是 0 在 Tick 世界里本身就是一个合法值。
        /// 用 -1 能避免“当前正好是第 0 Tick”与“无超时”语义混淆。
        /// </para>
        /// </summary>
        public const int NoDeadline = -1;

        /// <summary>
        /// 规范化 TickRate，保证任何时间换算都至少使用 1 Tick/秒。
        /// <para>
        /// 该方法的目的，是把旧代码里分散的防御式写法（例如 >0 ? tickRate : 60）
        /// 收口到一个统一入口，避免框架各处残留历史魔法数。
        /// </para>
        /// </summary>
        public static int NormalizeTickRate(int tickRate)
        {
            return Math.Max(1, tickRate);
        }

        /// <summary>
        /// 使用时间配置快照，将秒数转换为 Tick 数。
        /// <para>
        /// 这是业务层/系统层最常用的入口：调用方不需要手动拆出 TickRate 和 RoundingMode，
        /// 直接把当前 FrameView 持有的 <see cref="BlueprintTimeSettings"/> 传进来即可。
        /// </para>
        /// </summary>
        public static int SecondsToTicks(float seconds, BlueprintTimeSettings settings, int minTicks = 0)
        {
            return SecondsToTicks(seconds, settings.TargetTickRate, settings.RoundingMode, minTicks);
        }

        /// <summary>
        /// 将秒数转换为 Tick 数。
        /// <para>
        /// 转换步骤：
        /// 1. 先规范化 tickRate 与 minTicks；
        /// 2. 对 NaN / Infinity / 小于等于 0 的输入直接回退为 minTicks；
        /// 3. 用指定的舍入策略把 seconds * tickRate 量化为整数 Tick；
        /// 4. 最终保证结果不小于 minTicks。
        /// </para>
        /// <para>
        /// 这里故意不把“0 秒是否表示无超时”之类业务语义揉进来，
        /// 因为那是具体节点（Delay / Timeout / Duration）的规则，不是纯时间换算规则。
        /// </para>
        /// </summary>
        public static int SecondsToTicks(float seconds, int tickRate, BlueprintTimeRoundingMode roundingMode, int minTicks = 0)
        {
            int normalizedTickRate = NormalizeTickRate(tickRate);
            int normalizedMinTicks = Math.Max(0, minTicks);
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            {
                return normalizedMinTicks;
            }

            double rawTicks = seconds * normalizedTickRate;
            int ticks = roundingMode switch
            {
                BlueprintTimeRoundingMode.Floor => (int)Math.Floor(rawTicks),
                BlueprintTimeRoundingMode.Round => (int)Math.Round(rawTicks, MidpointRounding.AwayFromZero),
                _ => (int)Math.Ceiling(rawTicks),
            };

            return Math.Max(normalizedMinTicks, ticks);
        }

        /// <summary>
        /// 使用时间配置快照，将 Tick 数反算为秒数。
        /// <para>
        /// 主要用于日志、调试信息、编辑器显示或导出估算，不参与运行时判定。
        /// </para>
        /// </summary>
        public static float TicksToSeconds(int ticks, BlueprintTimeSettings settings)
        {
            return TicksToSeconds(ticks, settings.TargetTickRate);
        }

        /// <summary>
        /// 将 Tick 数反算为秒数。
        /// </summary>
        public static float TicksToSeconds(int ticks, int tickRate)
        {
            return ticks / (float)NormalizeTickRate(tickRate);
        }

        /// <summary>
        /// 以“当前 Tick + 延迟 Tick 数”的方式计算绝对目标 Tick。
        /// <para>
        /// 这是 deadline 模型的基础构件：系统内部不再存“已经等了多少 Tick”，
        /// 而是直接存“应该在第几 Tick 结束”。
        /// </para>
        /// </summary>
        public static int ScheduleAfterTicks(int currentTick, int delayTicks)
        {
            return currentTick + Math.Max(0, delayTicks);
        }

        /// <summary>
        /// 以“当前 Tick + 秒数”的方式生成绝对目标 Tick。
        /// <para>
        /// 这是 Delay / Duration 这类节点的标准调度入口。
        /// </para>
        /// </summary>
        public static int ScheduleAfterSeconds(int currentTick, float seconds, BlueprintTimeSettings settings, int minTicks = 0)
        {
            return currentTick + SecondsToTicks(seconds, settings, minTicks);
        }

        /// <summary>
        /// 为“超时”语义生成 deadline。
        /// <para>
        /// 与 <see cref="ScheduleAfterSeconds"/> 的区别在于：
        /// - seconds &lt;= 0 时返回 <see cref="NoDeadline"/>，表示“无超时”；
        /// - 有超时的情况下至少保证为 1 Tick，避免刚进入节点就立刻超时。
        /// </para>
        /// </summary>
        public static int ScheduleTimeoutAfterSeconds(int currentTick, float seconds, BlueprintTimeSettings settings)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            {
                return NoDeadline;
            }

            return ScheduleAfterSeconds(currentTick, seconds, settings, 1);
        }

        /// <summary>
        /// 判断某个目标 Tick 是否代表一个真实 deadline。
        /// </summary>
        public static bool HasDeadline(int targetTick)
        {
            return targetTick != NoDeadline;
        }

        /// <summary>
        /// 判断当前 Tick 是否已经达到指定 deadline。
        /// <para>
        /// 这是所有 timed system 应统一使用的完成判断入口。
        /// 当 targetTick 为 <see cref="NoDeadline"/> 时返回 false，调用方无需额外写 if 分支。
        /// </para>
        /// </summary>
        public static bool HasReached(int currentTick, int targetTick)
        {
            return HasDeadline(targetTick) && currentTick >= targetTick;
        }
    }
}
