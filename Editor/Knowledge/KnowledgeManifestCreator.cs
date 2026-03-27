#nullable enable
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Contract.Knowledge;
using SceneBlueprint.Runtime.Knowledge;

namespace SceneBlueprint.Editor.Knowledge
{
    /// <summary>
    /// 一键创建并预填充 KnowledgeManifest 资产。
    /// 菜单路径：SceneBlueprint → Knowledge → Create Knowledge Manifest
    /// </summary>
    public static class KnowledgeManifestCreator
    {
        private const string KnowledgeRoot = "Assets/Extensions/SceneBlueprintUser/Knowledge";
        private const string ManifestPath  = KnowledgeRoot + "/SceneBlueprintKnowledge.asset";

        [MenuItem("SceneBlueprint/Knowledge/Create Knowledge Manifest", priority = 200)]
        public static void CreateManifest()
        {
            // 如果已存在，选中它
            var existing = AssetDatabase.LoadAssetAtPath<KnowledgeManifest>(ManifestPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                UnityEngine.Debug.Log($"[Knowledge] Manifest 已存在: {ManifestPath}");
                return;
            }

            // 创建 SO 实例
            var manifest = ScriptableObject.CreateInstance<KnowledgeManifest>();

            // ── S0: 核心概念 ──
            manifest.CoreConcepts = CreateDocRef(
                KnowledgeLayer.S0_CoreConcepts,
                "S0: 核心概念词汇表",
                "程序和策划共用的统一术语表",
                new[] { "术语", "概念", "词汇", "blueprint", "action", "marker", "binding" },
                "Shared/S0_CoreConcepts.md");

            // ── D0: 框架架构 ──
            manifest.Architecture = CreateDocRef(
                KnowledgeLayer.D0_Architecture,
                "D0: 框架架构设计",
                "系统分层、模块依赖、数据流和扩展点",
                new[] { "架构", "architecture", "程序集", "asmdef", "数据流", "扩展点", "BlueprintExporter", "BlueprintRunner" },
                "Developer/D0_Architecture.md");

            // ── D1: 核心代码逻辑 ──
            manifest.CoreLogic = CreateDocRef(
                KnowledgeLayer.D1_CoreLogic,
                "D1: 核心代码逻辑",
                "关键类职责、方法签名、扩展点（待填充）",
                new[] { "代码", "类", "方法", "API", "接口" },
                "Developer/D1_CoreLogic.md");

            // ── D2: 设计决策 ──
            manifest.Decisions = CreateDocRef(
                KnowledgeLayer.D2_Decisions,
                "D2: 设计决策记录",
                "ADR 格式的设计决策（待填充）",
                new[] { "决策", "ADR", "设计", "为什么" },
                "Developer/D2_Decisions.md");

            // ── P0: 工作流全景 ──
            manifest.Workflow = CreateDocRef(
                KnowledgeLayer.P0_Workflow,
                "P0: 工作流全景",
                "从零配完一个关卡的完整步骤",
                new[] { "工作流", "步骤", "流程", "怎么用", "新手", "入门", "创建", "导出" },
                "Designer/P0_Workflow.md");

            // ── P1: Action 节点文档 ──
            manifest.ActionGuides = new[]
            {
                CreateDocRef(
                    KnowledgeLayer.P1_ActionGuide,
                    "Spawn.Wave — 波次刷怪",
                    "按波次分批刷出怪物（待填充）",
                    new[] { "Spawn.Wave", "波次", "刷怪", "wave", "刷新" },
                    "Designer/P1_Actions/Spawn.Wave.md"),
                CreateDocRef(
                    KnowledgeLayer.P1_ActionGuide,
                    "Spawn.Preset — 预设刷怪",
                    "在固定位置预先放置怪物（待填充）",
                    new[] { "Spawn.Preset", "预设", "固定位置", "preset" },
                    "Designer/P1_Actions/Spawn.Preset.md"),
                CreateDocRef(
                    KnowledgeLayer.P1_ActionGuide,
                    "Trigger.EnterArea — 区域进入触发",
                    "玩家进入区域时触发下游逻辑（待填充）",
                    new[] { "Trigger.EnterArea", "触发", "进入", "区域", "trigger" },
                    "Designer/P1_Actions/Trigger.EnterArea.md"),
            };

            // ── P2: Marker 手册 ──
            manifest.MarkerGuide = CreateDocRef(
                KnowledgeLayer.P2_MarkerGuide,
                "P2: Marker 使用手册",
                "各类场景标记的使用方法（待填充）",
                new[] { "marker", "标记", "区域", "点位", "AreaMarker", "PointMarker" },
                "Designer/P2_Markers.md");

            // ── P3: FAQ ──
            manifest.FAQ = CreateDocRef(
                KnowledgeLayer.P3_FAQ,
                "P3: FAQ / 问题排查",
                "常见问题和排查清单（待填充）",
                new[] { "FAQ", "问题", "报错", "不生效", "排查" },
                "Designer/P3_FAQ.md");

            // ── Prompt 配置 ──
            var promptJson = AssetDatabase.LoadAssetAtPath<TextAsset>(KnowledgeRoot + "/Prompts/default_prompt.json");
            manifest.PromptConfigJson = promptJson;

            // 保存资产
            AssetDatabase.CreateAsset(manifest, ManifestPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = manifest;
            EditorGUIUtility.PingObject(manifest);
            UnityEngine.Debug.Log($"[Knowledge] Manifest 创建成功: {ManifestPath}\n" +
                                  $"  已索引 {CountDocs(manifest)} 篇文档\n" +
                                  $"  Prompt 配置: {(promptJson != null ? "已绑定" : "未找到")}");
        }

        [MenuItem("SceneBlueprint/Knowledge/Create Knowledge Manifest", true)]
        private static bool ValidateCreateManifest()
        {
            return !EditorApplication.isCompiling;
        }

        // ── 辅助方法 ──

        private static KnowledgeDocRef CreateDocRef(
            KnowledgeLayer layer, string title, string description, string[] tags, string relativePath)
        {
            var docRef = new KnowledgeDocRef
            {
                Entry = new KnowledgeEntry
                {
                    Layer = layer,
                    Title = title,
                    Description = description,
                    Tags = tags,
                },
                FilePath = KnowledgeRoot + "/" + relativePath,
            };

            // 尝试绑定 TextAsset
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(docRef.FilePath);
            if (textAsset != null)
                docRef.MarkdownFile = textAsset;

            return docRef;
        }

        private static int CountDocs(KnowledgeManifest manifest)
        {
            int count = 0;
            if (!string.IsNullOrEmpty(manifest.CoreConcepts?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.Architecture?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.CoreLogic?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.Decisions?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.Workflow?.Entry.Title)) count++;
            if (manifest.ActionGuides != null)
                foreach (var g in manifest.ActionGuides)
                    if (!string.IsNullOrEmpty(g?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.MarkerGuide?.Entry.Title)) count++;
            if (!string.IsNullOrEmpty(manifest.FAQ?.Entry.Title)) count++;
            return count;
        }
    }
}
