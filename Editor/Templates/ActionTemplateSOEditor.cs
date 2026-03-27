#nullable enable
using System.Collections.Generic;
using System.Linq;
using SceneBlueprint.Core;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// ActionTemplateSO 自定义 Inspector。
    /// <para>
    /// 在默认 Inspector 基础上增加：
    /// <list type="bullet">
    ///   <item>实时验证（TypeId 唯一性、必填项检查、格式校验）</item>
    ///   <item>节点外观预览（颜色、端口、属性列表概览）</item>
    ///   <item>与 C# 定义冲突提示</item>
    /// </list>
    /// </para>
    /// </summary>
    [CustomEditor(typeof(ActionTemplateSO))]
    public class ActionTemplateSOEditor : UnityEditor.Editor
    {
        private ActionTemplateSO _template = null!;
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();
        private bool _showPreview = true;

        private void OnEnable()
        {
            _template = (ActionTemplateSO)target;
        }

        public override void OnInspectorGUI()
        {
            // 绘制默认 Inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            // 验证
            Validate();

            // 显示验证结果
            DrawValidationMessages();

            // 预览区域
            DrawPreview();
        }

        // ═══════════════════════════════════════════════════════════
        //  验证逻辑
        // ═══════════════════════════════════════════════════════════

        private void Validate()
        {
            _errors.Clear();
            _warnings.Clear();

            // TypeId 检查
            if (string.IsNullOrWhiteSpace(_template.TypeId))
            {
                _errors.Add("TypeId 不能为空");
            }
            else
            {
                if (!_template.TypeId.Contains('.'))
                    _warnings.Add("建议使用 'Category.Name' 格式（如 'Cinematic.PlayTimeline'）");

                // 检查与 C# 定义的冲突
                if (HasCSharpConflict(_template.TypeId))
                    _errors.Add($"TypeId '{_template.TypeId}' 与 C# 定义冲突，运行时将被忽略");

                // 检查与其他 SO 模板的重复
                if (HasDuplicateTemplate(_template.TypeId, _template))
                    _errors.Add($"TypeId '{_template.TypeId}' 与其他 ActionTemplateSO 重复");
            }

            // DisplayName 检查
            if (string.IsNullOrWhiteSpace(_template.DisplayName))
                _errors.Add("DisplayName 不能为空");

            // Category 检查
            if (string.IsNullOrWhiteSpace(_template.Category))
                _warnings.Add("Category 为空，节点将出现在未分类中");

            // 端口检查
            var portIds = new HashSet<string>();
            foreach (var port in _template.OutputPorts)
            {
                if (string.IsNullOrWhiteSpace(port.Id))
                {
                    _errors.Add("存在空端口 ID");
                }
                else if (!portIds.Add(port.Id))
                {
                    _errors.Add($"端口 ID '{port.Id}' 重复");
                }
            }

            ValidateOutputVariables(_template.OutputVariables, _errors, _warnings);

            var sceneBindingPropertyKeys = new HashSet<string>();
            ValidateProperties(_template.Properties, "属性", _errors, _warnings, sceneBindingPropertyKeys);

            // 场景需求检查
            var bindingKeys = new HashSet<string>();
            foreach (var req in _template.SceneRequirements)
            {
                if (string.IsNullOrWhiteSpace(req.BindingKey))
                {
                    _errors.Add("存在空 BindingKey");
                }
                else if (!bindingKeys.Add(req.BindingKey))
                {
                    _errors.Add($"BindingKey '{req.BindingKey}' 重复");
                }
                else if (!sceneBindingPropertyKeys.Contains(req.BindingKey))
                {
                    _errors.Add($"场景需求 '{req.BindingKey}' 未找到对应的 SceneBinding 属性，无法进入正式 definition 声明");
                }
            }

            foreach (var bindingKey in sceneBindingPropertyKeys)
            {
                if (!bindingKeys.Contains(bindingKey))
                {
                    _warnings.Add($"SceneBinding 属性 '{bindingKey}' 未声明场景需求，将缺少 marker requirement 元数据");
                }
            }

            // 属性数量警告
            if (_template.Properties.Count > 15)
                _warnings.Add($"属性过多（{_template.Properties.Count} 个），建议拆分");

            var definition = ActionTemplateConverter.Convert(_template);
            var declarationValidation = ActionDefinitionValidationSupport.EvaluateDeclarationResult(definition);
            ActionDefinitionValidationGui.AppendMessages(
                declarationValidation,
                _errors,
                _warnings);
        }

        private void DrawValidationMessages()
        {
            ActionDefinitionValidationGui.DrawMessages(
                _errors,
                _warnings,
                "验证通过 ✅",
                blockingSummary: $"错误 {_errors.Count} · 警告 {_warnings.Count} · 模板当前不可注册",
                warningSummary: $"错误 0 · 警告 {_warnings.Count} · 模板当前可注册");
        }

        // ═══════════════════════════════════════════════════════════
        //  节点预览
        // ═══════════════════════════════════════════════════════════

        private void DrawPreview()
        {
            EditorGUILayout.Space(4);
            _showPreview = EditorGUILayout.Foldout(_showPreview, "节点预览", true);
            if (!_showPreview) return;

            EditorGUI.indentLevel++;

            // 基本信息
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var definition = ActionTemplateConverter.Convert(_template);

                // 标题行（模拟节点头部）
                var headerRect = EditorGUILayout.GetControlRect(false, 24);
                EditorGUI.DrawRect(headerRect, _template.ThemeColor);
                var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter };
                EditorGUI.LabelField(headerRect, _template.DisplayName, style);

                // 端口
                EditorGUILayout.LabelField("端口", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("  → in (激活)");
                foreach (var port in _template.OutputPorts)
                {
                    if (string.IsNullOrEmpty(port.Id)) continue;
                    var typeLabel = port.PortType == ActionTemplateSO.PortTypeEntry.EventOut ? "Event" : "Flow";
                    var graphRoleLabel = port.GraphRole == PortGraphRole.None
                        ? string.Empty
                        : $" · {port.GraphRole}";
                    EditorGUILayout.LabelField($"  ← {port.Id} ({port.DisplayName}) [{typeLabel}{graphRoleLabel}]");
                }

                // 属性
                if (_template.Properties.Count > 0)
                {
                    EditorGUILayout.LabelField("属性", EditorStyles.boldLabel);
                    DrawDefinitionSectionPreview(definition);
                }

                if (ActionDefinitionDeclarationGui.Draw(
                        definition,
                        propertyBag: null,
                        showConfigurationStatus: false))
                {
                    EditorGUILayout.Space(4);
                }
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawDefinitionSectionPreview(ActionDefinition definition)
        {
            var sections = ActionDefinitionSectionLayoutBuilder.BuildVisibleSections(definition);
            for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                var section = sections[sectionIndex];
                if (sections.Count > 1 || !section.IsImplicitDefault)
                {
                    EditorGUILayout.LabelField($"  [{section.Title}]", EditorStyles.miniBoldLabel);
                }

                DrawPropertyPreviewEntries(section.NormalProperties, isAdvanced: false);
                DrawPropertyPreviewEntries(section.AdvancedProperties, isAdvanced: true);
            }
        }

        private static void DrawPropertyPreviewEntries(
            IReadOnlyList<PropertyDefinition> properties,
            bool isAdvanced)
        {
            for (var index = 0; index < properties.Count; index++)
            {
                var property = properties[index];
                var defaultStr = property.DefaultValue == null ? string.Empty : $" = {property.DefaultValue}";
                var advancedLabel = isAdvanced ? " [高级]" : string.Empty;
                EditorGUILayout.LabelField($"  {property.DisplayName} ({property.Type}){defaultStr}{advancedLabel}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  辅助方法
        // ═══════════════════════════════════════════════════════════

        /// <summary>检查 TypeId 是否与 C# ActionDefinition 冲突</summary>
        private static bool HasCSharpConflict(string typeId)
        {
            // 创建一个只包含 C# 定义的 Registry 来检查
            var registry = new ActionRegistry();
            registry.AutoDiscover();
            return registry.TryGet(typeId, out _);
        }

        /// <summary>检查是否有其他 ActionTemplateSO 使用相同 TypeId</summary>
        private static bool HasDuplicateTemplate(string typeId, ActionTemplateSO self)
        {
            var guids = AssetDatabase.FindAssets("t:ActionTemplateSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var other = AssetDatabase.LoadAssetAtPath<ActionTemplateSO>(path);
                if (other == null || other == self) continue;
                if (other.TypeId == typeId) return true;
            }
            return false;
        }

        private static void ValidateOutputVariables(
            IList<ActionTemplateSO.OutputVariableEntry> outputVariables,
            ICollection<string> errors,
            ICollection<string> warnings)
        {
            var variableNames = new HashSet<string>();
            for (var index = 0; index < outputVariables.Count; index++)
            {
                var entry = outputVariables[index];
                var label = $"输出变量#{index + 1}";
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    errors.Add($"{label} 的 Name 不能为空");
                    continue;
                }

                if (!variableNames.Add(entry.Name))
                {
                    errors.Add($"输出变量 Name '{entry.Name}' 重复");
                }

                if (string.IsNullOrWhiteSpace(entry.DisplayName))
                {
                    warnings.Add($"输出变量 '{entry.Name}' 未填写 DisplayName，预览与下游提示会回退到 Name");
                }
            }
        }

        private static void ValidateProperties(
            IList<ActionTemplateSO.PropertyEntry> properties,
            string scopeLabel,
            ICollection<string> errors,
            ICollection<string> warnings,
            ISet<string> sceneBindingPropertyKeys)
        {
            var propKeys = new HashSet<string>();
            var sections = new Dictionary<string, (string title, int order)>(System.StringComparer.Ordinal);
            for (var index = 0; index < properties.Count; index++)
            {
                var prop = properties[index];
                var propertyLabel = string.IsNullOrWhiteSpace(prop.Key)
                    ? $"{scopeLabel}#{index + 1}"
                    : $"{scopeLabel} '{prop.Key}'";

                if (string.IsNullOrWhiteSpace(prop.Key))
                {
                    errors.Add($"{scopeLabel}中存在空属性 Key");
                    continue;
                }

                if (!propKeys.Add(prop.Key))
                {
                    errors.Add($"{scopeLabel}中属性 Key '{prop.Key}' 重复");
                }

                if (string.IsNullOrWhiteSpace(prop.SectionKey) && !string.IsNullOrWhiteSpace(prop.SectionTitle))
                {
                    warnings.Add($"{propertyLabel} 只填写了 SectionTitle，未填写 SectionKey；建议总是通过稳定 SectionKey 驱动分组");
                }

                if (!string.IsNullOrWhiteSpace(prop.SectionKey))
                {
                    var normalizedTitle = string.IsNullOrWhiteSpace(prop.SectionTitle)
                        ? prop.SectionKey
                        : prop.SectionTitle;
                    if (sections.TryGetValue(prop.SectionKey, out var existing))
                    {
                        if (!string.Equals(existing.title, normalizedTitle, System.StringComparison.Ordinal))
                        {
                            warnings.Add($"{propertyLabel} 的 SectionTitle 与同组属性不一致；同一 SectionKey 应保持单一标题");
                        }

                        if (existing.order != prop.SectionOrder)
                        {
                            warnings.Add($"{propertyLabel} 的 SectionOrder 与同组属性不一致；同一 SectionKey 应保持单一排序");
                        }
                    }
                    else
                    {
                        sections.Add(prop.SectionKey, (normalizedTitle, prop.SectionOrder));
                    }
                }

                if (prop.Type == ActionTemplateSO.PropertyTypeEntry.Enum)
                {
                    var enumOptions = ParseCommaSeparated(prop.EnumOptions);
                    if (enumOptions.Count == 0)
                    {
                        warnings.Add($"属性 '{prop.Key}' 类型为 Enum 但未填写选项");
                    }

                    var displayOptions = ParseCommaSeparated(prop.EnumDisplayOptions);
                    if (displayOptions.Count > 0 && displayOptions.Count != enumOptions.Count)
                    {
                        warnings.Add($"属性 '{prop.Key}' 的 EnumDisplayOptions 数量与 EnumOptions 不一致，Inspector 将回退到默认文案");
                    }
                }

                if (!string.IsNullOrWhiteSpace(prop.TypeSourceKey))
                {
                    if (prop.Type != ActionTemplateSO.PropertyTypeEntry.String)
                    {
                        warnings.Add($"{propertyLabel} 配置了 TypeSourceKey，但当前类型不是 String");
                    }
                    else if (!properties.Any(candidate => candidate.Key == prop.TypeSourceKey))
                    {
                        errors.Add($"{propertyLabel} 的 TypeSourceKey '{prop.TypeSourceKey}' 未找到对应属性");
                    }
                    else
                    {
                        var source = properties.First(candidate => candidate.Key == prop.TypeSourceKey);
                        if (source.Type != ActionTemplateSO.PropertyTypeEntry.VariableSelector)
                        {
                            errors.Add($"{propertyLabel} 的 TypeSourceKey '{prop.TypeSourceKey}' 必须指向 VariableSelector 属性");
                        }
                    }
                }

                if (prop.Type == ActionTemplateSO.PropertyTypeEntry.SceneBinding)
                {
                    sceneBindingPropertyKeys.Add(prop.Key);
                }

                if (prop.Type == ActionTemplateSO.PropertyTypeEntry.StructList)
                {
                    if (prop.StructFields == null || prop.StructFields.Count == 0)
                    {
                        warnings.Add($"{propertyLabel} 为 StructList，但未声明子字段");
                    }
                    else
                    {
                        ValidateProperties(
                            prop.StructFields,
                            $"{propertyLabel}.StructFields",
                            errors,
                            warnings,
                            sceneBindingPropertyKeys: new HashSet<string>());
                    }
                }
            }
        }

        private static List<string> ParseCommaSeparated(string raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? new List<string>()
                : raw.Split(',')
                    .Select(part => part.Trim())
                    .Where(part => part.Length > 0)
                    .ToList();
        }
    }
}
