#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    public static class CompiledMetadataTransportUtility
    {
        public static string BuildMetadataKey(string metadataKeyPrefix, string? actionId)
        {
            return $"{metadataKeyPrefix?.Trim() ?? string.Empty}{actionId?.Trim() ?? string.Empty}";
        }

        public static bool TryReadPayload<TPayload>(
            PropertyValue[]? metadata,
            string metadataKeyPrefix,
            string? actionId,
            TryParsePayloadDelegate<TPayload> tryParse,
            out TPayload? payload)
            where TPayload : class
        {
            payload = null;
            if (metadata == null
                || metadata.Length == 0
                || string.IsNullOrWhiteSpace(metadataKeyPrefix)
                || string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            var key = BuildMetadataKey(metadataKeyPrefix, actionId);
            for (var index = 0; index < metadata.Length; index++)
            {
                var entry = metadata[index];
                if (!string.Equals(entry?.Key, key, StringComparison.Ordinal))
                {
                    continue;
                }

                return tryParse(entry.Value, out payload);
            }

            return false;
        }

        public delegate bool TryParsePayloadDelegate<TPayload>(string? rawPayload, out TPayload? payload)
            where TPayload : class;
    }
}
