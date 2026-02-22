#nullable enable
using UnityEngine;
using UnityEditor;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 简易文本输入对话框。用于让策划输入子蓝图名称等短文本。
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string _value = "";
        private string _message = "";
        private bool _confirmed;
        private bool _firstFrame = true;

        private static string? _result;

        /// <summary>
        /// 显示输入对话框，返回输入的文本。用户取消时返回 null。
        /// </summary>
        public static string? Show(string title, string message, string defaultValue = "")
        {
            _result = null;

            var dialog = CreateInstance<EditorInputDialog>();
            dialog.titleContent = new GUIContent(title);
            dialog._message = message;
            dialog._value = defaultValue;
            dialog._confirmed = false;

            // 居中显示
            dialog.position = new Rect(
                (Screen.currentResolution.width - 300) / 2f,
                (Screen.currentResolution.height - 120) / 2f,
                300, 120);
            dialog.ShowModalUtility();

            return _result;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(_message);
            EditorGUILayout.Space(4);

            GUI.SetNextControlName("InputField");
            _value = EditorGUILayout.TextField(_value);

            if (_firstFrame)
            {
                EditorGUI.FocusTextInControl("InputField");
                _firstFrame = false;
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("确定", GUILayout.Width(80)))
            {
                _confirmed = true;
                _result = _value;
                Close();
            }

            if (GUILayout.Button("取消", GUILayout.Width(80)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();

            // 回车确认
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                _confirmed = true;
                _result = _value;
                Close();
                Event.current.Use();
            }

            // ESC 取消
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
            }
        }
    }
}
