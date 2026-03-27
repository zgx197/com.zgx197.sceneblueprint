#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// EntityRef 序列化协议——在 editor / runtime 间共享 "Mode:Value" 格式。
    /// <para>
    /// 示例：
    /// - ByRole:Boss
    /// - ByAlias:FinalBoss
    /// - ByTag:Quality.Elite
    /// - ByTags:Quality.Elite+Team.Enemy
    /// - All:
    /// </para>
    /// </summary>
    public static class EntityRefCodec
    {
        public static EntityRef Parse(string? serialized)
        {
            var (mode, value) = ParseModeAndValue(serialized);
            return mode switch
            {
                EntityRefMode.ByRole     => EntityRef.FromRole(value),
                EntityRefMode.BySceneRef => EntityRef.FromSceneRef(value),
                EntityRefMode.All        => EntityRef.CreateAll(),
                EntityRefMode.Any        => EntityRef.CreateAny(),
                EntityRefMode.ByTag      => EntityRef.FromTag(value),
                EntityRefMode.ByTags     => EntityRef.FromTags(
                    value.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)),
                EntityRefMode.ByAlias    => EntityRef.FromAlias(value),
                _                        => EntityRef.FromRole(value)
            };
        }

        public static string Serialize(EntityRef entityRef)
        {
            if (entityRef == null)
            {
                return Serialize(EntityRefMode.ByRole, string.Empty);
            }

            return entityRef.Mode switch
            {
                EntityRefMode.ByRole     => Serialize(EntityRefMode.ByRole, entityRef.Role),
                EntityRefMode.BySceneRef => Serialize(EntityRefMode.BySceneRef, entityRef.SceneObjectId),
                EntityRefMode.All        => Serialize(EntityRefMode.All, string.Empty),
                EntityRefMode.Any        => Serialize(EntityRefMode.Any, string.Empty),
                EntityRefMode.ByTag      => Serialize(EntityRefMode.ByTag, entityRef.TagFilter),
                EntityRefMode.ByTags     => Serialize(EntityRefMode.ByTags, string.Join("+", entityRef.TagFilters ?? Array.Empty<string>())),
                EntityRefMode.ByAlias    => Serialize(EntityRefMode.ByAlias, entityRef.Alias),
                _                        => Serialize(EntityRefMode.ByRole, entityRef.Role)
            };
        }

        public static string Serialize(EntityRefMode mode, string value)
        {
            return $"{mode}:{value ?? string.Empty}";
        }

        private static (EntityRefMode mode, string value) ParseModeAndValue(string? serialized)
        {
            if (string.IsNullOrEmpty(serialized))
            {
                return (EntityRefMode.ByRole, string.Empty);
            }

            var colonIndex = serialized!.IndexOf(':');
            if (colonIndex < 0)
            {
                return (EntityRefMode.ByRole, serialized);
            }

            var modeText = serialized.Substring(0, colonIndex);
            var value = serialized.Substring(colonIndex + 1);

            if (Enum.TryParse(modeText, out EntityRefMode parsedMode))
            {
                return (parsedMode, value);
            }

            return (EntityRefMode.ByRole, serialized);
        }
    }
}
