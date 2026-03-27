#nullable enable
using System;
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// WatchCondition 参数串编解码器。
    /// 统一处理 "key=value;key2=value2" 这一层最小协议，供编译期和运行时共享。
    /// </summary>
    public static class ConditionParameterCodec
    {
        public static ConditionParameter[] Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<ConditionParameter>();
            }

            var result = new List<ConditionParameter>();
            var segments = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index];
                var equalsIndex = segment.IndexOf('=');
                if (equalsIndex <= 0 || equalsIndex >= segment.Length - 1)
                {
                    continue;
                }

                var key = segment.Substring(0, equalsIndex).Trim();
                var value = segment.Substring(equalsIndex + 1).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                result.Add(new ConditionParameter
                {
                    Key = key,
                    Value = value
                });
            }

            return result.ToArray();
        }

        public static Dictionary<string, string> ParseToDictionary(string? raw)
        {
            var parameters = Parse(raw);
            var result = new Dictionary<string, string>(parameters.Length, StringComparer.Ordinal);
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (string.IsNullOrWhiteSpace(parameter.Key))
                {
                    continue;
                }

                result[parameter.Key] = parameter.Value ?? string.Empty;
            }

            return result;
        }

        public static string Normalize(string? raw)
        {
            return Serialize(Parse(raw));
        }

        public static int CountValidEntries(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            var count = 0;
            var segments = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                if (IsValidSegment(segments[index]))
                {
                    count++;
                }
            }

            return count;
        }

        public static int CountInvalidEntries(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 0;
            }

            var count = 0;
            var segments = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < segments.Length; index++)
            {
                if (!IsValidSegment(segments[index]))
                {
                    count++;
                }
            }

            return count;
        }

        public static string Serialize(ConditionParameter[]? parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(parameters.Length);
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Key))
                {
                    continue;
                }

                parts.Add($"{parameter.Key.Trim()}={parameter.Value?.Trim() ?? string.Empty}");
            }

            return string.Join(";", parts);
        }

        private static bool IsValidSegment(string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return false;
            }

            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex >= segment.Length - 1)
            {
                return false;
            }

            var key = segment.Substring(0, equalsIndex).Trim();
            return !string.IsNullOrWhiteSpace(key);
        }
    }
}
