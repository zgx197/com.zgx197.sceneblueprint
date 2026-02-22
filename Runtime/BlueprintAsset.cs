#nullable enable
using UnityEngine;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime
{
    /// <summary>
    /// 蓝图资产（ScriptableObject）。
    /// 存储蓝图的图数据（JSON 字符串）和元信息。
    /// 
    /// 设计原则：
    /// - SO 本身只是数据容器，不包含序列化/反序列化逻辑
    /// - 图的序列化/反序列化由 Editor 层的 JsonGraphSerializer 负责
    /// - 场景绑定不存在 SO 中（由 SceneBlueprintManager 的 Slot 持有）
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewBlueprint",
        menuName = "SceneBlueprint/蓝图资产",
        order = 100)]
    public class BlueprintAsset : ScriptableObject
    {
        // ── 元信息 ──

        [Tooltip("蓝图唯一 ID（自动生成）")]
        public string BlueprintId = "";

        [Tooltip("蓝图显示名称")]
        public string BlueprintName = "";

        [Tooltip("蓝图描述")]
        [TextArea(2, 5)]
        public string Description = "";

        [Tooltip("数据版本号")]
        public int Version = 1;

        // ── 图数据（独立 .blueprint.json 文件引用）──

        [Tooltip("图数据文件引用（.blueprint.json，由编辑器自动管理）")]
        [HideInInspector]
        public UnityEngine.TextAsset? GraphData;

        // ── Blackboard 变量声明 ──

        [Tooltip("Blackboard 变量声明列表（由编辑器变量面板管理）")]
        [HideInInspector]
        public VariableDeclaration[] Variables = System.Array.Empty<VariableDeclaration>();

        /// <summary>图数据是否为空</summary>
        public bool IsEmpty => GraphData == null || string.IsNullOrEmpty(GraphData.text);

        /// <summary>初始化新蓝图（生成唯一 ID）</summary>
        public void InitializeNew(string name = "")
        {
            if (string.IsNullOrEmpty(BlueprintId))
                BlueprintId = System.Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(name))
                BlueprintName = name;
        }
    }
}
