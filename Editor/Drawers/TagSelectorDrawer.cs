#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Drawers
{
    /// <summary>
    /// Tag 选择器绘制工具——按维度渲染结构化的 Tag 选择 UI。
    /// <para>
    /// exclusive 维度 → Popup 单选下拉（含 "(无)" 选项）。
    /// multiple 维度 → Toggle 多选复选框。
    /// 无维度定义时降级为纯文本编辑。
    /// </para>
    /// </summary>
    public static class TagSelectorDrawer
    {
        /// <summary>
        /// 绘制 Tag 选择器。当前选中的 Tag 以逗号分隔字符串存储。
        /// </summary>
        /// <param name="label">字段显示名</param>
        /// <param name="currentValue">当前值（逗号分隔的 Tag 路径，如 "CombatRole.Frontline,Behavior.Patrol"）</param>
        /// <param name="registry">维度注册表（为 null 时降级为 TextField）</param>
        /// <returns>修改后的值；未修改时返回原值</returns>
        public static string Draw(string label, string currentValue, ITagDimensionRegistry? registry)
        {
            if (registry == null || registry.AllDimensions.Count == 0)
            {
                // 降级：无维度定义，纯文本编辑
                return EditorGUILayout.TextField(label, currentValue ?? "");
            }

            var currentTags = ParseTags(currentValue);
            bool changed = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            foreach (var dim in registry.AllDimensions)
            {
                if (dim.Values == null || dim.Values.Length == 0) continue;

                if (dim.IsExclusive)
                {
                    if (DrawExclusiveDimension(dim, currentTags))
                        changed = true;
                }
                else
                {
                    if (DrawMultipleDimension(dim, currentTags))
                        changed = true;
                }
            }

            EditorGUILayout.EndVertical();

            if (changed)
                return JoinTags(currentTags);

            return currentValue ?? "";
        }

        /// <summary>
        /// 绘制 SignalTag 选择器——从已注册的信号标签列表中选择。
        /// </summary>
        /// <param name="label">字段显示名</param>
        /// <param name="currentValue">当前信号标签路径</param>
        /// <param name="signalTags">所有可选的信号标签路径列表</param>
        /// <returns>修改后的值</returns>
        public static string DrawSignalTagSelector(string label, string currentValue, string[]? signalTags)
        {
            if (signalTags == null || signalTags.Length == 0)
            {
                // 无已注册信号标签，降级为文本输入
                return EditorGUILayout.TextField(label, currentValue ?? "");
            }

            // 构建显示选项：第一项为 "(无)"
            var displayOptions = new string[signalTags.Length + 1];
            displayOptions[0] = "(无)";
            for (int i = 0; i < signalTags.Length; i++)
                displayOptions[i + 1] = signalTags[i];

            // 查找当前选中索引
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(currentValue))
            {
                for (int i = 0; i < signalTags.Length; i++)
                {
                    if (string.Equals(signalTags[i], currentValue, StringComparison.Ordinal))
                    {
                        selectedIndex = i + 1;
                        break;
                    }
                }
            }

            int newIndex = EditorGUILayout.Popup(label, selectedIndex, displayOptions);

            if (newIndex != selectedIndex)
                return newIndex == 0 ? "" : signalTags[newIndex - 1];

            return currentValue ?? "";
        }

        // ── exclusive 维度：Popup 单选 ──

        private static bool DrawExclusiveDimension(TagDimensionDef dim, HashSet<string> tags)
        {
            // 构建选项：[(无), Value1, Value2, ...]
            var options = new string[dim.Values.Length + 1];
            options[0] = "(无)";
            int currentIndex = 0;

            for (int i = 0; i < dim.Values.Length; i++)
            {
                var val = dim.Values[i];
                options[i + 1] = !string.IsNullOrEmpty(val.Label) ? val.Label : val.Name;

                if (tags.Contains(val.FullPath))
                    currentIndex = i + 1;
            }

            string dimLabel = !string.IsNullOrEmpty(dim.DisplayName) ? dim.DisplayName : dim.Id;
            int newIndex = EditorGUILayout.Popup(dimLabel, currentIndex, options);

            if (newIndex == currentIndex) return false;

            // 移除该维度所有旧值
            foreach (var val in dim.Values)
                tags.Remove(val.FullPath);

            // 添加新值
            if (newIndex > 0)
                tags.Add(dim.Values[newIndex - 1].FullPath);

            return true;
        }

        // ── multiple 维度：Toggle 多选 ──

        private static bool DrawMultipleDimension(TagDimensionDef dim, HashSet<string> tags)
        {
            string dimLabel = !string.IsNullOrEmpty(dim.DisplayName) ? dim.DisplayName : dim.Id;
            EditorGUILayout.LabelField(dimLabel, EditorStyles.miniLabel);

            bool changed = false;
            EditorGUI.indentLevel++;

            foreach (var val in dim.Values)
            {
                bool wasOn = tags.Contains(val.FullPath);
                string valLabel = !string.IsNullOrEmpty(val.Label) ? val.Label : val.Name;
                bool isOn = EditorGUILayout.Toggle(valLabel, wasOn);

                if (isOn != wasOn)
                {
                    if (isOn) tags.Add(val.FullPath);
                    else      tags.Remove(val.FullPath);
                    changed = true;
                }
            }

            EditorGUI.indentLevel--;
            return changed;
        }

        // ── 工具方法 ──

        private static HashSet<string> ParseTags(string? value)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(value)) return set;

            foreach (var part in value.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0)
                    set.Add(trimmed);
            }
            return set;
        }

        private static string JoinTags(HashSet<string> tags)
        {
            if (tags.Count == 0) return "";
            return string.Join(",", tags);
        }
    }
}
