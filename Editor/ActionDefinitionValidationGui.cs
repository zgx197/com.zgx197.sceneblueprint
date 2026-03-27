#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// 统一 definition validation 在 Inspector / template editor 里的展示与消息分发。
    /// 避免不同入口继续各自维护“同一份校验结果怎么转成 HelpBox / string list”的协议。
    /// </summary>
    public static class ActionDefinitionValidationGui
    {
        public static void DrawResult(
            ActionDefinitionValidationResult result,
            string passedMessage)
        {
            EditorGUILayout.LabelField(result.BuildStatusLabel(), EditorStyles.miniLabel);
            if (!result.HasIssues)
            {
                EditorGUILayout.HelpBox(passedMessage, MessageType.Info);
                return;
            }

            DrawIssues(result);
        }

        public static void DrawIssues(ActionDefinitionValidationResult result)
        {
            for (var index = 0; index < result.Issues.Length; index++)
            {
                var issue = result.Issues[index];
                EditorGUILayout.HelpBox(
                    issue.Message,
                    issue.IsError ? MessageType.Error : MessageType.Warning);
            }
        }

        public static void AppendMessages(
            ActionDefinitionValidationResult result,
            ICollection<string> errors,
            ICollection<string> warnings)
        {
            for (var index = 0; index < result.Issues.Length; index++)
            {
                var issue = result.Issues[index];
                if (issue.IsError)
                {
                    errors.Add(issue.Message);
                }
                else
                {
                    warnings.Add(issue.Message);
                }
            }
        }

        public static void DrawMessages(
            IReadOnlyCollection<string> errors,
            IReadOnlyCollection<string> warnings,
            string passedMessage,
            string? blockingSummary = null,
            string? warningSummary = null)
        {
            var errorCount = errors?.Count ?? 0;
            var warningCount = warnings?.Count ?? 0;

            if (errorCount > 0)
            {
                EditorGUILayout.LabelField(
                    string.IsNullOrWhiteSpace(blockingSummary)
                        ? $"错误 {errorCount} · 警告 {warningCount} · 当前不可继续"
                        : blockingSummary,
                    EditorStyles.miniLabel);
            }
            else if (warningCount > 0)
            {
                EditorGUILayout.LabelField(
                    string.IsNullOrWhiteSpace(warningSummary)
                        ? $"错误 0 · 警告 {warningCount} · 当前可继续"
                        : warningSummary,
                    EditorStyles.miniLabel);
            }

            if (errorCount == 0 && warningCount == 0)
            {
                EditorGUILayout.HelpBox(passedMessage, MessageType.Info);
                return;
            }

            if (errors != null)
            {
                foreach (var error in errors.Where(message => !string.IsNullOrWhiteSpace(message)))
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            if (warnings != null)
            {
                foreach (var warning in warnings.Where(message => !string.IsNullOrWhiteSpace(message)))
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }
        }
    }
}
