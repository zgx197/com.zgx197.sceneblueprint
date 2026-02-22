#nullable enable
using System.Collections.Generic;
using System.Linq;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// 验证规则注册表——加载和缓存项目中的 ValidationRuleSO 资产。
    /// </summary>
    public static class ValidationRuleRegistry
    {
        private static List<ValidationRuleSO>? _cache;
        private static bool _dirty = true;

        /// <summary>标记缓存为脏，下次访问时重新加载</summary>
        public static void Invalidate() => _dirty = true;

        /// <summary>获取所有已注册的验证规则（含禁用的）</summary>
        public static IReadOnlyList<ValidationRuleSO> GetAll()
        {
            EnsureLoaded();
            return _cache!;
        }

        /// <summary>获取所有启用的验证规则</summary>
        public static IReadOnlyList<ValidationRuleSO> GetEnabled()
        {
            EnsureLoaded();
            return _cache!.Where(r => r.Enabled).ToList();
        }

        /// <summary>按规则类型获取启用的验证规则</summary>
        public static IReadOnlyList<ValidationRuleSO> GetByType(ValidationType type)
        {
            EnsureLoaded();
            return _cache!.Where(r => r.Enabled && r.Type == type).ToList();
        }

        private static void EnsureLoaded()
        {
            if (!_dirty && _cache != null) return;
            _dirty = false;

            _cache = new List<ValidationRuleSO>();
            var guids = AssetDatabase.FindAssets("t:ValidationRuleSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var rule = AssetDatabase.LoadAssetAtPath<ValidationRuleSO>(path);
                if (rule != null)
                    _cache.Add(rule);
            }

            SBLog.Debug(SBLogTags.Template,
                $"ValidationRuleRegistry: 加载 {_cache.Count} 个规则 ({_cache.Count(r => r.Enabled)} 启用)");
        }
    }
}
