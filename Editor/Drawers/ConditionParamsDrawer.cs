#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SceneBlueprint.Editor.Drawers
{
    /// <summary>
    /// 条件参数键值对编辑器——Inspector 中以动态增删的键值对列表形式编辑条件参数。
    /// <para>
    /// 序列化格式：<c>"key=value;key=value"</c>，如 "op=&lt;=;threshold=0.3"。
    /// </para>
    /// </summary>
    public static class ConditionParamsDrawer
    {
        /// <summary>
        /// 绘制条件参数编辑器。
        /// </summary>
        /// <param name="label">字段显示名</param>
        /// <param name="currentValue">当前序列化值（分号分隔的键值对）</param>
        /// <returns>修改后的序列化值</returns>
        public static string Draw(string label, string currentValue)
        {
            var pairs = Parse(currentValue);
            bool changed = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            // 绘制已有键值对
            int removeIndex = -1;
            for (int i = 0; i < pairs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var newKey = EditorGUILayout.TextField(pairs[i].Key, GUILayout.Width(100));
                EditorGUILayout.LabelField("=", GUILayout.Width(12));
                var newValue = EditorGUILayout.TextField(pairs[i].Value);

                if (GUILayout.Button("\u2715", GUILayout.Width(20)))
                {
                    removeIndex = i;
                    changed = true;
                }

                EditorGUILayout.EndHorizontal();

                if (newKey != pairs[i].Key || newValue != pairs[i].Value)
                {
                    pairs[i] = new KVPair(newKey, newValue);
                    changed = true;
                }
            }

            if (removeIndex >= 0)
                pairs.RemoveAt(removeIndex);

            // 添加按钮
            if (GUILayout.Button("+ 添加参数", EditorStyles.miniButton))
            {
                pairs.Add(new KVPair("key", "value"));
                changed = true;
            }

            EditorGUILayout.EndVertical();

            if (changed)
                return Serialize(pairs);

            return currentValue ?? "";
        }

        // ── 内部类型 ──

        private struct KVPair
        {
            public string Key;
            public string Value;
            public KVPair(string key, string value) { Key = key; Value = value; }
        }

        // ── 序列化 / 反序列化 ──

        private static List<KVPair> Parse(string? serialized)
        {
            var result = new List<KVPair>();
            if (string.IsNullOrWhiteSpace(serialized)) return result;

            var parts = serialized!.Split(';');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;

                int eqIdx = trimmed.IndexOf('=');
                if (eqIdx >= 0)
                    result.Add(new KVPair(trimmed.Substring(0, eqIdx), trimmed.Substring(eqIdx + 1)));
                else
                    result.Add(new KVPair(trimmed, ""));
            }
            return result;
        }

        private static string Serialize(List<KVPair> pairs)
        {
            if (pairs.Count == 0) return "";
            var parts = new string[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
                parts[i] = $"{pairs[i].Key}={pairs[i].Value}";
            return string.Join(";", parts);
        }
    }
}
