#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using NodeGraph.Math;
using NodeGraph.Serialization;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Templates;
using UnityEditor;
using UnityEngine;

namespace SceneBlueprint.Editor.Templates
{
    /// <summary>
    /// 子蓝图模板工具类，提供"保存为模板"和"从模板实例化"两个核心能力。
    /// </summary>
    public static class BlueprintTemplateUtils
    {
        // ═══════════════════════════════════════════════════════════
        //  保存为模板
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 将子蓝图保存为模板资产。
        /// </summary>
        /// <param name="graph">当前蓝图的完整图</param>
        /// <param name="frame">要保存的子蓝图框</param>
        /// <param name="serializer">图序列化器</param>
        /// <returns>创建的模板资产路径，失败返回 null</returns>
        public static string? SaveAsTemplate(Graph graph, SubGraphFrame frame, JsonGraphSerializer serializer)
        {
            // 1. 弹出保存对话框
            string path = EditorUtility.SaveFilePanelInProject(
                "保存为子蓝图模板",
                frame.Title.Replace(" ", ""),
                "asset",
                "选择模板保存位置",
                "Assets/Extensions/SceneBlueprint/Templates/Blueprints");

            if (string.IsNullOrEmpty(path)) return null;

            // 2. 序列化子蓝图内部的节点和连线
            var containedNodeIds = frame.ContainedNodeIds.ToList();
            string subGraphJson = serializer.SerializeSubGraph(graph, containedNodeIds);

            // 3. 提取绑定需求
            var requirements = ExtractBindingRequirements(graph, containedNodeIds);

            // 4. 统计信息
            var actionTypes = new HashSet<string>();
            foreach (var nid in containedNodeIds)
            {
                var node = graph.FindNode(nid);
                if (node?.UserData is ActionNodeData data)
                    actionTypes.Add(data.ActionTypeId);
            }

            // 5. 创建 SO 资产
            var template = ScriptableObject.CreateInstance<BlueprintTemplateSO>();
            template.DisplayName = frame.Title;
            template.Category = "";
            template.Description = "";
            template.GraphJson = subGraphJson;
            template.BindingRequirements = requirements;
            template.NodeCount = containedNodeIds.Count;
            template.ActionTypesSummary = string.Join(", ", actionTypes.OrderBy(s => s));
            template.CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            template.LastModified = template.CreatedDate;

            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();

            SBLog.Info(SBLogTags.Template,
                $"已保存子蓝图模板: {path} ({containedNodeIds.Count} 节点, {requirements.Count} 绑定需求)");

            // 6. 弹出编辑窗口让策划填写元数据
            Selection.activeObject = template;
            EditorGUIUtility.PingObject(template);

            return path;
        }

        /// <summary>
        /// 从子蓝图节点中提取所有 SceneBinding 属性作为模板的绑定需求。
        /// </summary>
        private static List<BlueprintTemplateSO.TemplateBindingRequirement> ExtractBindingRequirements(
            Graph graph, List<string> nodeIds)
        {
            var result = new List<BlueprintTemplateSO.TemplateBindingRequirement>();
            var registry = SceneBlueprintProfile.CreateActionRegistry();
            var seen = new HashSet<string>();

            foreach (var nid in nodeIds)
            {
                var node = graph.FindNode(nid);
                if (node?.UserData is not ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.Type != PropertyType.SceneBinding) continue;
                    if (!seen.Add(prop.Key)) continue;

                    // 查找对应的 MarkerRequirement
                    var markerReq = actionDef.SceneRequirements?
                        .FirstOrDefault(r => r.BindingKey == prop.Key);

                    result.Add(new BlueprintTemplateSO.TemplateBindingRequirement
                    {
                        BindingKey = prop.Key,
                        MarkerTypeId = markerReq?.MarkerTypeId ?? "Point",
                        Description = prop.DisplayName,
                        SourceActionTypeId = data.ActionTypeId
                    });
                }
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════
        //  从模板实例化
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 从模板实例化一个子蓝图到目标图中。
        /// <para>
        /// 使用 NodeGraph 的 <see cref="SubGraphInstantiator"/> 进行 ID 重映射，
        /// 同时重新生成 PropertyBag 中的 MarkerId 以避免跨关卡冲突。
        /// </para>
        /// </summary>
        /// <param name="targetGraph">目标图</param>
        /// <param name="template">模板资产</param>
        /// <param name="serializer">图序列化器</param>
        /// <param name="insertPosition">插入位置（画布坐标）</param>
        /// <returns>实例化结果，失败返回 null</returns>
        public static SubGraphInstantiator.Result? InstantiateTemplate(
            Graph targetGraph,
            BlueprintTemplateSO template,
            JsonGraphSerializer serializer,
            Vec2 insertPosition)
        {
            if (!template.HasValidGraph)
            {
                SBLog.Error(SBLogTags.Template,
                    $"模板 '{template.DisplayName}' 的 GraphJson 为空，无法实例化");
                return null;
            }

            try
            {
                // 1. 反序列化模板的子图
                var sourceGraph = serializer.Deserialize(template.GraphJson);

                // 2. 使用 SubGraphInstantiator 实例化（自动 ID 重映射）
                var result = SubGraphInstantiator.Instantiate(
                    targetGraph,
                    sourceGraph,
                    template.DisplayName,
                    insertPosition,
                    sourceAssetId: AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(template)));

                // 3. 重新生成 PropertyBag 中的 MarkerId（避免跨关卡冲突）
                RegenerateMarkerIds(targetGraph, result.NodeIdMap);

                SBLog.Info(SBLogTags.Template,
                    $"已从模板 '{template.DisplayName}' 实例化子蓝图 " +
                    $"({result.NodeIdMap.Count} 节点)");

                return result;
            }
            catch (Exception ex)
            {
                SBLog.Error(SBLogTags.Template,
                    $"从模板 '{template.DisplayName}' 实例化失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 重新生成实例化节点中 PropertyBag 的 MarkerId，避免跨关卡冲突。
        /// </summary>
        private static void RegenerateMarkerIds(Graph graph, Dictionary<string, string> nodeIdMap)
        {
            foreach (var newNodeId in nodeIdMap.Values)
            {
                var node = graph.FindNode(newNodeId);
                if (node?.UserData is not ActionNodeData data) continue;

                var keysToUpdate = new List<string>();
                foreach (var kvp in data.Properties.All)
                {
                    // MarkerId 通常是 GUID 格式的字符串
                    if (kvp.Value is string str && IsLikelyMarkerId(str))
                    {
                        keysToUpdate.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToUpdate)
                {
                    data.Properties.Set(key, Guid.NewGuid().ToString());
                }
            }
        }

        /// <summary>
        /// 简单判断字符串是否像 MarkerId（GUID 格式）。
        /// MarkerId 通常是 32 位以上包含 '-' 的字符串。
        /// </summary>
        private static bool IsLikelyMarkerId(string str)
        {
            return str.Length >= 32 && str.Contains('-') && Guid.TryParse(str, out _);
        }

        // ═══════════════════════════════════════════════════════════
        //  模板库查询
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 获取项目中所有 BlueprintTemplateSO 资产。
        /// </summary>
        public static List<BlueprintTemplateSO> FindAllTemplates()
        {
            var result = new List<BlueprintTemplateSO>();
            var guids = AssetDatabase.FindAssets("t:BlueprintTemplateSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<BlueprintTemplateSO>(path);
                if (template != null)
                    result.Add(template);
            }
            return result;
        }

        /// <summary>
        /// 按分类分组获取所有模板。
        /// </summary>
        public static Dictionary<string, List<BlueprintTemplateSO>> FindAllTemplatesGrouped()
        {
            var grouped = new Dictionary<string, List<BlueprintTemplateSO>>();
            foreach (var template in FindAllTemplates())
            {
                var category = string.IsNullOrEmpty(template.Category) ? "未分类" : template.Category;
                if (!grouped.TryGetValue(category, out var list))
                {
                    list = new List<BlueprintTemplateSO>();
                    grouped[category] = list;
                }
                list.Add(template);
            }
            return grouped;
        }
    }
}
