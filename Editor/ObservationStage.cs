#nullable enable

namespace SceneBlueprint.Editor
{
    public enum ObservationStage
    {
        None = 0,
        DefinitionValidation = 1,
        SemanticAnalysis = 2,
        PlanCompilation = 3,
        RuntimeState = 4,
        EventHistory = 5,
        DebugProjection = 6,
        Metadata = 7,
    }

    internal static class ObservationStageUtility
    {
        public static string GetTitle(ObservationStage stage)
        {
            switch (stage)
            {
                case ObservationStage.DefinitionValidation:
                    return "定义校验";
                case ObservationStage.SemanticAnalysis:
                    return "语义解析";
                case ObservationStage.PlanCompilation:
                    return "编译计划";
                case ObservationStage.RuntimeState:
                    return "执行状态";
                case ObservationStage.EventHistory:
                    return "事件历史";
                case ObservationStage.DebugProjection:
                    return "调试投影";
                case ObservationStage.Metadata:
                    return "运输元数据";
                default:
                    return string.Empty;
            }
        }

        public static ObservationStage InferFromLabel(string? label)
        {
            var normalized = label?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return ObservationStage.None;
            }

            if (normalized.Contains("定义", System.StringComparison.Ordinal)
                || normalized.Contains("诊断", System.StringComparison.Ordinal)
                || normalized.Contains("校验", System.StringComparison.Ordinal))
            {
                return ObservationStage.DefinitionValidation;
            }

            if (normalized.Contains("计划", System.StringComparison.Ordinal))
            {
                return ObservationStage.PlanCompilation;
            }

            if (normalized.Contains("Schema", System.StringComparison.Ordinal)
                || normalized.Contains("Metadata", System.StringComparison.Ordinal)
                || normalized.Contains("元数据", System.StringComparison.Ordinal))
            {
                return ObservationStage.Metadata;
            }

            if (normalized == "事件类型"
                || normalized == "来源节点"
                || normalized == "节点索引"
                || normalized == "Tick"
                || normalized == "信号标签"
                || normalized == "载荷"
                || normalized.StartsWith("载荷.", System.StringComparison.Ordinal))
            {
                return ObservationStage.EventHistory;
            }

            if (normalized.Contains("语义", System.StringComparison.Ordinal)
                || normalized.Contains("条件", System.StringComparison.Ordinal)
                || normalized.Contains("身份", System.StringComparison.Ordinal)
                || normalized.Contains("引用", System.StringComparison.Ordinal)
                || normalized.Contains("标识", System.StringComparison.Ordinal)
                || normalized.Contains("公共ID", System.StringComparison.Ordinal)
                || normalized.Contains("编译ID", System.StringComparison.Ordinal)
                || normalized.Contains("实体ID", System.StringComparison.Ordinal)
                || normalized.Contains("上游输入", System.StringComparison.Ordinal)
                || normalized.StartsWith("主体", System.StringComparison.Ordinal)
                || normalized.StartsWith("发起者", System.StringComparison.Ordinal)
                || normalized.StartsWith("目标", System.StringComparison.Ordinal)
                || normalized == "区域")
            {
                return ObservationStage.SemanticAnalysis;
            }

            return ObservationStage.RuntimeState;
        }
    }
}
