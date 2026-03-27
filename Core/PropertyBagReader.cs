#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// PropertyBag 的定义驱动读取器。
    /// 让 Inspector authoring 侧也能和 compiler 一样按 PropertyDefinition 读取默认值与结构化字段。
    /// </summary>
    public readonly struct PropertyBagReader
    {
        private readonly PropertyBag _bag;
        private readonly Dictionary<string, PropertyDefinition>? _definitions;

        public PropertyBagReader(PropertyBag bag, PropertyDefinition[]? definitions = null)
        {
            _bag = bag ?? throw new ArgumentNullException(nameof(bag));
            if (definitions == null || definitions.Length == 0)
            {
                _definitions = null;
                return;
            }

            _definitions = new Dictionary<string, PropertyDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < definitions.Length; index++)
            {
                _definitions[definitions[index].Key] = definitions[index];
            }
        }

        public bool Has(string key) => _bag.Has(key);

        public string GetString(string key, string defaultValue = "")
        {
            var raw = _bag.GetRaw(key);
            if (raw != null)
            {
                var value = raw.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return CoerceDefaultToString(key, defaultValue);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var raw = _bag.GetRaw(key);
            if (raw != null)
            {
                switch (raw)
                {
                    case int intValue:
                        return intValue;
                    case float floatValue:
                        return Convert.ToInt32(floatValue, CultureInfo.InvariantCulture);
                    case double doubleValue:
                        return Convert.ToInt32(doubleValue, CultureInfo.InvariantCulture);
                }

                if (int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            if (TryGetDefaultValue(key, out var defaultRaw))
            {
                switch (defaultRaw)
                {
                    case int intValue:
                        return intValue;
                    case float floatValue:
                        return Convert.ToInt32(floatValue, CultureInfo.InvariantCulture);
                    case double doubleValue:
                        return Convert.ToInt32(doubleValue, CultureInfo.InvariantCulture);
                }

                if (defaultRaw != null
                    && int.TryParse(defaultRaw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            var raw = _bag.GetRaw(key);
            if (raw != null)
            {
                switch (raw)
                {
                    case float floatValue:
                        return floatValue;
                    case int intValue:
                        return intValue;
                    case double doubleValue:
                        return (float)doubleValue;
                }

                if (float.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            if (TryGetDefaultValue(key, out var defaultRaw))
            {
                switch (defaultRaw)
                {
                    case float floatValue:
                        return floatValue;
                    case int intValue:
                        return intValue;
                    case double doubleValue:
                        return (float)doubleValue;
                }

                if (defaultRaw != null
                    && float.TryParse(defaultRaw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }

            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            var raw = _bag.GetRaw(key);
            if (raw != null)
            {
                switch (raw)
                {
                    case bool boolValue:
                        return boolValue;
                    case int intValue:
                        return intValue != 0;
                }

                var text = raw.ToString();
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "1", StringComparison.Ordinal))
                {
                    return true;
                }

                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "0", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (TryGetDefaultValue(key, out var defaultRaw))
            {
                switch (defaultRaw)
                {
                    case bool boolValue:
                        return boolValue;
                    case int intValue:
                        return intValue != 0;
                }

                if (defaultRaw != null && bool.TryParse(defaultRaw.ToString(), out var parsed))
                {
                    return parsed;
                }
            }

            return defaultValue;
        }

        public List<Dictionary<string, object>> GetStructList(string key)
        {
            var raw = _bag.GetRaw(key);
            if (raw is List<Dictionary<string, object>> list)
            {
                return list;
            }

            if (raw is string json)
            {
                return StructListValueCodec.Deserialize(json, GetStructFields(key));
            }

            if (TryGetDefaultValue(key, out var defaultRaw) && defaultRaw is string defaultJson)
            {
                return StructListValueCodec.Deserialize(defaultJson, GetStructFields(key));
            }

            return new List<Dictionary<string, object>>();
        }

        public PropertyDefinition? GetDefinition(string key)
        {
            return TryGetDefinition(key, out var definition) ? definition : null;
        }

        private PropertyDefinition[] GetStructFields(string key)
        {
            return TryGetDefinition(key, out var definition) && definition.Type == PropertyType.StructList
                ? definition.StructFields ?? Array.Empty<PropertyDefinition>()
                : Array.Empty<PropertyDefinition>();
        }

        private string CoerceDefaultToString(string key, string fallback)
        {
            if (!TryGetDefaultValue(key, out var defaultRaw) || defaultRaw == null)
            {
                return fallback;
            }

            return defaultRaw switch
            {
                string stringValue => stringValue,
                bool boolValue => boolValue ? "true" : "false",
                float floatValue => floatValue.ToString("G", CultureInfo.InvariantCulture),
                double doubleValue => doubleValue.ToString("G", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => defaultRaw.ToString() ?? fallback,
            };
        }

        private bool TryGetDefaultValue(string key, out object? value)
        {
            if (TryGetDefinition(key, out var definition))
            {
                value = definition.DefaultValue;
                return definition.DefaultValue != null;
            }

            value = null;
            return false;
        }

        private bool TryGetDefinition(string key, out PropertyDefinition definition)
        {
            if (_definitions != null && _definitions.TryGetValue(key, out definition!))
            {
                return true;
            }

            definition = default!;
            return false;
        }
    }
}
