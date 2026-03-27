#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// SceneBlueprint 顶层 transport metadata 访问辅助。
    /// <para>
    /// `SceneBlueprintData.Metadata` 继续作为序列化字段保留，但默认读写应视其为
    /// “运输壳 / transport envelope”，而不是业务协议本体。
    /// </para>
    /// </summary>
    public static class SceneBlueprintTransportMetadataUtility
    {
        public static PropertyValue[] Read(SceneBlueprintData? data)
        {
            return data?.Metadata ?? Array.Empty<PropertyValue>();
        }

        public static void Replace(SceneBlueprintData? data, PropertyValue[]? transportMetadata)
        {
            if (data == null)
            {
                return;
            }

            data.Metadata = transportMetadata ?? Array.Empty<PropertyValue>();
        }

        public static void Clear(SceneBlueprintData? data)
        {
            Replace(data, Array.Empty<PropertyValue>());
        }

        public static void Append(SceneBlueprintData? data, IEnumerable<PropertyValue>? transportMetadata)
        {
            if (data == null || transportMetadata == null)
            {
                return;
            }

            var existing = Read(data);
            var appended = new List<PropertyValue>(existing.Length);
            if (existing.Length > 0)
            {
                appended.AddRange(existing);
            }

            foreach (var entry in transportMetadata)
            {
                if (entry != null)
                {
                    appended.Add(entry);
                }
            }

            data.Metadata = appended.ToArray();
        }

        public static bool TryGetValue(SceneBlueprintData? data, string key, out string value)
        {
            return TryGetValue(Read(data), key, out value);
        }

        public static bool TryGetValue(PropertyValue[]? transportMetadata, string key, out string value)
        {
            value = string.Empty;
            if (transportMetadata == null
                || transportMetadata.Length == 0
                || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            for (var index = 0; index < transportMetadata.Length; index++)
            {
                var entry = transportMetadata[index];
                if (!string.Equals(entry?.Key, key, StringComparison.Ordinal))
                {
                    continue;
                }

                value = entry?.Value ?? string.Empty;
                return true;
            }

            return false;
        }

        public static string GetValue(SceneBlueprintData? data, string key)
        {
            return GetValue(Read(data), key);
        }

        public static string GetValue(PropertyValue[]? transportMetadata, string key)
        {
            return TryGetValue(transportMetadata, key, out var value)
                ? value
                : string.Empty;
        }
    }
}
