#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 场景对象代理节点类型 ID 常量。
    /// <para>
    /// Proxy 节点不是 Action，而是场景对象的图中表示。
    /// TypeId 格式：Proxy.{MarkerTypeId}
    /// </para>
    /// </summary>
    public static class SceneObjectProxyTypes
    {
        /// <summary>点标记代理</summary>
        public const string Point = "Proxy.Point";

        /// <summary>区域标记代理</summary>
        public const string Area = "Proxy.Area";

        /// <summary>
        /// 根据标记类型 ID 生成 Proxy 节点类型 ID
        /// </summary>
        public static string FromMarkerType(string markerTypeId)
        {
            return $"Proxy.{markerTypeId}";
        }

        /// <summary>
        /// 判断是否为 Proxy 节点类型
        /// </summary>
        public static bool IsProxyType(string nodeTypeId)
        {
            return nodeTypeId.StartsWith("Proxy.");
        }

        /// <summary>
        /// 从 Proxy 节点类型 ID 提取标记类型 ID
        /// </summary>
        public static string? ExtractMarkerType(string proxyTypeId)
        {
            if (!IsProxyType(proxyTypeId))
                return null;

            return proxyTypeId.Substring("Proxy.".Length);
        }
    }
}
