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

            // 属性检查
            var propKeys = new HashSet<string>();
            foreach (var prop in _template.Properties)
            {
                if (string.IsNullOrWhiteSpace(prop.Key))
                {
                    _errors.Add("存在空属性 Key");
                }
                else if (!propKeys.Add(prop.Key))
                {
                    _errors.Add($"属性 Key '{prop.Key}' 重复");
                }

                // 枚举选项检查
                if (prop.Type == ActionTemplateSO.PropertyTypeEntry.Enum
                    && string.IsNullOrWhiteSpace(prop.EnumOptions))
                {
                    _warnings.Add($"属性 '{prop.Key}' 类型为 Enum 但未填写选项");
                }
            }

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
            }

            // 属性数量警告
            if (_template.Properties.Count > 15)
                _warnings.Add($"属性过多（{_template.Properties.Count} 个），建议拆分");
        }

        private void DrawValidationMessages()
        {
            foreach (var error in _errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            foreach (var warning in _warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            if (_errors.Count == 0 && _warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("验证通过 ✅", MessageType.Info);
            }
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
                    EditorGUILayout.LabelField($"  ← {port.Id} ({port.DisplayName}) [{typeLabel}]");
                }

                // 属性
                if (_template.Properties.Count > 0)
                {
                    EditorGUILayout.LabelField("属性", EditorStyles.boldLabel);
                    foreach (var prop in _template.Properties.OrderBy(p => p.Order))
                    {
                        if (string.IsNullOrEmpty(prop.Key)) continue;
                        var defaultStr = string.IsNullOrEmpty(prop.DefaultValue) ? "" : $" = {prop.DefaultValue}";
                        EditorGUILayout.LabelField($"  {prop.DisplayName} ({prop.Type}){defaultStr}");
                    }
                }

                // 场景需求
                if (_template.SceneRequirements.Count > 0)
                {
                    EditorGUILayout.LabelField("场景需求", EditorStyles.boldLabel);
                    foreach (var req in _template.SceneRequirements)
                    {
                        if (string.IsNullOrEmpty(req.BindingKey)) continue;
                        var reqStr = req.Required ? "必需" : "可选";
                        EditorGUILayout.LabelField($"  {req.DisplayName} [{req.MarkerTypeId}] ({reqStr})");
                    }
                }
            }

            EditorGUI.indentLevel--;
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
    }
}
