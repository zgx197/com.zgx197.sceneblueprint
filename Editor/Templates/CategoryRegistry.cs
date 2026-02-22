#nullable enable
using System.Collections.Generic;
using System.Linq;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// 分类注册表——加载和缓存项目中的 CategorySO 资产。
    /// <para>
    /// 提供按 CategoryId 查找分类的能力，以及获取排序权重用于右键菜单排序。
    /// 没有对应 CategorySO 的分类 ID 也能正常工作（返回默认值）。
    /// </para>
    /// </summary>
    public static class CategoryRegistry
    {
        private static Dictionary<string, CategorySO>? _cache;
        private static bool _dirty = true;

        // 硬编码的中文名映射表（无 CategorySO 时的后备方案）
        private static readonly Dictionary<string, string> _fallbackDisplayNames = new()
        {
            { "Flow", "流程控制" },
            { "Spawn", "刷怪" },
            { "Monster", "怪物" },
            { "Location", "位置" },
            { "Condition", "条件" },
            { "Behavior", "行为" },
            { "VFX", "视觉效果" },
            { "Trigger", "触发器" },
            { "Proxy", "代理" }
        };

        /// <summary>标记缓存为脏，下次访问时重新加载</summary>
        public static void Invalidate() => _dirty = true;

        /// <summary>获取所有已注册的分类 SO</summary>
        public static IReadOnlyList<CategorySO> GetAll()
        {
            EnsureLoaded();
            return _cache!.Values.ToList();
        }

        /// <summary>
        /// 按 CategoryId 查找分类 SO。
        /// </summary>
        /// <returns>匹配的分类 SO，未找到返回 null</returns>
        public static CategorySO? Find(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return null;
            EnsureLoaded();
            _cache!.TryGetValue(categoryId, out var result);
            return result;
        }

        /// <summary>
        /// 获取分类的排序权重。有 SO 时使用 SortOrder，无 SO 时返回默认值 1000。
        /// </summary>
        public static int GetSortOrder(string categoryId)
        {
            var so = Find(categoryId);
            return so?.SortOrder ?? 1000;
        }

        /// <summary>
        /// 获取分类的显示名称。优先级：CategorySO.DisplayName > 硬编码映射表 > categoryId 本身。
        /// </summary>
        public static string GetDisplayName(string categoryId)
        {
            var so = Find(categoryId);
            if (so != null) return so.GetDisplayName();
            
            // 使用硬编码的后备映射表
            if (_fallbackDisplayNames.TryGetValue(categoryId, out var displayName))
                return displayName;
            
            return string.IsNullOrEmpty(categoryId) ? "未分类" : categoryId;
        }

        /// <summary>
        /// 获取分类的主题色。有 SO 时使用 ThemeColor，无 SO 时返回 null。
        /// </summary>
        public static UnityEngine.Color? GetThemeColor(string categoryId)
        {
            var so = Find(categoryId);
            return so?.ThemeColor;
        }

        /// <summary>
        /// 获取分类图标。有 SO 时使用 Icon，无 SO 时返回空字符串。
        /// </summary>
        public static string GetIcon(string categoryId)
        {
            var so = Find(categoryId);
            return so?.Icon ?? "";
        }

        private static void EnsureLoaded()
        {
            if (!_dirty && _cache != null) return;
            _dirty = false;

            _cache = new Dictionary<string, CategorySO>(System.StringComparer.OrdinalIgnoreCase);
            var guids = AssetDatabase.FindAssets("t:CategorySO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cat = AssetDatabase.LoadAssetAtPath<CategorySO>(path);
                if (cat == null || string.IsNullOrEmpty(cat.CategoryId)) continue;

                if (_cache.ContainsKey(cat.CategoryId))
                {
                    SBLog.Warn(SBLogTags.Template,
                        $"CategoryRegistry: CategoryId '{cat.CategoryId}' 重复，跳过 {path}");
                    continue;
                }
                _cache[cat.CategoryId] = cat;
            }

            SBLog.Debug(SBLogTags.Template, $"CategoryRegistry: 加载 {_cache.Count} 个分类");
        }
    }
}
