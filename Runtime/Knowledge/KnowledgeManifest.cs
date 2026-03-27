#nullable enable
using UnityEngine;
using SceneBlueprint.Contract.Knowledge;

namespace SceneBlueprint.Runtime.Knowledge
{
    /// <summary>
    /// SceneBlueprint 知识库清单 ScriptableObject。
    /// 索引所有知识文档的路径和元数据，不存储文档内容。
    /// 业务层创建此 SO 的实例并填充各层级文档引用。
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneBlueprintKnowledge",
        menuName = "SceneBlueprint/Knowledge Manifest",
        order = 100)]
    public class KnowledgeManifest : ScriptableObject
    {
        // ══════════════════════════════════════
        //  共享层 (Shared)
        // ══════════════════════════════════════

        [Header("S0: 核心概念词汇表")]
        [Tooltip("统一术语表，程序和策划共用")]
        public KnowledgeDocRef CoreConcepts = new();

        [Header("S1: 类型定义总览")]
        [Tooltip("sbdef 定义的 Action/Marker/Annotation/Enum 概览")]
        public KnowledgeDocRef Definitions = new();

        // ══════════════════════════════════════
        //  程序视角 (Developer)
        // ══════════════════════════════════════

        [Header("D0: 框架架构设计")]
        [Tooltip("系统分层、模块依赖、数据流")]
        public KnowledgeDocRef Architecture = new();

        [Header("D1: 核心代码逻辑")]
        [Tooltip("关键类职责、方法签名、扩展点")]
        public KnowledgeDocRef CoreLogic = new();

        [Header("D2: 设计决策记录")]
        [Tooltip("ADR 格式的设计决策")]
        public KnowledgeDocRef Decisions = new();

        // ══════════════════════════════════════
        //  策划视角 (Designer)
        // ══════════════════════════════════════

        [Header("P0: 工作流全景")]
        [Tooltip("从零配完一个关卡的完整步骤")]
        public KnowledgeDocRef Workflow = new();

        [Header("P1: 节点使用手册")]
        [Tooltip("每个 Action 节点一条文档引用")]
        public KnowledgeDocRef[] ActionGuides = System.Array.Empty<KnowledgeDocRef>();

        [Header("P2: Marker 使用手册")]
        [Tooltip("各类 Marker 的使用方法")]
        public KnowledgeDocRef MarkerGuide = new();

        [Header("P3: FAQ / 问题排查")]
        [Tooltip("常见问题和排查清单")]
        public KnowledgeDocRef FAQ = new();

        // ══════════════════════════════════════
        //  Prompt 配置
        // ══════════════════════════════════════

        [Header("Prompt 配置")]
        [Tooltip("指向 default_prompt.json（TextAsset）")]
        public TextAsset? PromptConfigJson;

        /// <summary>
        /// 反序列化 Prompt 配置。
        /// </summary>
        public PromptConfigData? LoadPromptConfig()
        {
            if (PromptConfigJson == null) return null;

            try
            {
                return JsonUtility.FromJson<PromptConfigData>(PromptConfigJson.text);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[Knowledge] Prompt 配置反序列化失败: {ex.Message}");
                return null;
            }
        }
    }
}
