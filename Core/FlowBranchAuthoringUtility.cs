#nullable enable
using System;
using System.Collections.Generic;
using SceneBlueprint.Contract;
using SceneBlueprint.Core.Generated;

namespace SceneBlueprint.Core
{
    /// <summary>
    /// Flow.Branch 的定义驱动 authoring 读取工具。
    /// 这是 Flow.Branch 在 editor / compiler / runtime fallback 间共享的正式 authoring 入口。
    /// </summary>
    public static class FlowBranchAuthoringUtility
    {
        private static readonly PropertyDefinition[] Definitions =
        {
            Prop.Bool(ActionPortIds.FlowBranch.Condition, "条件结果", defaultValue: false)
                .WithTooltip("true 走 True 分支，false 走 False 分支。")
                .InSection("condition", "条件 Condition", sectionOrder: 0),
        };

        public static IReadOnlyList<PropertyDefinition> Properties
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static PropertyDefinition[] CreatePropertiesArray()
            => PropertyDefinitionValueUtility.CloneDefinitions(Definitions);

        public static PropertyDefinition? FindProperty(string propertyKey)
            => PropertyDefinitionValueUtility.FindClonedDefinition(Definitions, propertyKey);

        public static PropertyBagReader CreateBagReader(PropertyBag bag)
        {
            return new PropertyBagReader(bag, Definitions);
        }

        public static PropertyValueReader CreatePropertyReader(ActionEntry action, ActionDefinition? definition = null)
        {
            return new PropertyValueReader(action, definition ?? CreateDefinitionFallback());
        }

        public static FlowBranchAuthoringState Read(PropertyBag bag)
        {
            return Read(CreateBagReader(bag));
        }

        public static FlowBranchAuthoringState Read(ActionEntry action, ActionDefinition? definition = null)
        {
            return Read(CreatePropertyReader(action, definition));
        }

        public static FlowBranchAuthoringState Read(PropertyBagReader reader)
        {
            return new FlowBranchAuthoringState(
                reader.GetBool(ActionPortIds.FlowBranch.Condition.Key, false));
        }

        public static FlowBranchAuthoringState Read(PropertyValueReader reader)
        {
            return new FlowBranchAuthoringState(
                reader.GetBool(ActionPortIds.FlowBranch.Condition.Key, false));
        }

        public static string BuildConditionSummary(bool conditionValue)
        {
            return SemanticSummaryUtility.BuildFlowBranchConditionSummary(conditionValue);
        }

        public static string BuildRouteSummary(bool conditionValue, string? routedPort)
        {
            return SemanticSummaryUtility.BuildFlowBranchRouteSummary(conditionValue, routedPort);
        }

        public static string BuildConnectedBranchSummary(bool hasTrueOutput, bool hasFalseOutput)
        {
            if (hasTrueOutput && hasFalseOutput)
            {
                return "True / False 均已连接";
            }

            if (hasTrueOutput)
            {
                return "仅 True 分支已连接";
            }

            if (hasFalseOutput)
            {
                return "仅 False 分支已连接";
            }

            return "无已连接分支";
        }

        public static bool TryBuildAuthoringHint(
            bool hasTrueOutput,
            bool hasFalseOutput,
            out string message,
            out bool isWarning)
        {
            message = string.Empty;
            isWarning = false;

            if (!hasTrueOutput && !hasFalseOutput)
            {
                message = "True / False 两个分支当前都未连接，下游不会接收到分支结果。";
                isWarning = true;
                return true;
            }

            if (!hasTrueOutput)
            {
                message = "当前只连接了 False 分支；条件为真时不会有下游接收。";
                isWarning = false;
                return true;
            }

            if (!hasFalseOutput)
            {
                message = "当前只连接了 True 分支；条件为假时不会有下游接收。";
                isWarning = false;
                return true;
            }

            return false;
        }

        public static bool TryBuildValidationIssue(
            bool hasTrueOutput,
            bool hasFalseOutput,
            out ValidationIssue issue)
        {
            issue = null!;
            if (!hasTrueOutput && !hasFalseOutput)
            {
                issue = ValidationIssue.Error("True / False 两个分支当前都未连接，下游不会接收到分支结果。");
                return true;
            }

            return false;
        }

        private static ActionDefinition CreateDefinitionFallback()
        {
            return new ActionDefinition
            {
                Properties = CreatePropertiesArray(),
            };
        }
    }

    public readonly struct FlowBranchAuthoringState
    {
        public FlowBranchAuthoringState(bool conditionValue)
        {
            ConditionValue = conditionValue;
        }

        public bool ConditionValue { get; }
    }
}
