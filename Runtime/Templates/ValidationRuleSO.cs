#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Templates
{
    /// <summary>验证规则严重级别</summary>
    public enum RuleSeverity { Warning, Error }

    /// <summary>
    /// 验证规则类型。
    /// </summary>
    public enum ValidationType
    {
        /// <summary>指定 Action 的指定属性必须非空</summary>
        PropertyRequired,

        /// <summary>指定 Action 的所有 SceneBinding 必须已绑定</summary>
        BindingRequired,

        /// <summary>子蓝图内至少 N 个节点</summary>
        MinNodesInSubGraph,
    }

    /// <summary>
    /// 可配置的蓝图验证规则——补充框架内置的结构验证。
    /// <para>
    /// 策划通过创建 ValidationRuleSO 资产来定义业务层面的验证规则：
    /// <list type="bullet">
    ///   <item><b>PropertyRequired</b>：要求指定 Action 的指定属性不能为空</item>
    ///   <item><b>BindingRequired</b>：要求指定 Action 的所有场景绑定都已配置</item>
    ///   <item><b>MinNodesInSubGraph</b>：要求子蓝图内至少有指定数量的节点</item>
    /// </list>
    /// </para>
    /// <para>
    /// 这些规则在导出时与 C# 内置规则合并执行，不影响编辑器的正常使用。
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewValidationRule",
        menuName = "SceneBlueprint/Validation Rule",
        order = 104)]
    public class ValidationRuleSO : ScriptableObject
    {
        // ─── 基础 ───

        [Header("── 基础 ──")]

        [Tooltip("规则 ID，全局唯一（如 'Combat.Spawn.BindingRequired'）")]
        public string RuleId = "";

        [Tooltip("规则描述（用于日志和配置窗口显示）")]
        [TextArea(1, 3)]
        public string Description = "";

        [Tooltip("严重级别")]
        public RuleSeverity Severity = RuleSeverity.Warning;

        [Tooltip("是否启用此规则")]
        public bool Enabled = true;

        // ─── 规则类型 ───

        [Header("── 规则类型 ──")]

        [Tooltip("验证类型")]
        public ValidationType Type;

        // ─── 参数（按 Type 填写）───

        [Header("── 参数 ──")]

        [Tooltip("目标 Action TypeId（Type=PropertyRequired/BindingRequired 时填写）")]
        public string TargetActionTypeId = "";

        [Tooltip("目标属性 Key（Type=PropertyRequired 时填写）")]
        public string TargetPropertyKey = "";

        [Tooltip("子蓝图最少节点数（Type=MinNodesInSubGraph 时填写）")]
        [Min(1)]
        public int MinNodeCount = 1;
    }
}
