#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Runtime.Settings;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Settings
{
    [CustomEditor(typeof(SceneBlueprintLevelMonsterMappingAsset))]
    public sealed class SceneBlueprintLevelMonsterMappingAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (SceneBlueprintLevelMonsterMappingAsset)target;
            serializedObject.Update();

            var levelId = serializedObject.FindProperty("_levelId");
            var entries = serializedObject.FindProperty("_entries");
            if (levelId == null || entries == null)
            {
                EditorGUILayout.HelpBox("Monster Mapping 资产字段缺失。", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("关卡怪物映射 Level Monster Mapping", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "该资产是唯一的怪物映射数据源。项目设置页只做汇总展示与导航，具体映射内容请在这里维护。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            EditorGUILayout.PropertyField(levelId, new GUIContent("关卡 ID Level ID"));
            EditorGUILayout.Space(2f);

            DrawEntryTable(entries);
            DrawDuplicateWarnings(entries);

            if (serializedObject.ApplyModifiedProperties())
            {
                asset.ApplySnapshot(asset.CreateSnapshot());
                EditorUtility.SetDirty(target);
                SceneBlueprintMonsterMappingRegistry.Invalidate();
            }
        }

        private static void DrawEntryTable(SerializedProperty entries)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("映射条目 Entries", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("添加条目 Add Entry", GUILayout.Width(150f)))
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("MonsterType")!.intValue = GetNextMonsterType(entries, entries.arraySize - 1);
                entry.FindPropertyRelative("MonsterId")!.intValue = 0;
                entry.FindPropertyRelative("ShortName")!.stringValue = string.Empty;
                entry.FindPropertyRelative("Description")!.stringValue = string.Empty;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12f);
            EditorGUILayout.LabelField("Type", EditorStyles.miniBoldLabel, GUILayout.Width(48f));
            EditorGUILayout.LabelField("MonsterId", EditorStyles.miniBoldLabel, GUILayout.Width(74f));
            EditorGUILayout.LabelField("简称 Short Name", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
            EditorGUILayout.LabelField("描述 Description", EditorStyles.miniBoldLabel);
            GUILayout.Space(48f);
            EditorGUILayout.EndHorizontal();

            if (entries.arraySize == 0)
            {
                EditorGUILayout.HelpBox("当前关卡还没有任何映射条目。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            for (int entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                var entry = entries.GetArrayElementAtIndex(entryIndex);
                var monsterType = entry.FindPropertyRelative("MonsterType");
                var monsterId = entry.FindPropertyRelative("MonsterId");
                var shortName = entry.FindPropertyRelative("ShortName");
                var description = entry.FindPropertyRelative("Description");
                if (monsterType == null || monsterId == null || shortName == null || description == null)
                    continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12f);
                EditorGUILayout.PropertyField(monsterType, GUIContent.none, GUILayout.Width(48f));
                EditorGUILayout.PropertyField(monsterId, GUIContent.none, GUILayout.Width(74f));
                EditorGUILayout.PropertyField(shortName, GUIContent.none, GUILayout.Width(120f));
                EditorGUILayout.PropertyField(description, GUIContent.none, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(40f)))
                {
                    entries.DeleteArrayElementAtIndex(entryIndex);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private static int GetNextMonsterType(SerializedProperty entries, int newEntryIndex)
        {
            int maxMonsterType = -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (i == newEntryIndex)
                    continue;

                var entry = entries.GetArrayElementAtIndex(i);
                var monsterType = entry.FindPropertyRelative("MonsterType");
                if (monsterType == null)
                    continue;

                maxMonsterType = Mathf.Max(maxMonsterType, monsterType.intValue);
            }

            return maxMonsterType + 1;
        }

        private static void DrawDuplicateWarnings(SerializedProperty entries)
        {
            var seenTypes = new HashSet<int>();
            var duplicatedTypes = new HashSet<int>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var monsterType = entry.FindPropertyRelative("MonsterType");
                if (monsterType == null)
                    continue;

                int type = monsterType.intValue;
                if (!seenTypes.Add(type))
                    duplicatedTypes.Add(type);
            }

            foreach (int duplicatedType in duplicatedTypes)
            {
                EditorGUILayout.HelpBox(
                    $"存在重复 MonsterType: {duplicatedType}。请在资产内保持唯一映射，避免运行时语义歧义。",
                    MessageType.Warning);
            }
        }
    }
}
