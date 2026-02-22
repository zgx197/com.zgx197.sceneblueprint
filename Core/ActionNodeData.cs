#nullable enable
using NodeGraph.Core;

namespace SceneBlueprint.Core
{
    // ═══════════════════════════════════════════════════════════
    //  行动节点数据 (ActionNodeData)
    //
    //  ActionNodeData 是“实例数据”，存储在 NodeGraph 的 Node.UserData 中。
    //  它和 ActionDefinition 的关系类似于“类”与“实例”的关系：
    //
    //  ActionDefinition  =  “刷怪行动有这些属性”   （模板）
    //  ActionNodeData    =  “这个刷怪节点的值是…”  （实例）
    //
    //  创建流程：
    //  1. 用户在编辑器中创建一个“刷怪”节点
    //  2. 系统根据 ActionDefinition 调用 CreateFromDefinition()
    //  3. 自动生成 ActionNodeData，填入默认值
    //  4. ActionNodeData 存储在 Node.UserData 中
    //  5. 用户通过 Inspector 修改 PropertyBag 中的值
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 行动节点数据——存储在 NodeGraph 的 Node.UserData 中。
    /// <para>
    /// 包含两部分信息：
    /// <list type="bullet">
    ///   <item><see cref="ActionTypeId"/> —— 指向哪个 ActionDefinition（“我是什么类型”）</item>
    ///   <item><see cref="Properties"/> —— 属性的实际值（“我的配置是什么”）</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ActionNodeData : INodeData, IDescribableNode
    {
        /// <summary>
        /// 节点描述/备注文字（策划填写，显示在节点标题下方）。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 指向 ActionDefinition.TypeId，表示这个节点是什么类型的行动。
        /// <para>如 "Combat.Spawn" 表示这是一个刷怪节点。</para>
        /// </summary>
        public string ActionTypeId { get; set; }

        /// <summary>
        /// 属性值容器——存储这个节点的所有属性值。
        /// <para>键名对应 PropertyDefinition.Key，值是用户在 Inspector 中设置的数据。</para>
        /// </summary>
        public PropertyBag Properties { get; set; }

        /// <summary>
        /// 构造函数——创建一个空的行动节点数据。
        /// <para>通常不直接调用，而是使用 <see cref="CreateFromDefinition"/> 工厂方法。</para>
        /// </summary>
        /// <param name="typeId">行动类型 ID，对应 ActionDefinition.TypeId</param>
        public ActionNodeData(string typeId)
        {
            ActionTypeId = typeId;
            Properties = new PropertyBag();
        }

        /// <summary>
        /// 根据 ActionDefinition 创建节点数据，自动填充默认值。
        /// <para>
        /// 这是创建新节点时的标准入口。它会：
        /// 1. 设置 ActionTypeId
        /// 2. 遍历 ActionDefinition.Properties 中的每个属性定义
        /// 3. 如果属性有默认值，将其填充到 PropertyBag 中
        /// </para>
        /// </summary>
        /// <param name="def">行动定义（模板）</param>
        /// <returns>填充了默认值的新节点数据</returns>
        public static ActionNodeData CreateFromDefinition(ActionDefinition def)
        {
            var data = new ActionNodeData(def.TypeId);
            // 遍历所有属性定义，将有默认值的属性填充到 PropertyBag
            foreach (var prop in def.Properties)
            {
                if (prop.DefaultValue != null)
                    data.Properties.Set(prop.Key, prop.DefaultValue);
            }
            return data;
        }
    }
}
