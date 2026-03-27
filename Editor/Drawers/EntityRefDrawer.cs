#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Editor.Drawers
{
    /// <summary>
    /// EntityRef 结构化编辑器——Inspector 中以 Mode 下拉 + 对应字段的形式编辑实体引用。
    /// <para>
    /// 序列化格式：<c>"Mode:Value"</c>，如 "ByRole:Boss"、"ByTag:CombatRole.Frontline"、"All:"、"ByTags:A+B"。
    /// Inspector 解析此字符串后绘制结构化 UI，修改后重新序列化回字符串。
    /// </para>
    /// </summary>
    public static class EntityRefDrawer
    {
        private static readonly string[] ModeLabels = { "按角色(ByRole)", "按场景对象(BySceneRef)", "全部(All)", "任意(Any)", "按Tag(ByTag)", "按Tags(ByTags)", "按别名(ByAlias)" };
        private static readonly EntityRefMode[] ModeValues = { EntityRefMode.ByRole, EntityRefMode.BySceneRef, EntityRefMode.All, EntityRefMode.Any, EntityRefMode.ByTag, EntityRefMode.ByTags, EntityRefMode.ByAlias };

        /// <summary>
        /// 绘制 EntityRef 选择器。
        /// </summary>
        /// <param name="label">字段显示名</param>
        /// <param name="currentValue">当前序列化值（"Mode:Value" 格式）</param>
        /// <returns>修改后的序列化值</returns>
        public static string Draw(
            string label,
            string currentValue,
            IReadOnlyList<EntityRefSceneCandidate>? sceneCandidates = null)
        {
            var entityRef = EntityRefCodec.Parse(currentValue);
            var (mode, value) = ExtractEditorValue(entityRef);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            // Mode 下拉
            int modeIndex = Array.IndexOf(ModeValues, mode);
            if (modeIndex < 0) modeIndex = 0;
            int newModeIndex = EditorGUILayout.Popup("引用模式", modeIndex, ModeLabels);
            var newMode = ModeValues[newModeIndex];

            // 模式切换时清空值
            if (newMode != mode)
            {
                mode = newMode;
                value = "";
            }

            // 根据模式绘制对应字段
            switch (mode)
            {
                case EntityRefMode.ByRole:
                    value = EditorGUILayout.TextField("角色名", value);
                    break;
                case EntityRefMode.BySceneRef:
                    value = DrawSceneRefField(value, sceneCandidates);
                    break;
                case EntityRefMode.All:
                case EntityRefMode.Any:
                    // 无需额外字段
                    EditorGUILayout.LabelField("", mode == EntityRefMode.All ? "引用所有实体" : "引用任意一个匹配实体", EditorStyles.miniLabel);
                    break;
                case EntityRefMode.ByTag:
                    value = EditorGUILayout.TextField("Tag 模式", value);
                    EditorGUILayout.LabelField("", "支持通配：如 CombatRole.* 或 CombatRole.Frontline", EditorStyles.miniLabel);
                    break;
                case EntityRefMode.ByTags:
                    value = EditorGUILayout.TextField("Tags（+分隔）", value);
                    EditorGUILayout.LabelField("", "AND 语义：用 + 分隔多个 Tag 模式", EditorStyles.miniLabel);
                    break;
                case EntityRefMode.ByAlias:
                    value = EditorGUILayout.TextField("逻辑别名", value);
                    EditorGUILayout.LabelField("", "推荐使用稳定逻辑身份，如 FinalBoss / PuzzleGem.Red", EditorStyles.miniLabel);
                    break;
            }

            DrawAuthoringSummary(Serialize(mode, value), sceneCandidates);

            EditorGUILayout.EndVertical();

            return Serialize(mode, value);
        }

        /// <summary>将 EntityRef 序列化值转换为运行时 EntityRef 对象</summary>
        public static EntityRef ToEntityRef(string serialized)
        {
            return EntityRefCodec.Parse(serialized);
        }

        // ── 序列化 / 反序列化 ──

        private static (EntityRefMode mode, string value) ExtractEditorValue(EntityRef entityRef)
        {
            if (entityRef == null)
            {
                return (EntityRefMode.ByRole, string.Empty);
            }

            return entityRef.Mode switch
            {
                EntityRefMode.ByRole     => (EntityRefMode.ByRole, entityRef.Role),
                EntityRefMode.BySceneRef => (EntityRefMode.BySceneRef, entityRef.SceneObjectId),
                EntityRefMode.All        => (EntityRefMode.All, string.Empty),
                EntityRefMode.Any        => (EntityRefMode.Any, string.Empty),
                EntityRefMode.ByTag      => (EntityRefMode.ByTag, entityRef.TagFilter),
                EntityRefMode.ByTags     => (EntityRefMode.ByTags, string.Join("+", entityRef.TagFilters ?? Array.Empty<string>())),
                EntityRefMode.ByAlias    => (EntityRefMode.ByAlias, entityRef.Alias),
                _                        => (EntityRefMode.ByRole, entityRef.Role)
            };
        }

        private static string Serialize(EntityRefMode mode, string value)
        {
            return EntityRefCodec.Serialize(mode, value);
        }

        private static string DrawSceneRefField(
            string currentValue,
            IReadOnlyList<EntityRefSceneCandidate>? sceneCandidates)
        {
            if (sceneCandidates != null && sceneCandidates.Count > 0)
            {
                var matchedCandidateIndex =
                    EntityRefSceneCandidateProvider.FindCandidateIndex(currentValue, sceneCandidates);
                var popupOptions = BuildSceneCandidateOptions(sceneCandidates);
                var popupIndex = matchedCandidateIndex >= 0 ? matchedCandidateIndex + 1 : 0;
                var newPopupIndex = EditorGUILayout.Popup("对象候选", popupIndex, popupOptions);
                currentValue = EntityRefSceneCandidateProvider.ResolveSceneObjectIdFromPopup(
                    currentValue,
                    newPopupIndex,
                    sceneCandidates);

                var selectedCandidateIndex =
                    EntityRefSceneCandidateProvider.FindCandidateIndex(currentValue, sceneCandidates);
                if (selectedCandidateIndex >= 0)
                {
                    var selectedCandidate = sceneCandidates[selectedCandidateIndex];
                    EditorGUILayout.LabelField(
                        "",
                        $"已匹配：{selectedCandidate.SubjectSummary}",
                        EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        "",
                        "当前 ID 未命中蓝图中的场景对象候选，可继续手动输入稳定 SceneObjectId。",
                        EditorStyles.miniLabel);
                }
            }

            return EditorGUILayout.TextField("场景对象ID", currentValue);
        }

        private static void DrawAuthoringSummary(
            string serializedValue,
            IReadOnlyList<EntityRefSceneCandidate>? sceneCandidates)
        {
            var summary = BuildAuthoringSummary(serializedValue, sceneCandidates);
            if (!summary.HasSummary && !summary.HasRuntimeIdentity && !summary.HasHelpText)
            {
                return;
            }

            EditorGUILayout.Space(2);
            if (summary.HasSummary)
            {
                EditorGUILayout.LabelField("", $"{summary.ModeLabel}：{summary.SummaryText}", EditorStyles.miniLabel);
            }

            if (summary.HasRuntimeIdentity)
            {
                EditorGUILayout.LabelField("", $"稳定身份：{summary.RuntimeIdentityText}", EditorStyles.miniLabel);
            }

            if (summary.HasHelpText)
            {
                EditorGUILayout.HelpBox(summary.HelpText, MessageType.Info);
            }
        }

        public static EntityRefAuthoringSummary BuildAuthoringSummary(
            string serializedValue,
            IReadOnlyList<EntityRefSceneCandidate>? sceneCandidates = null)
        {
            var entityRef = EntityRefCodec.Parse(serializedValue);
            return entityRef.Mode switch
            {
                EntityRefMode.BySceneRef => BuildSceneRefSummary(entityRef.SceneObjectId, sceneCandidates),
                EntityRefMode.ByAlias => BuildBasicSummary("逻辑别名", entityRef.Alias, serializedValue),
                EntityRefMode.ByRole => BuildBasicSummary("逻辑角色", entityRef.Role, serializedValue),
                EntityRefMode.ByTag => BuildBasicSummary("Tag 筛选", entityRef.TagFilter, serializedValue),
                EntityRefMode.ByTags => BuildBasicSummary("Tags 筛选", string.Join(" + ", entityRef.TagFilters ?? Array.Empty<string>()), serializedValue),
                EntityRefMode.All => BuildBasicSummary("引用范围", "全部实体", EntityRefCodec.Serialize(EntityRef.CreateAll())),
                EntityRefMode.Any => BuildBasicSummary("引用范围", "任意匹配实体", EntityRefCodec.Serialize(EntityRef.CreateAny())),
                _ => default
            };
        }

        private static EntityRefAuthoringSummary BuildSceneRefSummary(
            string sceneObjectId,
            IReadOnlyList<EntityRefSceneCandidate>? sceneCandidates)
        {
            var normalizedSceneObjectId = EntityRefSceneIdentityConventions.NormalizeSceneObjectId(sceneObjectId);
            if (string.IsNullOrEmpty(normalizedSceneObjectId))
            {
                return new EntityRefAuthoringSummary(
                    "静态主体",
                    string.Empty,
                    string.Empty,
                    "请选择蓝图中的静态场景对象，或手动输入稳定 SceneObjectId。");
            }

            if (EntityRefSceneIdentityConventions.TryFindCandidate(normalizedSceneObjectId, sceneCandidates, out var candidate))
            {
                return new EntityRefAuthoringSummary(
                    "静态主体",
                    candidate.SubjectSummary,
                    candidate.RuntimeIdentity,
                    EntityRefSceneIdentityConventions.StableIdentityHelpText);
            }

            return new EntityRefAuthoringSummary(
                "静态主体",
                EntityRefSceneIdentityConventions.BuildSubjectSummary(
                    normalizedSceneObjectId,
                    displayName: null,
                    objectType: null),
                EntityRefSceneIdentityConventions.BuildRuntimeIdentity(normalizedSceneObjectId),
                "当前 SceneObjectId 未命中蓝图中的静态对象候选；如为导出稳定 ID，可继续保留。");
        }

        private static EntityRefAuthoringSummary BuildBasicSummary(
            string modeLabel,
            string rawSummary,
            string runtimeIdentityText)
        {
            if (string.IsNullOrWhiteSpace(rawSummary) && string.IsNullOrWhiteSpace(runtimeIdentityText))
            {
                return default;
            }

            return new EntityRefAuthoringSummary(
                modeLabel,
                rawSummary ?? string.Empty,
                runtimeIdentityText ?? string.Empty,
                string.Empty);
        }

        private static string[] BuildSceneCandidateOptions(IReadOnlyList<EntityRefSceneCandidate> sceneCandidates)
        {
            var options = new string[sceneCandidates.Count + 1];
            options[0] = "(手动输入场景对象ID)";
            for (var index = 0; index < sceneCandidates.Count; index++)
            {
                options[index + 1] = sceneCandidates[index].DisplayLabel;
            }

            return options;
        }
    }
}
