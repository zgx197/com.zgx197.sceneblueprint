#nullable enable

namespace SceneBlueprint.Contract.Knowledge
{
    /// <summary>
    /// 单条知识文档的元数据。
    /// 不存储文档内容本身，只做索引。实际内容由 .md 文件承载。
    /// </summary>
    [System.Serializable]
    public class KnowledgeEntry
    {
        /// <summary>所属知识层级</summary>
        public KnowledgeLayer Layer;

        /// <summary>显示标题（如 "Spawn.Wave — 波次刷怪"）</summary>
        public string Title = "";

        /// <summary>简短描述（一句话说明该文档覆盖什么内容）</summary>
        public string Description = "";

        /// <summary>
        /// 检索标签，用于关键词匹配。
        /// 例如 ["Spawn.Wave", "刷怪", "波次", "wave"]
        /// </summary>
        public string[] Tags = System.Array.Empty<string>();
    }

    /// <summary>
    /// Prompt 配置中的 Few-shot 示例对。
    /// </summary>
    [System.Serializable]
    public class FewShotExample
    {
        /// <summary>用户问题</summary>
        public string User = "";

        /// <summary>助手回答</summary>
        public string Assistant = "";
    }

    /// <summary>
    /// 单个角色的 Prompt 模板数据。
    /// 从 JSON 配置文件反序列化。
    /// </summary>
    [System.Serializable]
    public class PromptTemplate
    {
        /// <summary>角色显示名（如 "程序助手" / "策划助手"）</summary>
        public string DisplayName = "";

        /// <summary>系统级 Prompt（角色定义 + 回答规范）</summary>
        public string SystemPrompt = "";

        /// <summary>
        /// 可访问的知识层级列表。
        /// 字符串格式，与 KnowledgeLayer 枚举名对应。
        /// 例如 ["S0_CoreConcepts", "D0_Architecture", "D1_CoreLogic", "D2_Decisions"]
        /// </summary>
        public string[] KnowledgeLayers = System.Array.Empty<string>();

        /// <summary>
        /// 上下文模板，包含占位符。
        /// 占位符格式：{blueprintName}, {nodeList}, {selectedNode} 等。
        /// </summary>
        public string ContextTemplate = "";

        /// <summary>Few-shot 示例对</summary>
        public FewShotExample[] FewShotExamples = System.Array.Empty<FewShotExample>();
    }

    /// <summary>
    /// Prompt 配置根节点。
    /// 对应 default_prompt.json 的顶层结构。
    /// </summary>
    [System.Serializable]
    public class PromptConfigData
    {
        /// <summary>配置版本号</summary>
        public string Version = "1.0";

        /// <summary>程序角色 Prompt 模板</summary>
        public PromptTemplate? Developer;

        /// <summary>策划角色 Prompt 模板</summary>
        public PromptTemplate? Designer;
    }

    /// <summary>
    /// 蓝图实时上下文数据。
    /// 由 IBlueprintContextProvider 采集，注入到 Prompt 占位符。
    /// </summary>
    [System.Serializable]
    public class BlueprintContext
    {
        /// <summary>蓝图资产名</summary>
        public string BlueprintName = "";

        /// <summary>节点总数</summary>
        public int NodeCount;

        /// <summary>节点列表摘要（如 "Spawn.Wave × 2, Trigger.EnterArea × 1"）</summary>
        public string NodeListSummary = "";

        /// <summary>当前选中节点的 TypeId（未选中时为空）</summary>
        public string SelectedNodeTypeId = "";

        /// <summary>当前选中节点的显示名</summary>
        public string SelectedNodeDisplayName = "";

        /// <summary>选中节点的属性键值对摘要</summary>
        public string SelectedNodeProperties = "";

        /// <summary>校验问题列表（每条一行）</summary>
        public string ValidationIssues = "";

        // ── 程序角色专用 ──

        /// <summary>当前打开的文件路径（程序角色）</summary>
        public string ActiveFile = "";

        /// <summary>光标所在的类/方法名（程序角色）</summary>
        public string ActiveSymbol = "";
    }
}
