#nullable enable
using UnityEngine;

namespace SceneBlueprint.Runtime.Templates
{
    /// <summary>
    /// 节点分类定义——集中管理右键菜单分组、节点颜色、排序。
    /// <para>
    /// 策划通过创建 CategorySO 资产来定义和管理 Action 的分类体系：
    /// <list type="bullet">
    ///   <item>统一管理分类的显示名称、图标、主题色</item>
    ///   <item>通过 SortOrder 控制右键菜单中的分类排序</item>
    ///   <item>ActionDefinition 未指定 ThemeColor 时，从匹配的 CategorySO 继承</item>
    /// </list>
    /// </para>
    /// <para>
    /// CategorySO 是可选的——没有对应 SO 的分类仍会正常显示（使用字母排序和默认颜色）。
    /// </para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewCategory",
        menuName = "SceneBlueprint/Category",
        order = 103)]
    public class CategorySO : ScriptableObject
    {
        // ─── 基础 ───

        [Header("── 基础 ──")]

        [Tooltip("分类 ID，需与 ActionDefinition.Category 匹配（如 'Combat'）")]
        public string CategoryId = "";

        [Tooltip("显示名称（如 '战斗'）。为空时使用 CategoryId")]
        public string DisplayName = "";

        [TextArea(1, 3)]
        [Tooltip("分类描述")]
        public string Description = "";

        // ─── 视觉 ───

        [Header("── 视觉 ──")]

        [Tooltip("分类主题色——节点未指定 ThemeColor 时使用此色")]
        public Color ThemeColor = new(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("图标标识（可选）")]
        public string Icon = "";

        // ─── 排序 ───

        [Header("── 排序 ──")]

        [Tooltip("排序权重——越小越靠前（默认 100）")]
        public int SortOrder = 100;

        /// <summary>获取用于显示的名称（优先 DisplayName，其次 CategoryId）</summary>
        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(DisplayName) ? CategoryId : DisplayName;
        }
    }
}
