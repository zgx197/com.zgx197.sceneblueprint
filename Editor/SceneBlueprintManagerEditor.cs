#nullable enable
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// SceneBlueprintManager 的自定义 Inspector（只读展示）。
    /// 
    /// Manager 由蓝图编辑器的"同步到场景"功能自动维护，
    /// 策划无需直接操作此 Inspector。此 Editor 仅用于查看数据状态。
    /// </summary>
    [CustomEditor(typeof(SceneBlueprintManager))]
    public class SceneBlueprintManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var manager = (SceneBlueprintManager)target;

            // ── 提示信息 ──
            EditorGUILayout.HelpBox(
                "此组件由蓝图编辑器自动管理。\n请使用蓝图编辑器（Alt+B）进行编辑。",
                MessageType.Info);

            EditorGUILayout.Space(4);

            // ── 蓝图资产 ──
            EditorGUILayout.LabelField("蓝图资产", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("BlueprintAsset", manager.BlueprintAsset,
                typeof(BlueprintAsset), false);
            EditorGUI.EndDisabledGroup();

            // ── 绑定分组 ──
            if (manager.BindingGroups.Count > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(
                    $"绑定分组 ({manager.BindingGroups.Count} 个子蓝图)",
                    EditorStyles.boldLabel);

                foreach (var group in manager.BindingGroups)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"\U0001F4E6 {group.SubGraphTitle}",
                        EditorStyles.miniLabel);

                    EditorGUI.indentLevel++;
                    foreach (var binding in group.Bindings)
                    {
                        string status = binding.IsBound ? "\u2705" : "\u26A0\uFE0F";
                        string objName = binding.BoundObject != null
                            ? binding.BoundObject.name : "(未绑定)";
                        EditorGUILayout.LabelField(
                            $"  {status} {binding.DisplayName} ({binding.BindingType})",
                            objName);
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.EndVertical();
                }
            }

            // ── 打开编辑器按钮 ──
            EditorGUILayout.Space(8);
            if (GUILayout.Button("在蓝图编辑器中打开", GUILayout.Height(28)))
            {
                if (manager.BlueprintAsset != null && !manager.BlueprintAsset.IsEmpty)
                {
                    var window = EditorWindow.GetWindow<SceneBlueprintWindow>();
                    window.LoadFromAsset(manager.BlueprintAsset);
                }
                else
                {
                    EditorWindow.GetWindow<SceneBlueprintWindow>();
                }
            }
        }
    }
}
