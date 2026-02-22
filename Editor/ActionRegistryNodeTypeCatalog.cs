#nullable enable
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 将 <see cref="ActionRegistry"/> 适配为 <see cref="INodeTypeCatalog"/>，
    /// 按需将 ActionDefinition 转换为 NodeTypeDefinition（通过 ActionNodeTypeAdapter）。
    /// <para>
    /// 使用此类后可彻底移除 NodeTypeRegistry + ActionNodeTypeAdapter.RegisterAll 的二次注册步骤，
    /// ActionRegistry 直接成为 NodeGraph 的节点类型来源。
    /// </para>
    /// </summary>
    public sealed class ActionRegistryNodeTypeCatalog : INodeTypeCatalog
    {
        private readonly ActionRegistry                         _actionRegistry;
        private Dictionary<string, NodeTypeDefinition>?        _cache;
        private IReadOnlyList<NodeTypeDefinition>?              _allCached;

        public ActionRegistryNodeTypeCatalog(ActionRegistry actionRegistry)
            => _actionRegistry = actionRegistry;

        // ── 缓存管理 ──

        /// <summary>
        /// 手动失效缓存（ActionRegistry 内容变更时调用）。
        /// 正常使用中 Registry 在构造后不再变更，此方法供测试或热重载场景使用。
        /// </summary>
        public void InvalidateCache() { _cache = null; _allCached = null; }

        private Dictionary<string, NodeTypeDefinition> Cache
        {
            get
            {
                if (_cache != null) return _cache;
                _cache = new Dictionary<string, NodeTypeDefinition>();
                foreach (var actionDef in _actionRegistry.GetAll())
                    _cache[actionDef.TypeId] = ActionNodeTypeAdapter.Convert(actionDef);
                _allCached = new System.Collections.ObjectModel.ReadOnlyCollection<NodeTypeDefinition>(
                    new List<NodeTypeDefinition>(_cache.Values));
                return _cache;
            }
        }

        // ── INodeTypeProvider ──

        public NodeTypeDefinition? GetNodeType(string typeId)
            => Cache.TryGetValue(typeId, out var def) ? def : null;

        // ── INodeTypeCatalog ──

        public IEnumerable<NodeTypeDefinition> GetAll()
            => _allCached ?? (_allCached = new System.Collections.ObjectModel.ReadOnlyCollection<NodeTypeDefinition>(
                new List<NodeTypeDefinition>(Cache.Values)));

        public IEnumerable<NodeTypeDefinition> Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return GetAll();
            var lower = keyword.ToLowerInvariant();
            return GetAll().Where(d =>
                d.TypeId.ToLowerInvariant().Contains(lower) ||
                d.DisplayName.ToLowerInvariant().Contains(lower) ||
                d.Category.ToLowerInvariant().Contains(lower));
        }

        public IEnumerable<string> GetCategories()
            => GetAll()
                .Select(d => d.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct();
    }
}
