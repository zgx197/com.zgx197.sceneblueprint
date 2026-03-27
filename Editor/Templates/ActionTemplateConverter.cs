#nullable enable
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Math;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Templates;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// 将 <see cref="ActionTemplateSO"/> 转换为框架可消费的 <see cref="ActionDefinition"/>。
    /// <para>
    /// 转换过程处理：
    /// <list type="bullet">
    ///   <item>UnityEngine.Color → NodeGraph.Math.Color4 颜色转换</item>
    ///   <item>SO 嵌套枚举 → Core 枚举映射</item>
    ///   <item>字符串默认值 → 类型化默认值解析</item>
    ///   <item>端口、属性、场景需求的完整转换</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class ActionTemplateConverter
    {
        /// <summary>
        /// 将 ActionTemplateSO 转换为 ActionDefinition。
        /// </summary>
        /// <param name="template">策划配置的 SO 模板</param>
        /// <returns>框架可消费的 ActionDefinition</returns>
        public static ActionDefinition Convert(ActionTemplateSO template)
        {
            var def = new ActionDefinition
            {
                TypeId = template.TypeId,
                DisplayName = template.DisplayName,
                Category = template.Category,
                Description = template.Description,
                ThemeColor = ToColor4(template.ThemeColor),
                Icon = string.IsNullOrEmpty(template.Icon) ? null : template.Icon,
                Duration = ConvertDuration(template.Duration),
            };

            var requirementByBindingKey = BuildRequirementMap(template);

            // ── 端口：默认 FlowIn("in") + 用户配置的输出端口 ──
            var ports = new List<PortDefinition>
            {
                Port.In("in", "激活")
            };
            foreach (var p in template.OutputPorts)
            {
                if (string.IsNullOrEmpty(p.Id)) continue;
                ports.Add(ConvertPort(p));
            }
            def.Ports = ports.ToArray();

            def.OutputVariables = template.OutputVariables
                .Select(ConvertOutputVariable)
                .ToArray();

            // ── 属性 ──
            def.Properties = template.Properties
                .Where(p => !string.IsNullOrEmpty(p.Key))
                .Select(p => ConvertProperty(
                    p,
                    requirementByBindingKey.TryGetValue(p.Key, out var requirement)
                        ? requirement
                        : null))
                .ToArray();

            return def;
        }

        // ═══════════════════════════════════════════════════════════
        //  内部转换方法
        // ═══════════════════════════════════════════════════════════

        private static Color4 ToColor4(UnityEngine.Color c)
        {
            return new Color4(c.r, c.g, c.b, c.a);
        }

        private static ActionDuration ConvertDuration(ActionTemplateSO.ActionDurationEntry entry)
        {
            return entry switch
            {
                ActionTemplateSO.ActionDurationEntry.Instant => ActionDuration.Instant,
                ActionTemplateSO.ActionDurationEntry.Duration => ActionDuration.Duration,
                ActionTemplateSO.ActionDurationEntry.Passive => ActionDuration.Passive,
                _ => ActionDuration.Instant
            };
        }

        private static PortDefinition ConvertPort(ActionTemplateSO.PortEntry p)
        {
            var port = p.PortType switch
            {
                ActionTemplateSO.PortTypeEntry.EventOut => Port.Event(p.Id, p.DisplayName),
                _ => Port.Out(p.Id, p.DisplayName)
            };

            if (p.GraphRole != PortGraphRole.None)
            {
                port.WithGraphRole(p.GraphRole, p.SummaryLabel, p.MinConnections);
            }

            return port;
        }

        private static PropertyDefinition ConvertProperty(
            ActionTemplateSO.PropertyEntry p,
            MarkerRequirement? bindingRequirement)
        {
            var prop = new PropertyDefinition
            {
                Key = p.Key,
                DisplayName = p.DisplayName,
                Type = ConvertPropertyType(p.Type),
                Tooltip = string.IsNullOrEmpty(p.Tooltip) ? null : p.Tooltip,
                Category = string.IsNullOrEmpty(p.Category) ? null : p.Category,
                SectionKey = string.IsNullOrWhiteSpace(p.SectionKey) ? null : p.SectionKey,
                SectionTitle = string.IsNullOrWhiteSpace(p.SectionTitle) ? null : p.SectionTitle,
                SectionOrder = p.SectionOrder,
                IsAdvanced = p.IsAdvanced,
                Order = p.Order,
                VisibleWhen = string.IsNullOrEmpty(p.VisibleWhen) ? null : p.VisibleWhen,
                TypeSourceKey = string.IsNullOrWhiteSpace(p.TypeSourceKey) ? null : p.TypeSourceKey,
            };

            // 解析默认值
            prop.DefaultValue = ParseDefaultValue(p.Type, p.DefaultValue);

            // 类型特有字段
            switch (p.Type)
            {
                case ActionTemplateSO.PropertyTypeEntry.Float:
                case ActionTemplateSO.PropertyTypeEntry.Int:
                    if (p.UseRange)
                    {
                        prop.Min = p.Min;
                        prop.Max = p.Max;
                    }
                    break;

                case ActionTemplateSO.PropertyTypeEntry.Enum:
                    prop.EnumOptions = ParseEnumOptions(p.EnumOptions);
                    prop.EnumDisplayOptions = ParseEnumOptions(p.EnumDisplayOptions);
                    // 枚举默认值：未指定时用第一个选项
                    if (string.IsNullOrEmpty(p.DefaultValue) && prop.EnumOptions is { Length: > 0 })
                        prop.DefaultValue = prop.EnumOptions[0];
                    break;

                case ActionTemplateSO.PropertyTypeEntry.SceneBinding:
                    prop.SceneBindingType = ConvertBindingType(p.BindingType);
                    prop.BindingRequirement = bindingRequirement;
                    break;

                case ActionTemplateSO.PropertyTypeEntry.AssetRef:
                    prop.AssetFilterTypeName = string.IsNullOrEmpty(p.AssetFilterTypeName)
                        ? null : p.AssetFilterTypeName;
                    break;

                case ActionTemplateSO.PropertyTypeEntry.StructList:
                    prop.StructFields = p.StructFields
                        .Where(field => !string.IsNullOrWhiteSpace(field.Key))
                        .Select(field => ConvertProperty(field, bindingRequirement: null))
                        .ToArray();
                    prop.SummaryFormat = string.IsNullOrWhiteSpace(p.SummaryFormat)
                        ? null
                        : p.SummaryFormat;
                    break;
            }

            return prop;
        }

        private static OutputVariableDefinition ConvertOutputVariable(ActionTemplateSO.OutputVariableEntry entry)
        {
            return new OutputVariableDefinition
            {
                Name = entry.Name?.Trim() ?? string.Empty,
                DisplayName = entry.DisplayName?.Trim() ?? string.Empty,
                Type = string.IsNullOrWhiteSpace(entry.Type) ? "String" : entry.Type.Trim(),
                Scope = string.IsNullOrWhiteSpace(entry.Scope) ? "Global" : entry.Scope.Trim(),
            };
        }

        private static Dictionary<string, MarkerRequirement> BuildRequirementMap(ActionTemplateSO template)
        {
            var result = new Dictionary<string, MarkerRequirement>();
            foreach (var requirement in template.SceneRequirements)
            {
                if (string.IsNullOrWhiteSpace(requirement.BindingKey))
                {
                    continue;
                }

                result[requirement.BindingKey] = ConvertRequirement(requirement);
            }

            return result;
        }

        private static PropertyType ConvertPropertyType(ActionTemplateSO.PropertyTypeEntry entry)
        {
            return entry switch
            {
                ActionTemplateSO.PropertyTypeEntry.Float => PropertyType.Float,
                ActionTemplateSO.PropertyTypeEntry.Int => PropertyType.Int,
                ActionTemplateSO.PropertyTypeEntry.Bool => PropertyType.Bool,
                ActionTemplateSO.PropertyTypeEntry.String => PropertyType.String,
                ActionTemplateSO.PropertyTypeEntry.Enum => PropertyType.Enum,
                ActionTemplateSO.PropertyTypeEntry.AssetRef => PropertyType.AssetRef,
                ActionTemplateSO.PropertyTypeEntry.Vector2 => PropertyType.Vector2,
                ActionTemplateSO.PropertyTypeEntry.Vector3 => PropertyType.Vector3,
                ActionTemplateSO.PropertyTypeEntry.Color => PropertyType.Color,
                ActionTemplateSO.PropertyTypeEntry.Tag => PropertyType.Tag,
                ActionTemplateSO.PropertyTypeEntry.SceneBinding => PropertyType.SceneBinding,
                ActionTemplateSO.PropertyTypeEntry.StructList => PropertyType.StructList,
                ActionTemplateSO.PropertyTypeEntry.VariableSelector => PropertyType.VariableSelector,
                ActionTemplateSO.PropertyTypeEntry.SignalTagSelector => PropertyType.SignalTagSelector,
                ActionTemplateSO.PropertyTypeEntry.EntityRefSelector => PropertyType.EntityRefSelector,
                ActionTemplateSO.PropertyTypeEntry.ConditionParams => PropertyType.ConditionParams,
                _ => PropertyType.String
            };
        }

        private static BindingType ConvertBindingType(ActionTemplateSO.BindingTypeEntry entry)
        {
            return entry switch
            {
                ActionTemplateSO.BindingTypeEntry.Transform => BindingType.Transform,
                ActionTemplateSO.BindingTypeEntry.Area => BindingType.Area,
                ActionTemplateSO.BindingTypeEntry.Path => BindingType.Path,
                ActionTemplateSO.BindingTypeEntry.Collider => BindingType.Collider,
                _ => BindingType.Transform
            };
        }

        private static object? ParseDefaultValue(ActionTemplateSO.PropertyTypeEntry type, string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return type switch
                {
                    ActionTemplateSO.PropertyTypeEntry.StructList => "[]",
                    ActionTemplateSO.PropertyTypeEntry.VariableSelector => -1,
                    _ => null
                };
            }

            return type switch
            {
                ActionTemplateSO.PropertyTypeEntry.Float
                    => float.TryParse(raw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f,

                ActionTemplateSO.PropertyTypeEntry.Int
                    => int.TryParse(raw, out var i) ? i : 0,

                ActionTemplateSO.PropertyTypeEntry.Bool
                    => raw.Trim().ToLowerInvariant() is "true" or "1",

                ActionTemplateSO.PropertyTypeEntry.String
                    => raw,

                ActionTemplateSO.PropertyTypeEntry.Enum
                    => raw,

                ActionTemplateSO.PropertyTypeEntry.StructList
                    => raw,

                ActionTemplateSO.PropertyTypeEntry.VariableSelector
                    => int.TryParse(raw, out var variableIndex) ? variableIndex : -1,

                ActionTemplateSO.PropertyTypeEntry.SignalTagSelector
                    => raw,

                ActionTemplateSO.PropertyTypeEntry.EntityRefSelector
                    => raw,

                ActionTemplateSO.PropertyTypeEntry.ConditionParams
                    => raw,

                _ => null
            };
        }

        private static string[]? ParseEnumOptions(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return raw.Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
        }

        private static MarkerRequirement ConvertRequirement(ActionTemplateSO.SceneRequirementEntry r)
        {
            return new MarkerRequirement(
                bindingKey: r.BindingKey,
                markerTypeId: r.MarkerTypeId,
                required: r.Required,
                allowMultiple: r.AllowMultiple,
                minCount: r.MinCount,
                displayName: r.DisplayName,
                defaultTag: r.DefaultTag);
        }
    }
}
