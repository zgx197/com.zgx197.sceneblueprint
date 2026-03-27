#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Runtime.Markers;
using UnityEngine;

namespace SceneBlueprint.Runtime.Interpreter
{
    /// <summary>
    /// 运行时实体对象解析服务——把 EntityRef 解析成当前场景中的 GameObject。
    /// <para>
    /// 第一版职责保持很小：
    /// 1. 直接解析 BySceneRef 指向的静态场景对象
    /// 2. 借助 IEntityRegistry，把 alias / role 等先解析成稳定实体 ID，再尝试映射回场景对象
    /// </para>
    /// <para>
    /// 该服务不负责“默认主体回退到 Player”这类业务策略；业务节点自己决定回退规则。
    /// </para>
    /// </summary>
    public interface IEntityObjectResolver
    {
        GameObject? Resolve(EntityRef entityRef, BlueprintFrame? frame);

        GameObject? Resolve(string serializedEntityRef, BlueprintFrame? frame);
    }

    public sealed class DefaultEntityObjectResolver : IEntityObjectResolver
    {
        public GameObject? Resolve(EntityRef entityRef, BlueprintFrame? frame)
        {
            if (entityRef == null || IsUnspecified(entityRef))
            {
                return null;
            }

            if (entityRef.Mode == EntityRefMode.BySceneRef)
            {
                return ResolveSceneObjectByStableId(entityRef.SceneObjectId);
            }

            var registry = frame?.Runner?.GetService<IEntityRegistry>();
            if (registry == null)
            {
                return null;
            }

            var resolvedEntityIds = registry.ResolveEntityIds(entityRef);
            for (var index = 0; index < resolvedEntityIds.Count; index++)
            {
                var sceneObject = ResolveSceneObjectByStableId(resolvedEntityIds[index]);
                if (sceneObject != null)
                {
                    return sceneObject;
                }
            }

            return null;
        }

        public GameObject? Resolve(string serializedEntityRef, BlueprintFrame? frame)
        {
            if (string.IsNullOrWhiteSpace(serializedEntityRef))
            {
                return null;
            }

            return Resolve(EntityRefCodec.Parse(serializedEntityRef), frame);
        }

        public static GameObject? ResolveSceneObjectByStableId(string stableSceneObjectId)
        {
            if (string.IsNullOrWhiteSpace(stableSceneObjectId))
            {
                return null;
            }

            var markerId = NormalizeMarkerId(stableSceneObjectId);
            if (string.IsNullOrEmpty(markerId))
            {
                return null;
            }

            var markers = Object.FindObjectsOfType<SceneMarker>();
            for (var index = 0; index < markers.Length; index++)
            {
                if (string.Equals(markers[index].MarkerId, markerId, System.StringComparison.Ordinal))
                {
                    return markers[index].gameObject;
                }
            }

            return null;
        }

        private static string NormalizeMarkerId(string stableSceneObjectId)
        {
            const string markerPrefix = "marker:";
            var normalized = stableSceneObjectId.Trim();
            return normalized.StartsWith(markerPrefix, System.StringComparison.Ordinal)
                ? normalized.Substring(markerPrefix.Length)
                : normalized;
        }

        private static bool IsUnspecified(EntityRef entityRef)
        {
            return entityRef.Mode switch
            {
                EntityRefMode.ByRole => string.IsNullOrWhiteSpace(entityRef.Role),
                EntityRefMode.ByAlias => string.IsNullOrWhiteSpace(entityRef.Alias),
                EntityRefMode.BySceneRef => string.IsNullOrWhiteSpace(entityRef.SceneObjectId),
                EntityRefMode.ByTag => string.IsNullOrWhiteSpace(entityRef.TagFilter),
                EntityRefMode.ByTags => entityRef.TagFilters == null || entityRef.TagFilters.Length == 0,
                EntityRefMode.All => false,
                EntityRefMode.Any => false,
                _ => true,
            };
        }
    }
}
