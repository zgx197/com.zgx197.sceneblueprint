#nullable enable
using SceneBlueprint.SpatialAbstraction;
using SceneBlueprint.SpatialAbstraction.Defaults;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// BindingScope 工具：统一 scopedBindingKey 的生成与解析。
    /// 键格式：nodeId/bindingKey —— 每个节点实例拥有独立作用域。
    /// </summary>
    internal static class BindingScopeUtility
    {
        private static readonly IBindingScopePolicy ScopePolicy = new DefaultBindingScopePolicy();

        /// <summary>
        /// 生成 scoped key：nodeId/bindingKey。
        /// </summary>
        public static string BuildScopedKey(string nodeId, string bindingKey)
        {
            return ScopePolicy.BuildScopedKey(nodeId, bindingKey);
        }

        /// <summary>
        /// 判断 key 是否已经是 scoped 形式（包含 '/'）。
        /// </summary>
        public static bool IsScopedKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.Contains("/");
        }

        /// <summary>
        /// 从 scoped key 中提取原始 bindingKey（'/' 之后的部分）。
        /// 若输入不含 '/' 则原样返回。
        /// </summary>
        public static string ExtractRawBindingKey(string scopedOrRawKey)
        {
            if (string.IsNullOrEmpty(scopedOrRawKey))
                return "";

            int idx = scopedOrRawKey.IndexOf('/');
            if (idx < 0 || idx + 1 >= scopedOrRawKey.Length)
                return scopedOrRawKey;

            return scopedOrRawKey[(idx + 1)..];
        }

        /// <summary>
        /// 从 scoped key 中提取 nodeId（'/' 之前的部分）。
        /// 若输入不含 '/' 则返回空字符串。
        /// </summary>
        public static string ExtractNodeId(string scopedKey)
        {
            if (string.IsNullOrEmpty(scopedKey))
                return "";

            int idx = scopedKey.IndexOf('/');
            return idx > 0 ? scopedKey[..idx] : "";
        }
    }
}
