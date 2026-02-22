#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneBlueprint.Runtime.Templates
{
    /// <summary>
    /// 子蓝图模板，存储一个可复用的子蓝图（节点 + 连线 + 元数据）。
    /// <para>
    /// 策划通过"保存为模板"创建，通过"模板库"面板拖入使用。
    /// 模板是"快照"——实例化后与模板断开联系，修改模板不影响已有关卡。
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewBlueprintTemplate",
        menuName = "SceneBlueprint/Blueprint Template",
        order = 101)]
    public class BlueprintTemplateSO : ScriptableObject
    {
        // ═══════════════════════════════════════════════════════════
        //  元数据
        // ═══════════════════════════════════════════════════════════

        [Header("── 元数据 ──")]
        [Tooltip("模板显示名称")]
        public string DisplayName = "";

        [Tooltip("分类，如 'Combat', 'Cinematic'")]
        public string Category = "";

        [TextArea(2, 4)]
        [Tooltip("描述文本")]
        public string Description = "";

        [Tooltip("缩略图（可选）")]
        public Texture2D? Thumbnail;

        // ═══════════════════════════════════════════════════════════
        //  图数据
        // ═══════════════════════════════════════════════════════════

        [Header("── 图数据（自动填充，请勿手动编辑） ──")]
        [HideInInspector]
        [Tooltip("子蓝图内部的序列化 JSON")]
        public string GraphJson = "";

        // ═══════════════════════════════════════════════════════════
        //  需求声明
        // ═══════════════════════════════════════════════════════════

        [Header("── 绑定需求（自动提取） ──")]
        [Tooltip("使用此模板时需要绑定的标记列表")]
        public List<TemplateBindingRequirement> BindingRequirements = new();

        // ═══════════════════════════════════════════════════════════
        //  统计信息（只读）
        // ═══════════════════════════════════════════════════════════

        [Header("── 统计（只读） ──")]
        [Tooltip("包含的节点数量")]
        public int NodeCount;

        [Tooltip("包含的 Action 类型列表")]
        public string ActionTypesSummary = "";

        [Tooltip("创建日期")]
        public string CreatedDate = "";

        [Tooltip("最后修改日期")]
        public string LastModified = "";

        // ═══════════════════════════════════════════════════════════
        //  嵌套序列化类型
        // ═══════════════════════════════════════════════════════════

        /// <summary>模板绑定需求——使用此模板时需要绑定的场景标记</summary>
        [Serializable]
        public class TemplateBindingRequirement
        {
            [Tooltip("绑定键名（模板内 PropertyBag 中的 key）")]
            public string BindingKey = "";

            [Tooltip("需要的标记类型 ID")]
            public string MarkerTypeId = "Point";

            [Tooltip("描述（如 '战斗区域'）")]
            public string Description = "";

            [Tooltip("对应的 Action 类型 ID（来源信息）")]
            public string SourceActionTypeId = "";
        }

        /// <summary>GraphJson 是否有效</summary>
        public bool HasValidGraph => !string.IsNullOrEmpty(GraphJson);
    }
}
