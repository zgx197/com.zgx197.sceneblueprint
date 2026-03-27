#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 把 PropertyValue[] 上的原始字符串访问收口到一层正式 reader。
    /// 这层仍兼容当前顶层字符串 contract，但 compiler/authoring 侧不再需要各自手写解析细节。
    /// </summary>
    public readonly struct PropertyValueReader
    {
        private readonly PropertyValue[] _properties;
        private readonly Dictionary<string, PropertyDefinition>? _definitions;

        public PropertyValueReader(PropertyValue[]? properties, PropertyDefinition[]? definitions = null)
        {
            _properties = properties ?? Array.Empty<PropertyValue>();
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

        public PropertyValueReader(ActionEntry action, ActionDefinition? definition = null)
            : this(action?.Properties, definition?.Properties)
        {
        }

        public bool Has(string key)
        {
            for (var index = 0; index < _properties.Length; index++)
            {
                if (string.Equals(_properties[index].Key, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            return CoerceDefaultToString(key, defaultValue);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (TryGetValue(key, out var value)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            if (TryGetDefaultValue(key, out var rawDefault))
            {
                if (rawDefault is int intValue)
                {
                    return intValue;
                }

                if (rawDefault is float floatValue)
                {
                    return Convert.ToInt32(floatValue, CultureInfo.InvariantCulture);
                }

                if (rawDefault != null
                    && int.TryParse(rawDefault.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    return parsed;
                }
            }

            return defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (TryGetValue(key, out var value)
                && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            if (TryGetDefaultValue(key, out var rawDefault))
            {
                if (rawDefault is float floatValue)
                {
                    return floatValue;
                }

                if (rawDefault is int intValue)
                {
                    return intValue;
                }

                if (rawDefault != null
                    && float.TryParse(rawDefault.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    return parsed;
                }
            }

            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (TryGetValue(key, out var value))
            {
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "1", StringComparison.Ordinal))
                {
                    return true;
                }

                if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "0", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (TryGetDefaultValue(key, out var rawDefault))
            {
                if (rawDefault is bool boolValue)
                {
                    return boolValue;
                }

                if (rawDefault != null
                    && bool.TryParse(rawDefault.ToString(), out var parsed))
                {
                    return parsed;
                }
            }

            return defaultValue;
        }

        public List<Dictionary<string, object>> GetStructList(string key)
        {
            var json = GetRawOrDefaultString(key, "[]");
            if (!TryGetDefinition(key, out var definition) || definition.Type != PropertyType.StructList)
            {
                return StructListValueCodec.Deserialize(json, Array.Empty<PropertyDefinition>());
            }

            return StructListValueCodec.Deserialize(json, definition.StructFields);
        }

        public bool TryGetStructList(string key, out List<Dictionary<string, object>> items)
        {
            items = GetStructList(key);
            return items.Count > 0 || Has(key);
        }

        public PropertyDefinition? GetDefinition(string key)
        {
            return TryGetDefinition(key, out var definition) ? definition : null;
        }

        private bool TryGetValue(string key, out string value)
        {
            for (var index = 0; index < _properties.Length; index++)
            {
                if (string.Equals(_properties[index].Key, key, StringComparison.Ordinal))
                {
                    value = _properties[index].Value ?? string.Empty;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private string GetRawOrDefaultString(string key, string fallback)
        {
            if (TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return CoerceDefaultToString(key, fallback);
        }

        private string CoerceDefaultToString(string key, string fallback)
        {
            if (!TryGetDefaultValue(key, out var rawDefault) || rawDefault == null)
            {
                return fallback;
            }

            return rawDefault switch
            {
                string stringValue => stringValue,
                bool boolValue => boolValue ? "true" : "false",
                float floatValue => floatValue.ToString("G", CultureInfo.InvariantCulture),
                double doubleValue => doubleValue.ToString("G", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => rawDefault.ToString() ?? fallback,
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
