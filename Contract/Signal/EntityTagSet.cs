#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 实体标签集合——多维度标签的运行时数据结构。
    /// <para>
    /// 存储格式为层级路径字符串集合（如 "CombatRole.Support", "Race.Humanoid"）。
    /// 与 <see cref="SignalTag"/> 层级路径格式统一，支持通配匹配。
    /// </para>
    /// <para>
    /// 设计要点：
    /// <list type="bullet">
    ///   <item>内部使用 HashSet 存储，O(1) 精确查找</item>
    ///   <item>维度查询通过前缀匹配实现（如 GetSingle("CombatRole") → "Support"）</item>
    ///   <item>MergeFrom 支持双层配置合并（模板默认 + 蓝图覆盖）</item>
    ///   <item>零 Unity 依赖，放置于 Contract 层</item>
    /// </list>
    /// </para>
    /// </summary>
    [Serializable]
    public class EntityTagSet
    {
        private HashSet<string>? _tags;

        /// <summary>创建空标签集合</summary>
        public EntityTagSet() { }

        /// <summary>从标签路径数组创建</summary>
        public EntityTagSet(params string[] tagPaths)
        {
            if (tagPaths != null && tagPaths.Length > 0)
            {
                _tags = new HashSet<string>(tagPaths, StringComparer.Ordinal);
            }
        }

        /// <summary>从标签路径集合创建</summary>
        public EntityTagSet(IEnumerable<string> tagPaths)
        {
            if (tagPaths != null)
            {
                _tags = new HashSet<string>(tagPaths, StringComparer.Ordinal);
            }
        }

        // ── 基础操作 ──

        /// <summary>添加一个标签路径（如 "CombatRole.Support"）</summary>
        public void Add(string tagPath)
        {
            if (string.IsNullOrEmpty(tagPath)) return;
            _tags ??= new HashSet<string>(StringComparer.Ordinal);
            _tags.Add(tagPath);
        }

        /// <summary>移除一个标签路径</summary>
        public bool Remove(string tagPath)
        {
            if (string.IsNullOrEmpty(tagPath) || _tags == null) return false;
            return _tags.Remove(tagPath);
        }

        /// <summary>精确匹配——是否包含指定标签路径</summary>
        public bool Has(string tagPath)
        {
            if (string.IsNullOrEmpty(tagPath) || _tags == null) return false;
            return _tags.Contains(tagPath);
        }

        /// <summary>标签数量</summary>
        public int Count => _tags?.Count ?? 0;

        /// <summary>是否为空</summary>
        public bool IsEmpty => _tags == null || _tags.Count == 0;

        /// <summary>获取所有标签路径（只读）</summary>
        public IReadOnlyCollection<string> All => _tags != null
            ? (IReadOnlyCollection<string>)_tags
            : Array.Empty<string>();

        /// <summary>清空所有标签</summary>
        public void Clear() => _tags?.Clear();

        // ── 维度查询 ──

        /// <summary>
        /// 前缀匹配——是否有任何标签属于指定维度。
        /// <para>如 HasDimension("CombatRole") → 是否有 "CombatRole.XXX" 的标签。</para>
        /// </summary>
        /// <param name="dimension">维度前缀（不含末尾点号），如 "CombatRole"</param>
        public bool HasDimension(string dimension)
        {
            if (string.IsNullOrEmpty(dimension) || _tags == null) return false;
            var prefix = dimension + ".";
            foreach (var tag in _tags)
            {
                if (tag.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取 exclusive 维度的唯一值（第一个匹配项）。
        /// <para>如 GetSingle("CombatRole") → "Support"（从 "CombatRole.Support" 提取）。</para>
        /// </summary>
        /// <param name="dimension">维度前缀，如 "CombatRole"</param>
        /// <returns>值段字符串，不匹配时返回 null</returns>
        public string? GetSingle(string dimension)
        {
            if (string.IsNullOrEmpty(dimension) || _tags == null) return null;
            var prefix = dimension + ".";
            foreach (var tag in _tags)
            {
                if (tag.StartsWith(prefix, StringComparison.Ordinal))
                    return tag.Substring(prefix.Length);
            }
            return null;
        }

        /// <summary>
        /// 获取 multiple 维度的所有值。
        /// <para>如 GetAll("Behavior") → ["Defensive", "Alerter"]。</para>
        /// </summary>
        /// <param name="dimension">维度前缀，如 "Behavior"</param>
        public IEnumerable<string> GetAll(string dimension)
        {
            if (string.IsNullOrEmpty(dimension) || _tags == null)
                yield break;
            var prefix = dimension + ".";
            foreach (var tag in _tags)
            {
                if (tag.StartsWith(prefix, StringComparison.Ordinal))
                    yield return tag.Substring(prefix.Length);
            }
        }

        /// <summary>
        /// 获取指定维度下的所有完整路径。
        /// <para>如 GetFullPaths("Behavior") → ["Behavior.Defensive", "Behavior.Alerter"]。</para>
        /// </summary>
        public IEnumerable<string> GetFullPaths(string dimension)
        {
            if (string.IsNullOrEmpty(dimension) || _tags == null)
                yield break;
            var prefix = dimension + ".";
            foreach (var tag in _tags)
            {
                if (tag.StartsWith(prefix, StringComparison.Ordinal))
                    yield return tag;
            }
        }

        // ── 通配匹配 ──

        /// <summary>
        /// 检查集合中是否有任何标签匹配指定模式（支持通配）。
        /// <para>复用 <see cref="SignalTag.MatchesPattern"/> 的匹配逻辑。</para>
        /// </summary>
        /// <param name="pattern">匹配模式（如 "Quality.Elite"、"Combat.*"、"Combat.*.Died"）</param>
        public bool MatchesAny(string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || _tags == null) return false;

            // 无通配符 → 精确匹配（O(1)）
            if (pattern.IndexOf('*') < 0)
                return _tags.Contains(pattern);

            // 有通配符 → 遍历匹配
            SignalTag patternTag = new SignalTag(pattern);
            foreach (var tag in _tags)
            {
                var t = new SignalTag(tag);
                if (t.MatchesPattern(patternTag))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查集合中是否所有指定模式都有至少一个标签匹配（AND 语义）。
        /// </summary>
        public bool MatchesAll(params string[] patterns)
        {
            if (patterns == null || patterns.Length == 0) return true;
            foreach (var p in patterns)
            {
                if (!MatchesAny(p)) return false;
            }
            return true;
        }

        // ── 合并 ──

        /// <summary>
        /// 从另一个 EntityTagSet 合并标签（双层配置合并）。
        /// <para>
        /// 合并规则：
        /// <list type="bullet">
        ///   <item>exclusive 维度：other 中有该维度的值 → 替换当前值</item>
        ///   <item>multiple 维度（或无 Schema 信息时）：union 合并</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="other">覆盖层的标签集合</param>
        /// <param name="exclusiveDimensions">exclusive 维度列表（替换语义），为 null 时全部按 union 处理</param>
        public void MergeFrom(EntityTagSet? other, IReadOnlyCollection<string>? exclusiveDimensions = null)
        {
            if (other == null || other._tags == null || other._tags.Count == 0) return;
            _tags ??= new HashSet<string>(StringComparer.Ordinal);

            if (exclusiveDimensions != null && exclusiveDimensions.Count > 0)
            {
                // 对 exclusive 维度：先移除当前维度下的所有值，再加入 other 的值
                foreach (var dim in exclusiveDimensions)
                {
                    if (!other.HasDimension(dim)) continue;
                    // 移除当前维度下的所有旧值
                    var prefix = dim + ".";
                    _tags.RemoveWhere(t => t.StartsWith(prefix, StringComparison.Ordinal));
                }
            }

            // 合并所有 other 的标签（union）
            foreach (var tag in other._tags)
            {
                _tags.Add(tag);
            }
        }

        // ── 序列化辅助 ──

        /// <summary>转为字符串数组（用于 JSON 序列化 / SpawnRequest.Tags）</summary>
        public string[] ToArray()
        {
            if (_tags == null || _tags.Count == 0) return Array.Empty<string>();
            return _tags.ToArray();
        }

        /// <summary>从字符串数组恢复</summary>
        public static EntityTagSet FromArray(string[]? tags)
        {
            if (tags == null || tags.Length == 0) return new EntityTagSet();
            return new EntityTagSet(tags);
        }

        public override string ToString()
        {
            if (_tags == null || _tags.Count == 0) return "[]";
            return "[" + string.Join(", ", _tags) + "]";
        }
    }
}
