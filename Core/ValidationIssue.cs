#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// IActionValidator.Validate() 返回的单条验证问题。
    /// </summary>
    public class ValidationIssue
    {
        /// <summary>问题描述</summary>
        public string Message { get; }

        /// <summary>true = Error（阻断导出）；false = Warning（提示但不阻断）</summary>
        public bool IsError { get; }

        /// <summary>
        /// 关联的端口语义 Id（如 "trueOut"）；null 表示节点级问题，不针对特定端口。
        /// </summary>
        public string? PortId { get; }

        public ValidationIssue(string message, bool isError, string? portId = null)
        {
            Message = message;
            IsError = isError;
            PortId  = portId;
        }

        /// <summary>创建 Error 级问题</summary>
        public static ValidationIssue Error(string message, string? portId = null)
            => new ValidationIssue(message, isError: true, portId);

        /// <summary>创建 Warning 级问题</summary>
        public static ValidationIssue Warning(string message, string? portId = null)
            => new ValidationIssue(message, isError: false, portId);
    }
}
