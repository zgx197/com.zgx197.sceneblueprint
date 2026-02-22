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

            // ── 属性 ──
            def.Properties = template.Properties
                .Where(p => !string.IsNullOrEmpty(p.Key))
                .Select(ConvertProperty)
                .ToArray();

            // ── 场景需求 ──
            def.SceneRequirements = template.SceneRequirements
                .Where(r => !string.IsNullOrEmpty(r.BindingKey))
                .Select(ConvertRequirement)
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
            return p.PortType switch
            {
                ActionTemplateSO.PortTypeEntry.EventOut => Port.Event(p.Id, p.DisplayName),
                _ => Port.Out(p.Id, p.DisplayName)
            };
        }

        private static PropertyDefinition ConvertProperty(ActionTemplateSO.PropertyEntry p)
        {
            var prop = new PropertyDefinition
            {
                Key = p.Key,
                DisplayName = p.DisplayName,
                Type = ConvertPropertyType(p.Type),
                Tooltip = string.IsNullOrEmpty(p.Tooltip) ? null : p.Tooltip,
                Category = string.IsNullOrEmpty(p.Category) ? null : p.Category,
                Order = p.Order,
                VisibleWhen = string.IsNullOrEmpty(p.VisibleWhen) ? null : p.VisibleWhen,
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
                    // 枚举默认值：未指定时用第一个选项
                    if (string.IsNullOrEmpty(p.DefaultValue) && prop.EnumOptions is { Length: > 0 })
                        prop.DefaultValue = prop.EnumOptions[0];
                    break;

                case ActionTemplateSO.PropertyTypeEntry.SceneBinding:
                    prop.SceneBindingType = ConvertBindingType(p.BindingType);
                    break;

                case ActionTemplateSO.PropertyTypeEntry.AssetRef:
                    prop.AssetFilterTypeName = string.IsNullOrEmpty(p.AssetFilterTypeName)
                        ? null : p.AssetFilterTypeName;
                    break;
            }

            return prop;
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
            if (string.IsNullOrEmpty(raw)) return null;

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
