#nullable enable

namespace SceneBlueprint.Runtime.Markers.Annotations
{
    /// <summary>
    /// 怪物初始行为枚举 — 描述怪物生成后的初始 AI 行为模式。
    /// <para>
    /// 由 <see cref="SpawnAnnotation"/> 使用，策划在 Inspector 中选择。
    /// 导出时写入 Playbook，运行时由 AI 系统消费。
    /// </para>
    /// </summary>
    public enum InitialBehavior
    {
        /// <summary>待机 — 原地不动，等待触发</summary>
        Idle,

        /// <summary>巡逻 — 沿路径或区域内巡逻</summary>
        Patrol,

        /// <summary>警戒 — 在指定半径内警戒，发现敌人后追击</summary>
        Guard,

        /// <summary>埋伏 — 隐藏状态，玩家接近后突然出现</summary>
        Ambush
    }
}
