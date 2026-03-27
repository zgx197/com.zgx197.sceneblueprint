#nullable enable
using System;
using System.Runtime.Serialization;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 实体引用模式——描述如何在蓝图中引用一个或一组实体。
    /// </summary>
    public enum EntityRefMode
    {
        /// <summary>按角色名引用（蓝图刷怪节点产出物的逻辑标记）</summary>
        ByRole,
        /// <summary>按场景对象 ID 引用（场景预置物体）</summary>
        BySceneRef,
        /// <summary>引用所有实体</summary>
        All,
        /// <summary>引用任意一个匹配的实体</summary>
        Any,
        /// <summary>按 Tag 筛选（Phase 2：单 Tag 模式匹配，支持通配）</summary>
        ByTag,
        /// <summary>按多个 Tag 筛选（Phase 2：AND 语义，实体须同时匹配所有 pattern）</summary>
        ByTags,
        /// <summary>按稳定逻辑别名引用（最小 Subject / Alias 语义承载）</summary>
        ByAlias,
    }

    /// <summary>
    /// 实体引用描述——蓝图中用于定位运行时实体的数据结构。
    /// <para>
    /// 在 Signal 系统中，WatchCondition 节点需要指定"监听哪些实体的条件变化"。
    /// EntityRef 描述了引用方式（按 Role / 按场景对象 / 全部 / 任意）。
    /// </para>
    /// <para>
    /// Phase 1 仅支持 ByRole 模式，Phase 2 演进为多标签系统。
    /// </para>
    /// </summary>
    [DataContract]
    [Serializable]
    public class EntityRef
    {
        /// <summary>引用模式</summary>
        [DataMember(Order = 0)]
        public EntityRefMode Mode = EntityRefMode.ByRole;

        /// <summary>角色名（Mode == ByRole 时使用）</summary>
        [DataMember(Order = 1)]
        public string Role = "";

        /// <summary>场景对象 ID（Mode == BySceneRef 时使用）</summary>
        [DataMember(Order = 2)]
        public string SceneObjectId = "";

        /// <summary>标签（Phase 2 多标签系统预留）</summary>
        [DataMember(Order = 3)]
        public string Tag = "";

        /// <summary>Tag 筛选模式（Mode == ByTag 时使用，如 "Quality.Elite" 或 "Quality.*"）</summary>
        [DataMember(Order = 4)]
        public string TagFilter = "";

        /// <summary>多 Tag 筛选模式（Mode == ByTags 时使用，AND 语义）</summary>
        [DataMember(Order = 5)]
        public string[] TagFilters = Array.Empty<string>();

        /// <summary>稳定逻辑别名（Mode == ByAlias 时使用）</summary>
        [DataMember(Order = 6)]
        public string Alias = "";

        /// <summary>创建 ByRole 引用</summary>
        public static EntityRef FromRole(string role) => new EntityRef { Mode = EntityRefMode.ByRole, Role = role };

        /// <summary>创建 BySceneRef 引用</summary>
        public static EntityRef FromSceneRef(string sceneObjectId) => new EntityRef { Mode = EntityRefMode.BySceneRef, SceneObjectId = sceneObjectId };

        /// <summary>创建 All 引用</summary>
        public static EntityRef CreateAll() => new EntityRef { Mode = EntityRefMode.All };

        /// <summary>创建 Any 引用</summary>
        public static EntityRef CreateAny() => new EntityRef { Mode = EntityRefMode.Any };

        /// <summary>创建 ByTag 引用（单 Tag 模式匹配）</summary>
        public static EntityRef FromTag(string tagPattern) => new EntityRef { Mode = EntityRefMode.ByTag, TagFilter = tagPattern };

        /// <summary>创建 ByTags 引用（多 Tag AND 语义）</summary>
        public static EntityRef FromTags(params string[] tagPatterns) => new EntityRef { Mode = EntityRefMode.ByTags, TagFilters = tagPatterns };

        /// <summary>创建 ByAlias 引用（稳定逻辑身份）</summary>
        public static EntityRef FromAlias(string alias) => new EntityRef { Mode = EntityRefMode.ByAlias, Alias = alias };

        public override string ToString()
        {
            return Mode switch
            {
                EntityRefMode.ByRole     => $"Role({Role})",
                EntityRefMode.BySceneRef => $"SceneRef({SceneObjectId})",
                EntityRefMode.All        => "All",
                EntityRefMode.Any        => "Any",
                EntityRefMode.ByTag      => $"Tag({TagFilter})",
                EntityRefMode.ByTags     => $"Tags({string.Join("+", TagFilters)})",
                EntityRefMode.ByAlias    => $"Alias({Alias})",
                _                        => $"Unknown({Mode})"
            };
        }
    }
}
