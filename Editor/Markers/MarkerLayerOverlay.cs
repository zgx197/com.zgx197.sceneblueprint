#nullable enable
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// Scene View 标记图层切换面板（Overlay）。
    /// <para>
    /// 在 Scene View 工具栏中显示图层开关，设计师可按需切换各标记图层的可见性。
    /// 使用 Unity 2021.2+ 的 Overlay API（<see cref="IMGUIOverlay"/>）。
    /// </para>
    /// <para>
    /// 图层列表由 <see cref="MarkerLayerSystem.Layers"/> 驱动，
    /// 可见性状态存储在 <see cref="MarkerLayerSystem"/> 中。
    /// </para>
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "标记图层")]
    [Icon("d_FilterByType")]
    public class MarkerLayerOverlay : IMGUIOverlay
    {
        private const string OverlayId = "scene-blueprint-marker-layers";

        public override void OnGUI()
        {
            // 面板宽度
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(220));

            // 标题
            EditorGUILayout.LabelField("标记图层", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // 各图层切换
            foreach (var layer in MarkerLayerSystem.Layers)
            {
                bool current = MarkerLayerSystem.IsLayerVisible(layer.Id);
                var label = new GUIContent($" {layer.Emoji} {layer.DisplayName}");

                // 用图层颜色绘制标签
                var prevColor = GUI.contentColor;
                GUI.contentColor = current ? layer.Color : Color.gray;

                bool next = EditorGUILayout.ToggleLeft(label, current);
                GUI.contentColor = prevColor;

                if (next != current)
                {
                    MarkerLayerSystem.SetLayerVisible(layer.Id, next);
                }
            }

            EditorGUILayout.Space(4);

            string currentFilter = MarkerLayerSystem.TagFilterExpression;
            string nextFilter = EditorGUILayout.TextField(
                new GUIContent("Tag过滤", "支持逗号/分号/竖线分隔。示例：Combat.*.Elite, Trigger"),
                currentFilter);

            if (!string.Equals(currentFilter, nextFilter, StringComparison.Ordinal))
                MarkerLayerSystem.SetTagFilterExpression(nextFilter);

            if (MarkerLayerSystem.HasTagFilter)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("清空Tag过滤", EditorStyles.miniButton, GUILayout.Width(86)))
                    MarkerLayerSystem.ClearTagFilterExpression();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);

            // 全部显示 / 全部隐藏
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全部显示", EditorStyles.miniButtonLeft))
            {
                MarkerLayerSystem.ShowAll();
            }
            if (GUILayout.Button("全部隐藏", EditorStyles.miniButtonRight))
            {
                MarkerLayerSystem.HideAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
