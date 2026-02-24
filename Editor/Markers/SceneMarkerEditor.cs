#nullable enable
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Editor.Markers.Extensions;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// SceneMarker 通用 Inspector 编辑器。
    /// <para>
    /// 职责：
    /// - 绘制 Marker 默认属性（使用 DrawDefaultInspector）
    /// - 查询并绘制业务层注册的编辑器扩展工具
    /// - 保持框架层通用性，不包含业务特定逻辑
    /// </para>
    /// <para>
    /// 扩展机制：
    /// 业务层通过实现 IMarkerEditorExtension 并标注 [MarkerEditorExtension("MarkerTypeId")]，
    /// 即可为特定 Marker 添加自定义编辑器工具（如位置生成器、路径编辑器等）。
    /// 这些工具会自动显示在 Inspector 的"编辑器工具"区域。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(SceneMarker), true)]
    [CanEditMultipleObjects]
    public class SceneMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 绘制默认属性
            DrawDefaultInspector();

            // 获取当前 Marker
            var marker = (SceneMarker)target;

            // 查询适用于此 Marker 的扩展工具
            var extensions = MarkerEditorExtensionRegistry.GetExtensions(marker.MarkerTypeId);

            if (extensions.Count == 0)
                return; // 无扩展工具，直接返回

            // 绘制扩展工具区域
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("编辑器工具", EditorStyles.boldLabel);
            
            EditorGUI.indentLevel++;

            foreach (var extension in extensions)
            {
                // 检查工具是否适用
                if (!extension.IsApplicable(marker))
                    continue;

                try
                {
                    // 绘制扩展工具 UI
                    extension.DrawInspectorGUI(marker);
                }
                catch (System.Exception e)
                {
                    EditorGUILayout.HelpBox(
                        $"编辑器工具 \"{extension.DisplayName}\" 绘制失败：{e.Message}",
                        MessageType.Error);
                    UnityEngine.Debug.LogError(
                        $"[SceneMarkerEditor] 扩展工具 {extension.GetType().FullName} 绘制异常：\n{e}");
                }
            }

            EditorGUI.indentLevel--;
        }
    }
}
