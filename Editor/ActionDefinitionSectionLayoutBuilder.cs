#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 把 definition-driven property section 布局收成共享入口，
    /// 让节点 Inspector、模板预览和后续更多作者态面板消费同一套 section 分组规则。
    /// </summary>
    public static class ActionDefinitionSectionLayoutBuilder
    {
        public static List<ActionDefinitionPropertySectionLayout> BuildVisibleSections(
            ActionDefinition definition,
            PropertyBag? bag = null)
        {
            var evaluationBag = bag ?? PropertyDefinitionValueUtility.CreatePropertyBag(definition.Properties, propertyValues: null);
            var sectionMap = new Dictionary<string, ActionDefinitionPropertySectionLayout>(StringComparer.Ordinal);
            var orderedSections = new List<ActionDefinitionPropertySectionLayout>();

            for (var index = 0; index < definition.Properties.Length; index++)
            {
                var property = definition.Properties[index];
                if (!string.IsNullOrEmpty(property.VisibleWhen)
                    && !VisibleWhenEvaluator.Evaluate(property.VisibleWhen, evaluationBag))
                {
                    continue;
                }

                var section = ResolveSection(property);
                if (!sectionMap.TryGetValue(section.Key, out var layout))
                {
                    layout = new ActionDefinitionPropertySectionLayout(
                        section.Key,
                        section.Title,
                        section.Order,
                        section.IsImplicitDefault);
                    sectionMap.Add(section.Key, layout);
                    orderedSections.Add(layout);
                }

                if (property.IsAdvanced)
                {
                    layout.AdvancedProperties.Add(property);
                }
                else
                {
                    layout.NormalProperties.Add(property);
                }
            }

            orderedSections.Sort(static (left, right) =>
            {
                var orderCompare = left.Order.CompareTo(right.Order);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                return string.CompareOrdinal(left.Key, right.Key);
            });

            for (var index = 0; index < orderedSections.Count; index++)
            {
                orderedSections[index].NormalProperties.Sort(ComparePropertyOrder);
                orderedSections[index].AdvancedProperties.Sort(ComparePropertyOrder);
            }

            return orderedSections;
        }

        private static int ComparePropertyOrder(PropertyDefinition left, PropertyDefinition right)
        {
            var orderCompare = left.Order.CompareTo(right.Order);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return string.CompareOrdinal(left.Key, right.Key);
        }

        private static ActionDefinitionPropertySectionInfo ResolveSection(PropertyDefinition property)
        {
            if (!string.IsNullOrWhiteSpace(property.SectionKey))
            {
                return new ActionDefinitionPropertySectionInfo(
                    property.SectionKey!,
                    string.IsNullOrWhiteSpace(property.SectionTitle) ? property.SectionKey! : property.SectionTitle!,
                    property.SectionOrder,
                    isImplicitDefault: false);
            }

            if (property.Type == PropertyType.SceneBinding)
            {
                return new ActionDefinitionPropertySectionInfo(
                    "scene-binding",
                    "场景绑定 Scene Binding",
                    900,
                    isImplicitDefault: false);
            }

            if (!string.IsNullOrWhiteSpace(property.Category))
            {
                return new ActionDefinitionPropertySectionInfo(
                    $"category:{property.Category}",
                    property.Category!,
                    property.SectionOrder,
                    isImplicitDefault: false);
            }

            return new ActionDefinitionPropertySectionInfo(
                "default",
                "基础 Base",
                0,
                isImplicitDefault: true);
        }
    }

    public sealed class ActionDefinitionPropertySectionLayout
    {
        public ActionDefinitionPropertySectionLayout(string key, string title, int order, bool isImplicitDefault)
        {
            Key = key;
            Title = title;
            Order = order;
            IsImplicitDefault = isImplicitDefault;
        }

        public string Key { get; }

        public string Title { get; }

        public int Order { get; }

        public bool IsImplicitDefault { get; }

        public List<PropertyDefinition> NormalProperties { get; } = new();

        public List<PropertyDefinition> AdvancedProperties { get; } = new();
    }

    public readonly struct ActionDefinitionPropertySectionInfo
    {
        public ActionDefinitionPropertySectionInfo(string key, string title, int order, bool isImplicitDefault)
        {
            Key = key;
            Title = title;
            Order = order;
            IsImplicitDefault = isImplicitDefault;
        }

        public string Key { get; }

        public string Title { get; }

        public int Order { get; }

        public bool IsImplicitDefault { get; }
    }
}
