#nullable enable
using SceneBlueprint.Runtime.Templates;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// BlueprintTemplateSO 自定义 Inspector。
    /// <para>
    /// 在默认 Inspector 基础上增加：
    /// <list type="bullet">
    ///   <item>GraphJson 有效性检查</item>
    ///   <item>绑定需求汇总预览</item>
    ///   <item>统计信息展示</item>
    /// </list>
    /// </para>
    /// </summary>
    [CustomEditor(typeof(BlueprintTemplateSO))]
    public class BlueprintTemplateSOEditor : UnityEditor.Editor
    {
        private BlueprintTemplateSO _template = null!;

        private void OnEnable()
        {
            _template = (BlueprintTemplateSO)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            // 验证
            if (string.IsNullOrEmpty(_template.DisplayName))
                EditorGUILayout.HelpBox("DisplayName 不能为空", MessageType.Error);

            if (!_template.HasValidGraph)
                EditorGUILayout.HelpBox("GraphJson 为空，模板无法使用。请通过蓝图编辑器中的「保存为模板」功能创建。", MessageType.Warning);
            else
                EditorGUILayout.HelpBox($"模板数据有效 ✅  GraphJson: {_template.GraphJson.Length} 字符", MessageType.Info);

            // 统计预览
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("模板概览", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("节点数", _template.NodeCount.ToString());
                EditorGUILayout.LabelField("Action 类型", string.IsNullOrEmpty(_template.ActionTypesSummary)
                    ? "(无)" : _template.ActionTypesSummary);
                EditorGUILayout.LabelField("绑定需求", _template.BindingRequirements.Count.ToString());
                EditorGUILayout.LabelField("创建日期", string.IsNullOrEmpty(_template.CreatedDate)
                    ? "(未知)" : _template.CreatedDate);

                if (_template.BindingRequirements.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("绑定需求明细", EditorStyles.boldLabel);
                    foreach (var req in _template.BindingRequirements)
                    {
                        EditorGUILayout.LabelField(
                            $"  {req.Description}",
                            $"[{req.MarkerTypeId}] key={req.BindingKey}");
                    }
                }
            }
        }
    }
}
