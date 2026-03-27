#nullable enable
using System;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 定义层校验的统一结果。
    /// 让 Inspector / preview compile / export 都能围绕同一套“问题列表 + 阻断语义”工作。
    /// </summary>
    public sealed class ActionDefinitionValidationResult
    {
        public ActionDefinitionValidationResult(ValidationIssue[]? issues = null)
        {
            Issues = issues ?? Array.Empty<ValidationIssue>();

            var errorCount = 0;
            for (var index = 0; index < Issues.Length; index++)
            {
                if (Issues[index].IsError)
                {
                    errorCount++;
                }
            }

            ErrorCount = errorCount;
            WarningCount = Issues.Length - errorCount;
        }

        public ValidationIssue[] Issues { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public bool HasIssues => Issues.Length > 0;

        public bool BlocksCompilation => ErrorCount > 0;

        public string BuildStatusLabel()
        {
            if (BlocksCompilation)
            {
                return $"错误 {ErrorCount} · 警告 {WarningCount} · 将阻断导出";
            }

            if (HasIssues)
            {
                return $"错误 0 · 警告 {WarningCount} · 可继续编译";
            }

            return "当前定义层校验通过，可继续进入语义解析与编译计划阶段";
        }
    }
}
