#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// PropertyDefinition 的定义驱动值工具。
    /// 统一承担三类职责：
    /// 1. 新建节点时的默认值物化。
    /// 2. StructList 子项的默认结构生成。
    /// 3. PropertyBag → PropertyValue[] 的统一序列化。
    /// </summary>
    public static class PropertyDefinitionValueUtility
    {
        public static PropertyDefinition[] CloneDefinitions(IReadOnlyList<PropertyDefinition>? definitions)
        {
            if (definitions == null || definitions.Count == 0)
            {
                return Array.Empty<PropertyDefinition>();
            }

            var clones = new PropertyDefinition[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
            {
                clones[index] = CloneDefinition(definitions[index]);
            }

            return clones;
        }

        public static PropertyDefinition CloneDefinition(PropertyDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new PropertyDefinition
            {
                Key = definition.Key,
                DisplayName = definition.DisplayName,
                Type = definition.Type,
                DefaultValue = CloneDefaultValue(definition.DefaultValue, definition.StructFields),
                Tooltip = definition.Tooltip,
                Category = definition.Category,
                SectionKey = definition.SectionKey,
                SectionTitle = definition.SectionTitle,
                SectionOrder = definition.SectionOrder,
                IsAdvanced = definition.IsAdvanced,
                Order = definition.Order,
                Min = definition.Min,
                Max = definition.Max,
                EnumOptions = CloneStringArray(definition.EnumOptions),
                EnumDisplayOptions = CloneStringArray(definition.EnumDisplayOptions),
                AssetFilterTypeName = definition.AssetFilterTypeName,
                SceneBindingType = definition.SceneBindingType,
                BindingRequirement = CloneMarkerRequirement(definition.BindingRequirement),
                VisibleWhen = definition.VisibleWhen,
                DirectorControllable = definition.DirectorControllable,
                DirectorInfluence = definition.DirectorInfluence,
                StructFields = CloneDefinitions(definition.StructFields),
                SummaryFormat = definition.SummaryFormat,
                TypeSourceKey = definition.TypeSourceKey,
                AuthoringRule = CloneAuthoringRule(definition.AuthoringRule),
            };
        }

        public static PropertyDefinition? FindClonedDefinition(
            IReadOnlyList<PropertyDefinition>? definitions,
            string propertyKey)
        {
            if (definitions == null || string.IsNullOrWhiteSpace(propertyKey))
            {
                return null;
            }

            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (string.Equals(definition.Key, propertyKey, StringComparison.Ordinal))
                {
                    return CloneDefinition(definition);
                }
            }

            return null;
        }

        public static object? CreateDefaultBagValue(PropertyDefinition definition)
        {
            if (definition.DefaultValue != null)
            {
                return NormalizeBagValue(definition, definition.DefaultValue);
            }

            return CreateImplicitBagValue(definition);
        }

        public static object CreateDefaultStructFieldValue(PropertyDefinition definition)
        {
            if (definition.DefaultValue != null)
            {
                return NormalizeStructFieldValue(definition, definition.DefaultValue);
            }

            return CreateImplicitStructFieldValue(definition);
        }

        public static Dictionary<string, object> CreateDefaultStructItem(PropertyDefinition[]? fields)
        {
            var safeFields = fields ?? Array.Empty<PropertyDefinition>();
            var item = new Dictionary<string, object>(StringComparer.Ordinal);
            for (var index = 0; index < safeFields.Length; index++)
            {
                var field = safeFields[index];
                item[field.Key] = CreateDefaultStructFieldValue(field);
            }

            return item;
        }

        public static PropertyValue[] BuildSerializedPropertyValues(
            PropertyDefinition[]? definitions,
            PropertyBag propertyBag,
            Func<PropertyDefinition, bool>? include = null)
        {
            var safeDefinitions = definitions ?? Array.Empty<PropertyDefinition>();
            var properties = new List<PropertyValue>(safeDefinitions.Length);
            for (var index = 0; index < safeDefinitions.Length; index++)
            {
                var definition = safeDefinitions[index];
                if (include != null && !include(definition))
                {
                    continue;
                }

                if (!TryCreateSerializedPropertyValue(definition, propertyBag.GetRaw(definition.Key), out var propertyValue))
                {
                    continue;
                }

                properties.Add(propertyValue);
            }

            return properties.ToArray();
        }

        public static PropertyBag CreatePropertyBag(
            PropertyDefinition[]? definitions,
            PropertyValue[]? propertyValues,
            SceneBindingEntry[]? sceneBindings = null)
        {
            var safeDefinitions = definitions ?? Array.Empty<PropertyDefinition>();
            var bag = new PropertyBag();
            for (var index = 0; index < safeDefinitions.Length; index++)
            {
                var definition = safeDefinitions[index];
                var defaultValue = CreateDefaultBagValue(definition);
                if (defaultValue != null)
                {
                    bag.Set(definition.Key, defaultValue);
                }

                if (definition.Type == PropertyType.SceneBinding)
                {
                    if (TryResolveSceneBindingValue(definition.Key, sceneBindings, out var bindingValue))
                    {
                        bag.Set(definition.Key, bindingValue);
                    }

                    continue;
                }

                if (!TryFindPropertyValue(definition.Key, propertyValues, out var propertyValue))
                {
                    continue;
                }

                bag.Set(definition.Key, DeserializePropertyValue(definition, propertyValue.Value));
            }

            return bag;
        }

        public static bool TryCreateSerializedPropertyValue(
            PropertyDefinition definition,
            object? rawValue,
            out PropertyValue propertyValue)
        {
            if (rawValue == null)
            {
                propertyValue = null!;
                return false;
            }

            propertyValue = new PropertyValue
            {
                Key = definition.Key,
                ValueType = GetSerializedValueType(definition.Type),
                Value = SerializePropertyValue(definition, rawValue),
            };
            return true;
        }

        public static object DeserializePropertyValue(
            PropertyDefinition definition,
            string? serializedValue)
        {
            var raw = serializedValue ?? string.Empty;
            return definition.Type switch
            {
                PropertyType.Float => NormalizeFloat(raw, 0f),
                PropertyType.Int => NormalizeInt(raw, 0),
                PropertyType.Bool => NormalizeBool(raw, false),
                PropertyType.StructList => NormalizeStructListJson(raw, definition.StructFields),
                PropertyType.VariableSelector => NormalizeInt(raw, -1),
                PropertyType.Enum => NormalizeString(raw, GetDefaultEnumValue(definition)),
                _ => NormalizeString(raw, string.Empty),
            };
        }

        public static string GetSerializedValueType(PropertyType type)
        {
            return type switch
            {
                PropertyType.Float => "float",
                PropertyType.Int => "int",
                PropertyType.Bool => "bool",
                PropertyType.String => "string",
                PropertyType.Enum => "enum",
                PropertyType.AssetRef => "assetRef",
                PropertyType.Vector2 => "vector2",
                PropertyType.Vector3 => "vector3",
                PropertyType.Color => "color",
                PropertyType.Tag => "tag",
                PropertyType.StructList => "json",
                PropertyType.VariableSelector => "int",
                _ => "string"
            };
        }

        public static string SerializePropertyValue(PropertyDefinition definition, object value)
        {
            var normalized = NormalizeBagValue(definition, value);
            return definition.Type switch
            {
                PropertyType.Float => ((float)normalized).ToString("G", CultureInfo.InvariantCulture),
                PropertyType.Int => ((int)normalized).ToString(CultureInfo.InvariantCulture),
                PropertyType.Bool => ((bool)normalized) ? "true" : "false",
                PropertyType.StructList => normalized as string ?? "[]",
                PropertyType.VariableSelector => ((int)normalized).ToString(CultureInfo.InvariantCulture),
                _ => normalized?.ToString() ?? string.Empty,
            };
        }

        private static object NormalizeBagValue(PropertyDefinition definition, object value)
        {
            return definition.Type switch
            {
                PropertyType.Int => NormalizeInt(value, 0),
                PropertyType.Float => NormalizeFloat(value, 0f),
                PropertyType.Bool => NormalizeBool(value, false),
                PropertyType.StructList => NormalizeStructListJson(value, definition.StructFields),
                PropertyType.VariableSelector => NormalizeInt(value, -1),
                PropertyType.Enum => NormalizeString(value, GetDefaultEnumValue(definition)),
                _ => NormalizeString(value, string.Empty),
            };
        }

        private static object NormalizeStructFieldValue(PropertyDefinition definition, object value)
        {
            return definition.Type switch
            {
                PropertyType.Int => NormalizeInt(value, 0),
                PropertyType.Float => NormalizeFloat(value, 0f),
                PropertyType.Bool => NormalizeBool(value, false),
                PropertyType.StructList => NormalizeStructItemList(value, definition.StructFields),
                PropertyType.VariableSelector => NormalizeInt(value, -1),
                PropertyType.Enum => NormalizeString(value, GetDefaultEnumValue(definition)),
                _ => NormalizeString(value, string.Empty),
            };
        }

        private static object CreateImplicitBagValue(PropertyDefinition definition)
        {
            return definition.Type switch
            {
                PropertyType.Int => 0,
                PropertyType.Float => 0f,
                PropertyType.Bool => false,
                PropertyType.StructList => "[]",
                PropertyType.VariableSelector => -1,
                PropertyType.Enum => GetDefaultEnumValue(definition),
                _ => string.Empty,
            };
        }

        private static object CreateImplicitStructFieldValue(PropertyDefinition definition)
        {
            return definition.Type switch
            {
                PropertyType.Int => 0,
                PropertyType.Float => 0f,
                PropertyType.Bool => false,
                PropertyType.StructList => new List<Dictionary<string, object>>(),
                PropertyType.VariableSelector => -1,
                PropertyType.Enum => GetDefaultEnumValue(definition),
                _ => string.Empty,
            };
        }

        private static string NormalizeStructListJson(object value, PropertyDefinition[]? fields)
        {
            if (value is string json)
            {
                return StructListValueCodec.Serialize(
                    StructListValueCodec.Deserialize(json, fields),
                    fields);
            }

            if (value is List<Dictionary<string, object>> items)
            {
                return StructListValueCodec.Serialize(CloneStructItems(items, fields), fields);
            }

            return "[]";
        }

        private static bool TryFindPropertyValue(
            string key,
            PropertyValue[]? propertyValues,
            out PropertyValue propertyValue)
        {
            var values = propertyValues ?? Array.Empty<PropertyValue>();
            for (var index = 0; index < values.Length; index++)
            {
                var candidate = values[index];
                if (candidate != null
                    && string.Equals(candidate.Key, key, StringComparison.Ordinal))
                {
                    propertyValue = candidate;
                    return true;
                }
            }

            propertyValue = null!;
            return false;
        }

        private static bool TryResolveSceneBindingValue(
            string bindingKey,
            SceneBindingEntry[]? sceneBindings,
            out string bindingValue)
        {
            var bindings = sceneBindings ?? Array.Empty<SceneBindingEntry>();
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                if (!string.Equals(binding?.BindingKey, bindingKey, StringComparison.Ordinal))
                {
                    continue;
                }

                bindingValue = string.IsNullOrWhiteSpace(binding.StableObjectId)
                    ? binding.SceneObjectId ?? string.Empty
                    : binding.StableObjectId;
                return !string.IsNullOrWhiteSpace(bindingValue);
            }

            bindingValue = string.Empty;
            return false;
        }

        private static List<Dictionary<string, object>> NormalizeStructItemList(object value, PropertyDefinition[]? fields)
        {
            if (value is string json)
            {
                return StructListValueCodec.Deserialize(json, fields);
            }

            if (value is List<Dictionary<string, object>> items)
            {
                return CloneStructItems(items, fields);
            }

            return new List<Dictionary<string, object>>();
        }

        private static List<Dictionary<string, object>> CloneStructItems(
            List<Dictionary<string, object>> items,
            PropertyDefinition[]? fields)
        {
            var cloned = new List<Dictionary<string, object>>(items.Count);
            for (var index = 0; index < items.Count; index++)
            {
                cloned.Add(CloneStructItem(items[index], fields));
            }

            return cloned;
        }

        private static Dictionary<string, object> CloneStructItem(
            Dictionary<string, object> item,
            PropertyDefinition[]? fields)
        {
            var cloned = new Dictionary<string, object>(StringComparer.Ordinal);
            var safeFields = fields ?? Array.Empty<PropertyDefinition>();

            for (var index = 0; index < safeFields.Length; index++)
            {
                var field = safeFields[index];
                if (item.TryGetValue(field.Key, out var value))
                {
                    cloned[field.Key] = NormalizeStructFieldValue(field, value);
                }
                else
                {
                    cloned[field.Key] = CreateDefaultStructFieldValue(field);
                }
            }

            foreach (var pair in item)
            {
                if (cloned.ContainsKey(pair.Key))
                {
                    continue;
                }

                cloned[pair.Key] = CloneUnknownValue(pair.Value);
            }

            return cloned;
        }

        private static object CloneUnknownValue(object value)
        {
            if (value is List<Dictionary<string, object>> items)
            {
                return CloneStructItems(items, null);
            }

            if (value is Dictionary<string, object> dictionary)
            {
                var cloned = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var pair in dictionary)
                {
                    cloned[pair.Key] = CloneUnknownValue(pair.Value);
                }

                return cloned;
            }

            return value;
        }

        private static object? CloneDefaultValue(object? value, PropertyDefinition[]? structFields)
        {
            if (value == null)
            {
                return null;
            }

            return value switch
            {
                string[] strings => CloneStringArray(strings),
                PropertyDefinition[] definitions => CloneDefinitions(definitions),
                MarkerRequirement requirement => CloneMarkerRequirement(requirement),
                List<Dictionary<string, object>> items => CloneStructItems(items, structFields),
                _ => value,
            };
        }

        private static string[]? CloneStringArray(string[]? values)
        {
            return values == null ? null : (string[])values.Clone();
        }

        private static MarkerRequirement? CloneMarkerRequirement(MarkerRequirement? requirement)
        {
            if (requirement == null)
            {
                return null;
            }

            return new MarkerRequirement
            {
                BindingKey = requirement.BindingKey,
                MarkerTypeId = requirement.MarkerTypeId,
                Required = requirement.Required,
                AllowMultiple = requirement.AllowMultiple,
                MinCount = requirement.MinCount,
                DisplayName = requirement.DisplayName,
                DefaultTag = requirement.DefaultTag,
                Exclusive = requirement.Exclusive,
                RequiredAnnotations = CloneStringArray(requirement.RequiredAnnotations),
            };
        }

        private static IPropertyAuthoringRule? CloneAuthoringRule(IPropertyAuthoringRule? rule)
        {
            if (rule == null)
            {
                return null;
            }

            return rule is IPropertyAuthoringRuleSnapshotProvider snapshotProvider
                ? snapshotProvider.CreateSnapshotRule()
                : rule;
        }

        private static string GetDefaultEnumValue(PropertyDefinition definition)
        {
            if (definition.DefaultValue != null)
            {
                var defaultText = NormalizeString(definition.DefaultValue, string.Empty);
                if (!string.IsNullOrWhiteSpace(defaultText))
                {
                    return defaultText;
                }
            }

            return definition.EnumOptions is { Length: > 0 }
                ? definition.EnumOptions[0]
                : string.Empty;
        }

        private static string NormalizeString(object? value, string fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            var result = value.ToString();
            return result ?? fallback;
        }

        private static int NormalizeInt(object value, int fallback)
        {
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                if (int.TryParse(
                    value.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed))
                {
                    return parsed;
                }

                if (float.TryParse(
                    value.ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var floatValue))
                {
                    return Convert.ToInt32(floatValue, CultureInfo.InvariantCulture);
                }

                return fallback;
            }
        }

        private static float NormalizeFloat(object value, float fallback)
        {
            try
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                if (float.TryParse(
                    value.ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
                {
                    return parsed;
                }

                return fallback;
            }
        }

        private static bool NormalizeBool(object value, bool fallback)
        {
            try
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                var text = value.ToString();
                if (string.Equals(text, "1", StringComparison.Ordinal))
                {
                    return true;
                }

                if (string.Equals(text, "0", StringComparison.Ordinal))
                {
                    return false;
                }

                return fallback;
            }
        }
    }
}
