#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 刷怪请求——由蓝图 System 解析后传递给 ISpawnHandler 的数据。
    /// <para>
    /// Phase 1 保留怪物特化字段（务实设计），Phase 2 拆分为通用基础 + ExtendedProperties。
    /// </para>
    /// <para>
    /// 与旧 SpawnData 的区别：
    /// - 新增 ActionId（关联蓝图节点，用于 SpawnResult 回报）
    /// - 新增 Role（实体逻辑标记，用于 EntityRegistry 映射）
    /// - 位置/旋转使用 float 数组（避免 Unity Vector3 依赖）
    /// </para>
    /// </summary>
    [Serializable]
    public class SpawnRequest
    {
        /// <summary>关联的蓝图节点 ActionId（用于 SpawnResult 回报）</summary>
        public string ActionId = "";

        /// <summary>批次内序号（0-based）</summary>
        public int Index;

        /// <summary>实体逻辑角色标记（用于 EntityRegistry 映射，如 "Boss", "Wave1_Monster"）</summary>
        public string Role = "";

        /// <summary>
        /// 稳定逻辑别名（可选）。
        /// <para>
        /// 用于把运行时实体注册成蓝图可稳定引用的主体，例如 "FinalBoss"、"PuzzleGem.Red"。
        /// 为空表示当前请求不声明额外 alias。
        /// </para>
        /// </summary>
        public string Alias = "";

        /// <summary>编译期主体 Id（可选）。</summary>
        public string CompiledSubjectId = "";

        /// <summary>公共主体 Id（可选）。</summary>
        public string PublicSubjectId = "";

        /// <summary>
        /// 源怪物槽位编号。
        /// <para>
        /// 这里表示当前关卡里的 `MonsterType`/身份槽位，主要用于编译追溯、调试投影和兼容诊断；
        /// 它不是运行时真正要生成的怪物资源 ID。
        /// </para>
        /// </summary>
        public int MonsterType;

        /// <summary>
        /// 运行时真实怪物 ID。
        /// <para>
        /// 正式主链应由编译期结合 `MonsterMappings` 完成解析并填入。
        /// 若为空，表示当前请求仍处于 legacy 兼容降级路径，接入方应显式告警或拒绝，而不是把 `MonsterType` 当成 `MonsterId` 直接混用。
        /// </para>
        /// </summary>
        public string MonsterId = "";

        /// <summary>怪物标签：Normal/Elite/Boss/Minion/Special</summary>
        public string Tag = "";

        /// <summary>存活模式：Alive/Static/Triggered</summary>
        public string SpawnMode = "Alive";

        /// <summary>初始行为：Idle/Patrol/Guard/Ambush</summary>
        public string InitialBehavior = "Idle";

        /// <summary>视觉感知范围</summary>
        public float VisionRange;

        /// <summary>听觉感知范围</summary>
        public float HearingRange;

        /// <summary>
        /// 实体标签集合（Phase 2：合并后的最终 Tag 路径列表）。
        /// <para>
        /// 由 SpawnSystem 在创建请求时合并模板默认 Tag 和蓝图覆盖 Tag 后填入。
        /// ISpawnHandler 创建实体后，应将此列表通过 EntityRegistry.SetTags 注册。
        /// </para>
        /// </summary>
        public string[]? Tags;

        /// <summary>生成位置 (x, y, z)</summary>
        public float[] Position = new float[3];

        /// <summary>生成旋转欧拉角 (x, y, z)</summary>
        public float[] EulerRotation = new float[3];

        /// <summary>当前请求是否已经带有正式解析后的 MonsterId。</summary>
        public bool HasResolvedMonsterId => !string.IsNullOrWhiteSpace(MonsterId);

        /// <summary>返回标准化后的 MonsterId；未解析时返回空字符串。</summary>
        public string GetResolvedMonsterIdOrEmpty()
        {
            return string.IsNullOrWhiteSpace(MonsterId) ? string.Empty : MonsterId.Trim();
        }

        /// <summary>
        /// 生成统一的怪物身份摘要。
        /// <para>
        /// 约定同时输出源 `MonsterType` 与最终 `MonsterId`，避免日志或调试界面只显示其中一侧。
        /// </para>
        /// </summary>
        public string GetMonsterIdentitySummary()
        {
            var resolvedMonsterId = GetResolvedMonsterIdOrEmpty();
            return $"monsterType={MonsterType}, monsterId={(resolvedMonsterId.Length > 0 ? resolvedMonsterId : "<unresolved>")}";
        }

        /// <summary>返回更适合命名或调试展示的怪物身份标签。</summary>
        public string GetMonsterIdentityLabel()
        {
            var resolvedMonsterId = GetResolvedMonsterIdOrEmpty();
            return resolvedMonsterId.Length > 0
                ? $"MonsterId.{resolvedMonsterId}"
                : $"MonsterType.{MonsterType}";
        }

        /// <summary>设置位置（便捷方法）</summary>
        public void SetPosition(float x, float y, float z)
        {
            Position[0] = x;
            Position[1] = y;
            Position[2] = z;
        }

        /// <summary>设置旋转（便捷方法）</summary>
        public void SetRotation(float x, float y, float z)
        {
            EulerRotation[0] = x;
            EulerRotation[1] = y;
            EulerRotation[2] = z;
        }
    }
}
