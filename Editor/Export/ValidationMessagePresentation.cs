#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// 统一导出阶段 validation message 的计数与对话框文案生成，
    /// 避免 export 入口继续只给一个“错误数”而不解释阻断内容。
    /// </summary>
    public static class ValidationMessagePresentation
    {
        public readonly struct Summary
        {
            public Summary(int errorCount, int warningCount, int infoCount)
            {
                ErrorCount = errorCount;
                WarningCount = warningCount;
                InfoCount = infoCount;
            }

            public int ErrorCount { get; }

            public int WarningCount { get; }

            public int InfoCount { get; }
        }

        public static Summary BuildSummary(IReadOnlyList<ValidationMessage>? messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return new Summary(0, 0, 0);
            }

            var errorCount = 0;
            var warningCount = 0;
            var infoCount = 0;
            for (var index = 0; index < messages.Count; index++)
            {
                switch (messages[index].Level)
                {
                    case ValidationLevel.Error:
                        errorCount++;
                        break;
                    case ValidationLevel.Warning:
                        warningCount++;
                        break;
                    default:
                        infoCount++;
                        break;
                }
            }

            return new Summary(errorCount, warningCount, infoCount);
        }

        public static string BuildDialogMessage(
            string intro,
            IReadOnlyList<ValidationMessage>? messages,
            int maxItems = 6)
        {
            var summary = BuildSummary(messages);
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(intro))
            {
                builder.AppendLine(intro.Trim());
            }

            builder.AppendLine($"错误 {summary.ErrorCount} · 警告 {summary.WarningCount} · 信息 {summary.InfoCount}");

            if (messages == null || messages.Count == 0)
            {
                return builder.ToString().TrimEnd();
            }

            var appended = 0;
            foreach (var message in messages.Where(static item => item.Level == ValidationLevel.Error))
            {
                if (appended >= maxItems)
                {
                    break;
                }

                builder.AppendLine($"- {message.Message}");
                appended++;
            }

            if (appended == 0)
            {
                foreach (var message in messages.Where(static item => item.Level == ValidationLevel.Warning))
                {
                    if (appended >= maxItems)
                    {
                        break;
                    }

                    builder.AppendLine($"- {message.Message}");
                    appended++;
                }
            }

            var remaining = messages.Count - appended;
            if (remaining > 0)
            {
                builder.AppendLine($"- 其余 {remaining} 条消息请查看 Console 日志");
            }

            return builder.ToString().TrimEnd();
        }
    }
}
