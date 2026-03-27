#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 收口 definition 层 SceneBinding 声明的 editor/preview/export 消费逻辑。
    /// 优先使用正式 PropertyDefinition 声明；若旧定义仍只保留 SceneRequirements，
    /// 则退回兼容视图，避免各入口继续各写一份绑定扫描协议。
    /// </summary>
    internal static class SceneBindingDeclarationSupport
    {
        private static readonly string[] EmptyAnnotations = Array.Empty<string>();

        public readonly struct DeclaredSceneBindingValue
        {
            public DeclaredSceneBindingValue(
                string bindingKey,
                string displayName,
                string bindingType,
                string rawValue,
                MarkerRequirement? requirement,
                PropertyDefinition? property = null)
            {
                BindingKey = bindingKey ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                BindingType = bindingType ?? "Transform";
                RawValue = rawValue ?? string.Empty;
                Requirement = requirement;
                Property = property;
            }

            public string BindingKey { get; }

            public string DisplayName { get; }

            public string BindingType { get; }

            public string RawValue { get; }

            public MarkerRequirement? Requirement { get; }

            public PropertyDefinition? Property { get; }
        }

        public static List<DeclaredSceneBindingValue> CollectDeclaredBindings(
            ActionDefinition definition,
            PropertyBag? propertyBag,
            bool includeEmpty = false)
        {
            return CollectDeclaredBindings(
                definition,
                propertyBag?.All,
                includeEmpty);
        }

        public static List<DeclaredSceneBindingValue> CollectDeclaredBindings(
            ActionDefinition definition,
            IReadOnlyDictionary<string, object>? propertyValues,
            bool includeEmpty = false)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var result = new List<DeclaredSceneBindingValue>();
            var properties = definition.FindSceneBindingProperties();
            if (properties.Length > 0)
            {
                for (var index = 0; index < properties.Length; index++)
                {
                    var property = properties[index];
                    var rawValue = ResolveRawValue(propertyValues, property.Key);
                    if (!includeEmpty && string.IsNullOrWhiteSpace(rawValue))
                    {
                        continue;
                    }

                    result.Add(new DeclaredSceneBindingValue(
                        property.Key,
                        property.DisplayName,
                        property.SceneBindingType?.ToString() ?? "Transform",
                        rawValue,
                        property.BindingRequirement,
                        property));
                }

                return result;
            }

            var requirements = definition.SceneRequirements ?? Array.Empty<MarkerRequirement>();
            for (var index = 0; index < requirements.Length; index++)
            {
                var requirement = requirements[index];
                var rawValue = ResolveRawValue(propertyValues, requirement.BindingKey);
                if (!includeEmpty && string.IsNullOrWhiteSpace(rawValue))
                {
                    continue;
                }

                result.Add(new DeclaredSceneBindingValue(
                    requirement.BindingKey,
                    string.IsNullOrWhiteSpace(requirement.DisplayName)
                        ? requirement.BindingKey
                        : requirement.DisplayName,
                    "Transform",
                    rawValue,
                    requirement));
            }

            return result;
        }

        public static string ResolveTitle(DeclaredSceneBindingValue binding)
        {
            if (!string.IsNullOrWhiteSpace(binding.Requirement?.DisplayName))
            {
                return binding.Requirement.DisplayName;
            }

            return string.IsNullOrWhiteSpace(binding.DisplayName)
                ? binding.BindingKey
                : binding.DisplayName;
        }

        public static string ResolveMarkerTypeId(DeclaredSceneBindingValue binding)
        {
            if (!string.IsNullOrWhiteSpace(binding.Requirement?.MarkerTypeId))
            {
                return binding.Requirement.MarkerTypeId;
            }

            return binding.BindingType switch
            {
                "Area" => "Area",
                "Path" => "Path",
                "Collider" => "Collider",
                _ => "Point"
            };
        }

        public static string ResolveConfigurationStatus(DeclaredSceneBindingValue binding)
        {
            if (!string.IsNullOrWhiteSpace(binding.RawValue))
            {
                return "已配置";
            }

            return binding.Requirement?.Required == true ? "未配置" : "可选";
        }

        public static string BuildDetailSummary(DeclaredSceneBindingValue binding)
        {
            var detail = $"绑定键: {binding.BindingKey}";
            var requirement = binding.Requirement;
            if (requirement == null)
            {
                return detail;
            }

            detail = $"{detail} | Marker: {ResolveMarkerTypeId(binding)}";
            detail = $"{detail} | {(requirement.Required ? "必需" : "可选")}";
            if (requirement.Exclusive)
            {
                detail = $"{detail} | 独占";
            }
            else if (requirement.AllowMultiple)
            {
                detail = $"{detail} | 多绑定";
            }

            var requiredAnnotations = requirement.RequiredAnnotations ?? EmptyAnnotations;
            if (requiredAnnotations.Length > 0)
            {
                detail = $"{detail} | 注解: {string.Join(", ", requiredAnnotations)}";
            }

            return detail;
        }

        private static string ResolveRawValue(
            IReadOnlyDictionary<string, object>? propertyValues,
            string bindingKey)
        {
            if (propertyValues == null
                || string.IsNullOrWhiteSpace(bindingKey)
                || !propertyValues.TryGetValue(bindingKey, out var rawValue)
                || rawValue == null)
            {
                return string.Empty;
            }

            return rawValue.ToString() ?? string.Empty;
        }
    }
}
