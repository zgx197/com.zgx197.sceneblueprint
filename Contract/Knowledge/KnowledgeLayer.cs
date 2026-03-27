#nullable enable

namespace SceneBlueprint.Contract.Knowledge
{
    /// <summary>
    /// 知识层级枚举。
    /// 共享层(S) + 程序视角(D) + 策划视角(P)。
    /// </summary>
    public enum KnowledgeLayer
    {
        /// <summary>S0: 核心概念词汇表（双方共用）</summary>
        S0_CoreConcepts,

        /// <summary>D0: 框架架构设计</summary>
        D0_Architecture,

        /// <summary>D1: 核心代码逻辑</summary>
        D1_CoreLogic,

        /// <summary>D2: 设计决策记录 (ADR)</summary>
        D2_Decisions,

        /// <summary>P0: 工作流全景</summary>
        P0_Workflow,

        /// <summary>P1: 节点使用手册（每个 Action 一份）</summary>
        P1_ActionGuide,

        /// <summary>P2: Marker 使用手册</summary>
        P2_MarkerGuide,

        /// <summary>P3: FAQ / 问题排查</summary>
        P3_FAQ,

        // ── 后续追加的共享层，显式赋值以避免改变现有序号 ──

        /// <summary>S1: 类型定义总览（Action/Marker/Annotation/Enum，双方共用）</summary>
        S1_Definitions = 8,
    }

    /// <summary>
    /// Prompt 角色枚举。
    /// </summary>
    public enum PromptRole
    {
        /// <summary>程序开发者</summary>
        Developer,

        /// <summary>关卡策划</summary>
        Designer,
    }
}
