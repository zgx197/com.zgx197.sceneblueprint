#nullable enable
using SceneBlueprint.Core;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 轻量 authoring override 共用的 Inspector 渲染辅助。
    /// 把“属性标签 + 摘要卡片 + 提示框”这类重复 UI 写法收成统一口径，
    /// 避免每个小型 override 各自维护一套 miniLabel/helpBox 协议。
    /// </summary>
    public static class ActionInspectorAuthoringGui
    {
        public static GUIContent BuildLabel(PropertyDefinition property)
        {
            return new GUIContent(property.DisplayName, property.Tooltip ?? string.Empty);
        }

        public static void DrawSummaryBlock(
            string headline,
            string? detail = null,
            string? hint = null,
            bool hintIsWarning = false)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!string.IsNullOrWhiteSpace(headline))
                {
                    EditorGUILayout.LabelField(headline, EditorStyles.miniBoldLabel);
                }

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    DrawWrappedMiniLabel(detail);
                }

                if (!string.IsNullOrWhiteSpace(hint))
                {
                    EditorGUILayout.HelpBox(
                        hint,
                        hintIsWarning ? MessageType.Warning : MessageType.Info);
                }
            }
        }

        public static void DrawWrappedMiniLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            EditorGUILayout.LabelField(text, GetWrappedMiniLabelStyle());
        }

        public static GUIStyle GetWrappedMiniLabelStyle()
        {
            return new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };
        }
    }
}
