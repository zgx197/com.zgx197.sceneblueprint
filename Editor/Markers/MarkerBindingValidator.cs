#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// 验证结果条目
    /// </summary>
    public struct ValidationEntry
    {
        public enum Severity { Info, Warning, Error }

        public Severity Level;
        public string Message;
        public string? NodeId;      // 关联的蓝图节点 ID（可空）
        public string? MarkerId;    // 关联的场景标记 ID（可空）

        public ValidationEntry(Severity level, string message, string? nodeId = null, string? markerId = null)
        {
            Level = level;
            Message = message;
            NodeId = nodeId;
            MarkerId = markerId;
        }
    }

    /// <summary>
    /// 验证报告
    /// </summary>
    public class ValidationReport
    {
        public List<ValidationEntry> Entries { get; } = new();

        public int ErrorCount => Entries.Count(e => e.Level == ValidationEntry.Severity.Error);
        public int WarningCount => Entries.Count(e => e.Level == ValidationEntry.Severity.Warning);
        public int InfoCount => Entries.Count(e => e.Level == ValidationEntry.Severity.Info);

        public bool HasIssues => ErrorCount > 0 || WarningCount > 0;

        public void Add(ValidationEntry.Severity level, string message, string? nodeId = null, string? markerId = null)
        {
            Entries.Add(new ValidationEntry(level, message, nodeId, markerId));
        }
    }

    /// <summary>
    /// 标记绑定一致性验证器。
    /// <para>
    /// 在蓝图编辑器打开/加载蓝图时调用，检查：
    /// <list type="bullet">
    ///   <item>缺失标记：蓝图节点引用的 MarkerId 在场景中找不到对应的 SceneMarker</item>
    ///   <item>孤立标记：场景中存在 SceneMarker 但没有被任何蓝图节点引用</item>
    ///   <item>类型不匹配：绑定的标记类型与 MarkerRequirement 声明的类型不一致</item>
    ///   <item>必需绑定缺失：Action 声明了 Required 的 MarkerRequirement 但未绑定</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class MarkerBindingValidator
    {
        /// <summary>
        /// 执行完整的绑定一致性验证。
        /// </summary>
        /// <param name="graph">蓝图 Graph</param>
        /// <param name="actionRegistry">Action 注册表</param>
        /// <returns>验证报告</returns>
        public static ValidationReport Validate(Graph graph, ActionRegistry actionRegistry)
        {
            var report = new ValidationReport();

            // 收集场景中所有标记
            var sceneMarkers = Object.FindObjectsOfType<SceneMarker>();
            var markerById = new Dictionary<string, SceneMarker>();
            foreach (var m in sceneMarkers)
            {
                if (!string.IsNullOrEmpty(m.MarkerId))
                    markerById[m.MarkerId] = m;
            }

            // 收集蓝图中所有被引用的 MarkerId
            var referencedMarkerIds = new HashSet<string>();

            foreach (var node in graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;
                if (!actionRegistry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                // 检查该 Action 的 SceneRequirements
                if (actionDef.SceneRequirements != null && actionDef.SceneRequirements.Length > 0)
                {
                    ValidateNodeRequirements(report, node, data, actionDef, markerById, referencedMarkerIds);
                }

                // 收集所有属性中引用的 MarkerId
                foreach (var kvp in data.Properties.All)
                {
                    if (kvp.Value is string strVal && !string.IsNullOrEmpty(strVal))
                    {
                        if (markerById.ContainsKey(strVal))
                            referencedMarkerIds.Add(strVal);
                    }
                }
            }

            // 检查缺失标记：节点引用了不存在的 MarkerId
            CheckMissingMarkers(report, graph, markerById);

            // 检查孤立标记：场景中的标记没有被蓝图引用
            CheckOrphanedMarkers(report, sceneMarkers, referencedMarkerIds);

            return report;
        }

        /// <summary>验证单个节点的 MarkerRequirement 绑定</summary>
        private static void ValidateNodeRequirements(
            ValidationReport report,
            Node node,
            ActionNodeData data,
            ActionDefinition actionDef,
            Dictionary<string, SceneMarker> markerById,
            HashSet<string> referencedMarkerIds)
        {
            foreach (var req in actionDef.SceneRequirements)
            {
                // 检查该 BindingKey 是否在属性中有值
                if (data.Properties.All.TryGetValue(req.BindingKey, out var val))
                {
                    if (val is string markerId && !string.IsNullOrEmpty(markerId))
                    {
                        referencedMarkerIds.Add(markerId);

                        // 检查标记是否存在于场景
                        if (!markerById.TryGetValue(markerId, out var marker))
                        {
                            report.Add(
                                ValidationEntry.Severity.Warning,
                                $"节点 [{actionDef.DisplayName}] 的绑定 '{req.DisplayName}' 引用的标记 ({markerId}) 在场景中不存在",
                                node.Id, markerId);
                        }
                        else
                        {
                            // 检查类型匹配
                            if (!string.Equals(marker.MarkerTypeId, req.MarkerTypeId, System.StringComparison.Ordinal))
                            {
                                report.Add(
                                    ValidationEntry.Severity.Warning,
                                    $"节点 [{actionDef.DisplayName}] 的绑定 '{req.DisplayName}' " +
                                    $"期望 {req.MarkerTypeId} 类型标记，但绑定的是 {marker.MarkerTypeId} 类型",
                                    node.Id, markerId);
                            }
                        }
                    }
                    else if (req.Required)
                    {
                        // 值为空但标记是必需的
                        report.Add(
                            ValidationEntry.Severity.Error,
                            $"节点 [{actionDef.DisplayName}] 缺少必需绑定: {req.DisplayName}",
                            node.Id);
                    }
                }
                else if (req.Required)
                {
                    // 属性不存在且标记是必需的
                    report.Add(
                        ValidationEntry.Severity.Error,
                        $"节点 [{actionDef.DisplayName}] 缺少必需绑定: {req.DisplayName}",
                        node.Id);
                }
            }
        }

        /// <summary>检查蓝图中引用的 MarkerId 是否在场景中存在</summary>
        private static void CheckMissingMarkers(
            ValidationReport report,
            Graph graph,
            Dictionary<string, SceneMarker> markerById)
        {
            foreach (var node in graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;

                foreach (var kvp in data.Properties.All)
                {
                    if (kvp.Value is string strVal && !string.IsNullOrEmpty(strVal))
                    {
                        // 简单启发式：如果值看起来像 MarkerId（12 位 hex）但场景中找不到
                        if (strVal.Length == 12 && IsHexString(strVal) && !markerById.ContainsKey(strVal))
                        {
                            report.Add(
                                ValidationEntry.Severity.Warning,
                                $"节点属性 '{kvp.Key}' 引用的标记 ({strVal}) 在场景中不存在（可能已被删除）",
                                node.Id, strVal);
                        }
                    }
                }
            }
        }

        /// <summary>检查场景中未被蓝图引用的孤立标记</summary>
        private static void CheckOrphanedMarkers(
            ValidationReport report,
            SceneMarker[] sceneMarkers,
            HashSet<string> referencedMarkerIds)
        {
            foreach (var marker in sceneMarkers)
            {
                if (string.IsNullOrEmpty(marker.MarkerId)) continue;

                if (!referencedMarkerIds.Contains(marker.MarkerId))
                {
                    report.Add(
                        ValidationEntry.Severity.Info,
                        $"场景标记 [{marker.GetDisplayLabel()}] ({marker.MarkerId}) 未被任何蓝图节点引用",
                        null, marker.MarkerId);
                }
            }
        }

        private static bool IsHexString(string s)
        {
            foreach (char c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 将验证报告输出到 Console。
        /// </summary>
        public static void LogReport(ValidationReport report)
        {
            if (!report.HasIssues)
            {
                SBLog.Info(SBLogTags.Validator, "绑定一致性检查通过，未发现问题");
                return;
            }

            foreach (var entry in report.Entries)
            {
                switch (entry.Level)
                {
                    case ValidationEntry.Severity.Error:
                        SBLog.Error(SBLogTags.Validator, entry.Message);
                        break;
                    case ValidationEntry.Severity.Warning:
                        SBLog.Warn(SBLogTags.Validator, entry.Message);
                        break;
                    case ValidationEntry.Severity.Info:
                        SBLog.Info(SBLogTags.Validator, entry.Message);
                        break;
                }
            }

            SBLog.Info(SBLogTags.Validator, $"汇总: {report.ErrorCount} 错误, {report.WarningCount} 警告, {report.InfoCount} 提示");
        }
    }
}
