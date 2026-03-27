#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 实体注册表接口——管理蓝图逻辑引用（Role）与运行时实体 ID 的映射。
    /// <para>
    /// 刷怪节点创建实体后，通过 <see cref="Register"/> 建立 Role → EntityId 映射。
    /// WatchCondition 节点通过 <see cref="ResolveEntityIds"/> 将 EntityRef 解析为实际的实体 ID 列表。
    /// </para>
    /// <para>
    /// 通过 BlueprintRunner.RegisterService&lt;IEntityRegistry&gt;() 注入。
    /// </para>
    /// </summary>
    public interface IEntityRegistry
    {
        // ── Phase 1：Role 映射 ──

        /// <summary>
        /// 注册一个实体映射：Role → EntityId。
        /// <para>同一 Role 可映射多个 EntityId（如一波怪物共享同一 Role）。</para>
        /// </summary>
        void Register(string role, string entityId);

        /// <summary>
        /// 注册一个稳定逻辑别名：Alias → EntityId。
        /// <para>同一 Alias 可临时映射多个实体，但 authoring 语义上更推荐一对一主体。</para>
        /// </summary>
        void RegisterAlias(string alias, string entityId);

        /// <summary>
        /// 将 EntityRef 解析为实际的实体 ID 列表。
        /// </summary>
        IReadOnlyList<string> ResolveEntityIds(EntityRef entityRef);

        /// <summary>
        /// 获取指定 Role 下的所有实体 ID。
        /// </summary>
        IReadOnlyList<string> GetEntitiesByRole(string role);

        /// <summary>
        /// 获取指定 Alias 下的所有实体 ID。
        /// </summary>
        IReadOnlyList<string> GetEntitiesByAlias(string alias);

        /// <summary>
        /// 注销指定实体，并移除其 Role / Alias / Tag 映射。
        /// </summary>
        /// <param name="entityId">运行时实体 ID</param>
        /// <returns>若实体原本存在并已移除，返回 true；否则返回 false</returns>
        bool UnregisterEntity(string entityId);

        /// <summary>
        /// 检查指定实体是否仍在注册表中。
        /// </summary>
        bool ContainsEntity(string entityId);

        /// <summary>
        /// 清除所有映射（蓝图重置时调用）。
        /// </summary>
        void Clear();

        // ── Phase 2：Tag 系统 ──

        /// <summary>
        /// 为实体附加 Tag 集合（刷怪创建后调用）。
        /// <para>如果该实体已有 Tag，将被替换为新的集合。</para>
        /// </summary>
        /// <param name="entityId">运行时实体 ID</param>
        /// <param name="tags">标签集合</param>
        void SetTags(string entityId, EntityTagSet tags);

        /// <summary>
        /// 查询实体的 Tag 集合。
        /// </summary>
        /// <param name="entityId">运行时实体 ID</param>
        /// <returns>标签集合，未注册时返回 null</returns>
        EntityTagSet? GetTags(string entityId);

        /// <summary>
        /// 按 Tag 模式查询匹配的实体（支持通配，如 "Quality.Elite" 或 "Quality.*"）。
        /// </summary>
        /// <param name="tagPattern">标签匹配模式</param>
        /// <returns>匹配的实体 ID 列表</returns>
        IReadOnlyList<string> QueryByTag(string tagPattern);

        /// <summary>
        /// 按多个 Tag 条件查询（AND 语义——实体必须同时匹配所有 pattern）。
        /// </summary>
        /// <param name="tagPatterns">标签匹配模式数组</param>
        /// <returns>匹配的实体 ID 列表</returns>
        IReadOnlyList<string> QueryByTags(params string[] tagPatterns);
    }
}
