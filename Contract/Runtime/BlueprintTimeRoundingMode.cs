#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 秒数换算为 Tick 数时使用的统一舍入策略。
    /// <para>
    /// 该枚举定义在 Contract 层，目的是让 Package 运行时、业务层系统、
    /// 以及后续接入的 FrameSync 侧都能共享同一套时间量化规则，避免出现
    /// 某些地方使用 RoundToInt、某些地方使用直接截断、某些地方再手写 +0.5f 的情况。
    /// </para>
    /// <para>
    /// 当前推荐默认值为 <see cref="Ceil"/>：
    /// 对“延迟 / 持续时间 / 超时”这类时间语义来说，向上取整能保证运行时等待时间
    /// 不会短于配置值，更符合策划直觉。
    /// </para>
    /// </summary>
    public enum BlueprintTimeRoundingMode
    {
        /// <summary>
        /// 向上取整。
        /// 例如：1.1 Tick → 2 Tick。
        /// 适用于不希望配置时长被运行时缩短的场景。
        /// </summary>
        Ceil = 0,

        /// <summary>
        /// 四舍五入（0.5 远离 0）。
        /// 适用于追求“最接近真实秒数”的场景。
        /// </summary>
        Round = 1,

        /// <summary>
        /// 向下取整。
        /// 例如：1.9 Tick → 1 Tick。
        /// 该策略会系统性缩短等待时间，一般不建议用于时长/延迟节点。
        /// </summary>
        Floor = 2,
    }
}
