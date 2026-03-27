#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Editor.Markers.Extensions;

namespace SceneBlueprint.Editor.Markers.ToolKit
{
    /// <summary>
    /// AreaMarker 自定义 Inspector 编辑器。
    /// <para>
    /// 职责：
    /// - 在默认属性之后追加"多边形洞管理面板"（仅 Polygon 模式显示）
    /// - 洞的新增（默认矩形）/ 删除操作
    /// - 查询并绘制业务层注册的 IMarkerEditorExtension 扩展工具
    /// </para>
    /// <para>
    /// 注意：本类的 [CustomEditor(typeof(AreaMarker), true)] 会覆盖
    /// SceneMarkerEditor 对 AreaMarker 及其子类的 Inspector 绘制，
    /// 因此必须在此处也加载扩展工具（与 SceneMarkerEditor 逻辑一致）。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(AreaMarker), true)]
    public class AreaMarkerEditor : UnityEditor.Editor
    {
        // ── 洞管理面板状态 ──
        private bool _holeFoldout;

        private AreaMarker Target => (AreaMarker)target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // ── 洞管理面板（仅 Polygon 模式显示）──
            if (Target.Shape == AreaShape.Polygon)
            {
                EditorGUILayout.Space(4);
                DrawHoleManagementPanel();
            }

            // ── 业务层扩展工具（IMarkerEditorExtension）──
            DrawEditorExtensions();
        }

        /// <summary>
        /// 洞管理面板：列出所有洞的概览 + 新增/删除按钮。
        /// <para>
        /// 每个洞显示顶点数和删除按钮；底部有"+ 新增洞"按钮，
        /// 在外轮廓重心处生成一个默认 2m×2m 矩形洞。
        /// </para>
        /// </summary>
        private void DrawHoleManagementPanel()
        {
            _holeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_holeFoldout, $"多边形洞 ({Target.Holes.Count})");
            if (_holeFoldout)
            {
                EditorGUI.indentLevel++;

                // 洞列表
                for (int i = 0; i < Target.Holes.Count; i++)
                {
                    var hole = Target.Holes[i];
                    int vertCount = hole.Vertices?.Count ?? 0;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"洞 {i}", $"{vertCount} 个顶点", GUILayout.ExpandWidth(true));

                    var defaultBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("删除", GUILayout.Width(48)))
                    {
                        Undo.RecordObject(Target, "删除洞");
                        Target.Holes.RemoveAt(i);
                        Target.IncrementGeometryVersionEditor();
                        EditorUtility.SetDirty(Target);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    GUI.backgroundColor = defaultBg;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);

                // 新增洞按钮——在外轮廓重心处生成一个默认小矩形洞
                if (GUILayout.Button("+ 新增洞（默认矩形）", GUILayout.Height(24)))
                {
                    Undo.RecordObject(Target, "新增洞");
                    var newHole = new PolygonHole();
                    var center = ComputeVerticesCentroid(Target.Vertices);
                    float size = 1f;
                    newHole.Vertices.Add(center + new Vector3(-size, 0, -size));
                    newHole.Vertices.Add(center + new Vector3( size, 0, -size));
                    newHole.Vertices.Add(center + new Vector3( size, 0,  size));
                    newHole.Vertices.Add(center + new Vector3(-size, 0,  size));
                    Target.Holes.Add(newHole);
                    Target.IncrementGeometryVersionEditor();
                    EditorUtility.SetDirty(Target);
                    SceneView.RepaintAll();
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>计算顶点列表的重心（局部坐标）</summary>
        private static Vector3 ComputeVerticesCentroid(List<Vector3> verts)
        {
            if (verts == null || verts.Count == 0) return Vector3.zero;
            var sum = Vector3.zero;
            foreach (var v in verts) sum += v;
            return sum / verts.Count;
        }

        /// <summary>
        /// 查询并绘制业务层注册的 IMarkerEditorExtension 扩展工具。
        /// <para>
        /// 逻辑与 SceneMarkerEditor 一致：通过 MarkerEditorExtensionRegistry
        /// 按 MarkerTypeId 查询适用的扩展工具，逐一调用 DrawInspectorGUI。
        /// </para>
        /// </summary>
        private void DrawEditorExtensions()
        {
            var marker = Target;
            var extensions = MarkerEditorExtensionRegistry.GetExtensions(marker.MarkerTypeId);
            if (extensions.Count == 0) return;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("编辑器工具", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            foreach (var extension in extensions)
            {
                if (!extension.IsApplicable(marker)) continue;
                try
                {
                    extension.DrawInspectorGUI(marker);
                }
                catch (System.Exception e)
                {
                    EditorGUILayout.HelpBox(
                        $"编辑器工具 \"{extension.DisplayName}\" 绘制失败：{e.Message}",
                        MessageType.Error);
                    UnityEngine.Debug.LogError(
                        $"[AreaMarkerEditor] 扩展工具 {extension.GetType().FullName} 绘制异常：\n{e}");
                }
            }
            EditorGUI.indentLevel--;
        }
    }
}
