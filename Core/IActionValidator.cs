#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 行动类型级自定义验证器接口。
    /// 在 ActionDefinition 中设置，由分析层（SB006）在分析阶段调用。
    /// <para>
    /// 用于表达"只有这种行动类型才有的约束"，例如：
    ///   - Branch 节点：conditionVariable 已连时，trueOut/falseOut 必须各连一条
    ///   - Join 节点：至少需要 2 个 Control 输入才有意义
    /// </para>
    /// <para>
    /// 与全局 IBlueprintRule 的区别：IBlueprintRule 是图级别的横切规则；
    /// IActionValidator 是类型专属规则，定义和约束放在同一处。
    /// </para>
    /// </summary>
    public interface IActionValidator
    {
        /// <summary>
        /// 对单个节点执行类型专属验证。
        /// 返回发现的问题列表；无问题时返回空集合（不返回 null）。
        /// </summary>
        IEnumerable<ValidationIssue> Validate(NodeValidationContext ctx);
    }
}
