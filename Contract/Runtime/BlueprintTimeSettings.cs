#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 蓝图运行时的时间配置快照。
    /// <para>
    /// 该结构用于把“当前蓝图应使用的时间规则”从配置层传递到执行层。
    /// 之所以使用一个独立的只读结构，而不是让各个系统直接读取某个全局单例，
    /// 是为了达成以下目标：
    /// </para>
    /// <para>
    /// 1. 明确配置边界：系统只消费时间配置，不关心配置来自 ScriptableObject、ProjectSettings
    ///    还是 FrameSync 注入。
    /// 2. 避免热路径到处读取全局对象：Adapter 在 BeginTick 时一次性准备好，System 直接读取。
    /// 3. 为后续 FrameSync 侧“初始化时冻结配置快照”提供统一数据结构。
    /// </para>
    /// </summary>
    public readonly struct BlueprintTimeSettings
    {
        /// <summary>
        /// 构造时间配置快照。
        /// <para>
        /// tickRate 会在构造时被规范化，确保运行时永远拿到合法值（最小为 1）。
        /// 这样后续所有时间换算逻辑都不再需要重复写防御式 fallback。
        /// </para>
        /// </summary>
        public BlueprintTimeSettings(int targetTickRate, BlueprintTimeRoundingMode roundingMode)
        {
            TargetTickRate = BlueprintTime.NormalizeTickRate(targetTickRate);
            RoundingMode = roundingMode;
        }

        /// <summary>
        /// 目标逻辑帧率（Tick/秒）。
        /// 所有“秒 → Tick”的量化计算都应基于该值，而不是在各个系统中硬编码 10、20、60 等魔法数。
        /// </summary>
        public int TargetTickRate { get; }

        /// <summary>
        /// 秒数转换为 Tick 数时使用的舍入策略。
        /// 这是时间语义的一部分，因此应和 TickRate 一起作为配置快照被传递。
        /// </summary>
        public BlueprintTimeRoundingMode RoundingMode { get; }
    }
}
