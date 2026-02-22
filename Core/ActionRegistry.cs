#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  行动注册表 (ActionRegistry)
    //
    //  ActionRegistry 是行动类型的中央管理器，类似于 GAS 中的
    //  AbilitySystemComponent 的注册表部分。它负责：
    //  - 注册所有可用的 ActionDefinition
    //  - 提供按 TypeId 查找、按分类过滤等查询能力
    //  - 通过 AutoDiscover() 自动扫描和注册所有标注了 [ActionType] 的 Provider
    //
    //  自动发现机制：
    //  1. 开发者创建一个类，实现 IActionDefinitionProvider 接口
    //  2. 在类上标注 [ActionType("Combat.Spawn")] 属性
    //  3. 调用 registry.AutoDiscover() 时会自动扫描并注册
    //
    //  使用流程：
    //    var registry = new ActionRegistry();
    //    registry.AutoDiscover();                          // 自动发现并注册所有行动
    //    var spawnDef = registry.Get("Combat.Spawn");      // 查找行动定义
    //    var combatActions = registry.GetByCategory("Combat"); // 获取某分类下所有行动
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 标注在 <see cref="IActionDefinitionProvider"/> 实现类上，声明行动类型 ID。
    /// <para>
    /// AutoDiscover() 会扫描所有标注了此 Attribute 的类，
    /// 并调用其 Define() 方法来获取 ActionDefinition。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [ActionType("Combat.Spawn")]
    /// public class SpawnActionDef : IActionDefinitionProvider { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ActionTypeAttribute : Attribute
    {
        /// <summary>行动类型 ID，如 "Combat.Spawn"</summary>
        public string TypeId { get; }

        public ActionTypeAttribute(string typeId) { TypeId = typeId; }
    }

    /// <summary>
    /// 行动定义提供者接口——实现此接口并标注 [ActionType] 即可被自动发现。
    /// <para>
    /// 每个实现类代表一种行动类型，Define() 方法返回该行动的完整定义。
    /// 这种设计让行动定义分散在各自的文件中，而不是集中在一个巨大的注册文件里。
    /// </para>
    /// </summary>
    public interface IActionDefinitionProvider
    {
        /// <summary>返回行动定义（元数据）</summary>
        ActionDefinition Define();
    }

    /// <summary>
    /// 行动注册表接口——定义查询行动定义的能力。
    /// <para>抽象接口，便于测试时 Mock。</para>
    /// </summary>
    public interface IActionRegistry
    {
        /// <summary>注册一个行动定义。TypeId 重复时抛异常。</summary>
        void Register(ActionDefinition definition);

        /// <summary>通过 TypeId 获取行动定义。未找到时抛 KeyNotFoundException。</summary>
        ActionDefinition Get(string typeId);

        /// <summary>尝试获取行动定义。未找到时返回 false，不抛异常。</summary>
        bool TryGet(string typeId, out ActionDefinition definition);

        /// <summary>获取某个分类下的所有行动。分类不存在时返回空列表。</summary>
        IReadOnlyList<ActionDefinition> GetByCategory(string category);

        /// <summary>获取所有已注册的行动定义</summary>
        IReadOnlyList<ActionDefinition> GetAll();

        /// <summary>获取所有已注册的分类名列表</summary>
        IReadOnlyList<string> GetCategories();
    }

    /// <summary>
    /// 行动注册表——管理所有 ActionDefinition 的注册和查找。
    /// <para>
    /// 内部维护三个索引结构：
    /// - 按 TypeId 查找（Dictionary）
    /// - 按 Category 分组（Dictionary of List）
    /// - 全量列表（List）
    /// </para>
    /// </summary>
    public class ActionRegistry : IActionRegistry
    {
        /// <summary>按 TypeId 索引——O(1) 查找</summary>
        private readonly Dictionary<string, ActionDefinition> _definitions = new Dictionary<string, ActionDefinition>();

        /// <summary>按 Category 分组索引——用于搜索窗分组显示</summary>
        private readonly Dictionary<string, List<ActionDefinition>> _byCategory = new Dictionary<string, List<ActionDefinition>>();

        /// <summary>全量列表——保持插入顺序</summary>
        private readonly List<ActionDefinition> _all = new List<ActionDefinition>();

        /// <summary>分类名列表——保持分类首次出现的顺序</summary>
        private readonly List<string> _categories = new List<string>();

        /// <summary>
        /// 注册一个行动定义。
        /// </summary>
        /// <param name="definition">行动定义</param>
        /// <exception cref="ArgumentException">TypeId 为空</exception>
        /// <exception cref="InvalidOperationException">TypeId 已存在</exception>
        public void Register(ActionDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.TypeId))
                throw new ArgumentException("ActionDefinition.TypeId 不能为空");

            if (_definitions.ContainsKey(definition.TypeId))
                throw new InvalidOperationException($"TypeId '{definition.TypeId}' 已注册");

            // 加入主索引和全量列表
            _definitions[definition.TypeId] = definition;
            _all.Add(definition);

            // 加入分类索引
            var category = definition.Category ?? "";
            if (!_byCategory.TryGetValue(category, out var list))
            {
                list = new List<ActionDefinition>();
                _byCategory[category] = list;
                _categories.Add(category);
            }
            list.Add(definition);
        }

        /// <summary>
        /// 通过 TypeId 获取行动定义。未找到时抛出 KeyNotFoundException。
        /// </summary>
        public ActionDefinition Get(string typeId)
        {
            if (_definitions.TryGetValue(typeId, out var def))
                return def;
            throw new KeyNotFoundException($"未找到 TypeId '{typeId}' 的 ActionDefinition");
        }

        /// <summary>
        /// 尝试获取行动定义。未找到时返回 false，不抛异常。
        /// </summary>
        public bool TryGet(string typeId, out ActionDefinition definition)
        {
            return _definitions.TryGetValue(typeId, out definition!);
        }

        /// <summary>获取某个分类下的所有行动。分类不存在时返回空数组。</summary>
        public IReadOnlyList<ActionDefinition> GetByCategory(string category)
        {
            if (_byCategory.TryGetValue(category, out var list))
                return list;
            return Array.Empty<ActionDefinition>();
        }

        /// <summary>获取所有已注册的行动定义（保持注册顺序）</summary>
        public IReadOnlyList<ActionDefinition> GetAll() => _all;

        /// <summary>获取所有已注册的分类名（保持首次出现的顺序）</summary>
        public IReadOnlyList<string> GetCategories() => _categories;

        /// <summary>
        /// 通过反射自动发现并注册所有行动定义。
        /// <para>
        /// 扫描流程：
        /// 1. 遍历 AppDomain 中所有已加载的程序集（跳过系统程序集）
        /// 2. 找到所有实现 IActionDefinitionProvider 且标注了 [ActionType] 的类
        /// 3. 实例化 Provider 并调用 Define() 获取 ActionDefinition
        /// 4. 注册到 Registry 中
        /// </para>
        /// <para>此方法是幂等的——多次调用不会重复注册。</para>
        /// </summary>
        public void AutoDiscover()
        {
            var providerType = typeof(IActionDefinitionProvider);

            // 遍历所有已加载的程序集
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 跳过系统程序集以提升扫描性能
                var assemblyName = assembly.GetName().Name ?? "";
                if (assemblyName.StartsWith("System") || assemblyName.StartsWith("Microsoft")
                    || assemblyName.StartsWith("mscorlib") || assemblyName.StartsWith("netstandard"))
                    continue;

                // 安全获取类型列表（某些程序集可能无法完全加载）
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex)
                { types = ex.Types.Where(t => t != null).ToArray()!; }

                AutoDiscoverFromTypes(types);
            }
        }

        /// <summary>
        /// 从预先提供的类型集合中自动发现并注册 <see cref="IActionDefinitionProvider"/>。
        /// <para>
        /// 在 Unity Editor 中推荐改由调用方传入
        /// <c>TypeCache.GetTypesDerivedFrom&lt;IActionDefinitionProvider&gt;()</c>
        /// 以替代全反射扫描，性能提升 10–100 倍。
        /// </para>
        /// <para>此方法是幂等的——已注册的 TypeId 会被跳过。</para>
        /// </summary>
        public void AutoDiscover(IEnumerable<Type> types)
            => AutoDiscoverFromTypes(types);

        private void AutoDiscoverFromTypes(IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IActionDefinitionProvider).IsAssignableFrom(type)) continue;

                var attr = type.GetCustomAttribute<ActionTypeAttribute>();
                if (attr == null) continue;
                if (_definitions.ContainsKey(attr.TypeId)) continue;

                try
                {
                    var provider = (IActionDefinitionProvider)Activator.CreateInstance(type)!;
                    Register(provider.Define());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoDiscover 跳过 {type.Name}: {ex.Message}");
                }
            }
        }
    }
}
