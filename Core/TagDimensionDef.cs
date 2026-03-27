#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Tag 维度候选值定义。
    /// </summary>
    [Serializable]
    public class TagValueDef
    {
        /// <summary>值名称（如 "Support"）</summary>
        public string Name { get; set; } = "";

        /// <summary>显示标签（如 "支援"）</summary>
        public string Label { get; set; } = "";

        /// <summary>完整路径（如 "CombatRole.Support"）</summary>
        public string FullPath { get; set; } = "";
    }

    /// <summary>
    /// Tag 维度定义——描述一个 Tag 维度的元数据。
    /// <para>
    /// 由 sbdef <c>tagdimension</c> 声明 → codegen 生成，Inspector 读取用于渲染 UI。
    /// </para>
    /// <para>
    /// exclusive 维度（如 CombatRole）：一个实体只能有一个值，Inspector 用 Popup 单选。
    /// multiple 维度（如 Behavior）：一个实体可有多个值，Inspector 用 Toggle 多选。
    /// </para>
    /// </summary>
    [Serializable]
    public class TagDimensionDef
    {
        /// <summary>维度 ID（如 "CombatRole"）</summary>
        public string Id { get; set; } = "";

        /// <summary>维度显示名（如 "战斗角色"）</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>true = 单选（exclusive），false = 多选（multiple）</summary>
        public bool IsExclusive { get; set; }

        /// <summary>候选值列表</summary>
        public TagValueDef[] Values { get; set; } = Array.Empty<TagValueDef>();

        /// <summary>排序顺序（用于 Inspector 中维度的显示顺序）</summary>
        public int Order { get; set; }
    }

    /// <summary>
    /// Tag 维度定义提供者接口——由 codegen 生成的类实现。
    /// </summary>
    public interface ITagDimensionDefinitionProvider
    {
        TagDimensionDef[] Define();
    }

    /// <summary>
    /// Tag 维度注册表接口——提供维度元数据的查询能力。
    /// <para>
    /// 编辑器 Inspector 通过此接口获取维度定义，渲染结构化的 Tag 选择器 UI。
    /// 运行时 <see cref="Contract.EntityTagSet.MergeFrom"/> 通过此接口判断维度的 exclusive/multiple 属性。
    /// </para>
    /// </summary>
    public interface ITagDimensionRegistry
    {
        /// <summary>所有已注册的维度定义</summary>
        IReadOnlyList<TagDimensionDef> AllDimensions { get; }

        /// <summary>按维度 ID 查询定义</summary>
        TagDimensionDef? GetDimension(string dimensionId);

        /// <summary>获取所有 exclusive 维度的 ID 列表</summary>
        IReadOnlyCollection<string> ExclusiveDimensionIds { get; }
    }

    /// <summary>
    /// Tag 维度注册表默认实现——从 <see cref="ITagDimensionDefinitionProvider"/> 收集维度定义。
    /// </summary>
    public class TagDimensionRegistry : ITagDimensionRegistry
    {
        private readonly List<TagDimensionDef> _dimensions = new();
        private readonly Dictionary<string, TagDimensionDef> _byId = new(StringComparer.Ordinal);
        private readonly HashSet<string> _exclusiveIds = new(StringComparer.Ordinal);

        public IReadOnlyList<TagDimensionDef> AllDimensions => _dimensions;

        public TagDimensionDef? GetDimension(string dimensionId)
        {
            _byId.TryGetValue(dimensionId, out var def);
            return def;
        }

        public IReadOnlyCollection<string> ExclusiveDimensionIds => _exclusiveIds;

        /// <summary>注册一个维度定义</summary>
        public void Register(TagDimensionDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            if (_byId.ContainsKey(def.Id)) return; // 不重复注册

            _dimensions.Add(def);
            _byId[def.Id] = def;
            if (def.IsExclusive)
                _exclusiveIds.Add(def.Id);
        }

        /// <summary>从 Provider 批量注册</summary>
        public void RegisterFrom(ITagDimensionDefinitionProvider provider)
        {
            if (provider == null) return;
            var defs = provider.Define();
            if (defs == null) return;
            foreach (var def in defs)
                Register(def);
        }

        /// <summary>清空所有注册</summary>
        public void Clear()
        {
            _dimensions.Clear();
            _byId.Clear();
            _exclusiveIds.Clear();
        }
    }
}
