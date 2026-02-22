#nullable enable
using System;
using UnityEditor;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.WindowServices.Binding;
using SceneBlueprint.Runtime;
using static SceneBlueprint.Editor.Export.ValidationLevel;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 蓝图导出服务——仅负责三阶段导出（Analyze → Compile → Emit）。
    /// 注入最小接口，与 Window 完全解耦。
    /// </summary>
    public sealed class BlueprintExportService
    {
        private readonly IBlueprintReadContext      _read;
        private readonly IBlueprintUIContext        _ui;
        private readonly ISceneBindingCollector     _collector;
        private readonly BlueprintAnalysisController _analysis;
        private readonly Action<string>             _onExportSucceeded;

        public BlueprintExportService(
            IBlueprintReadContext      read,
            IBlueprintUIContext        ui,
            ISceneBindingCollector     collector,
            BlueprintAnalysisController analysis,
            Action<string>             onExportSucceeded)
        {
            _read              = read;
            _ui                = ui;
            _collector         = collector;
            _analysis          = analysis;
            _onExportSucceeded = onExportSucceeded;
        }

        public void ExportBlueprint()
        {
            var viewModel = _read.ViewModel;
            if (viewModel == null) return;

            // Phase 1: Analyze
            var report = _analysis.ForceRunNow();
            if (report.HasErrors)
            {
                _ui.EnsureWorkbenchVisible();
                EditorUtility.DisplayDialog("分析失败，无法导出",
                    $"蓝图存在 {report.ErrorCount} 个错误，请查看工作台分析面板或 Console 日志。",
                    "确定");
                return;
            }
            if (report.HasWarnings)
                SBLog.Warn(SBLogTags.Export, $"蓝图存在 {report.WarningCount} 条警告，已继续导出。");

            // Phase 2-3: Compile + Emit
            var registry      = _read.ActionRegistry;
            var sceneBindings = _collector.CollectForExport();
            var asset         = _read.CurrentAsset;

            string bpName = asset != null ? asset.BlueprintName : "场景蓝图";
            string? bpId  = asset?.BlueprintId;

            var exportOptions = new BlueprintExporter.ExportOptions
            {
                AdapterType = _read.GetAdapterType()
            };

            var result = BlueprintExporter.Export(
                viewModel.Graph, registry, sceneBindings,
                blueprintId:   bpId,
                blueprintName: bpName,
                options:       exportOptions,
                variables:     asset?.Variables);

            int exportErrorCount = 0;
            foreach (var msg in result.Messages)
            {
                switch (msg.Level)
                {
                    case Error:   SBLog.Error(SBLogTags.Export, msg.Message); exportErrorCount++; break;
                    case Warning: SBLog.Warn(SBLogTags.Export, msg.Message);  break;
                    default:      SBLog.Info(SBLogTags.Export, msg.Message);  break;
                }
            }

            if (exportErrorCount > 0)
            {
                EditorUtility.DisplayDialog("编译失败，无法导出",
                    $"导出器转换时产生 {exportErrorCount} 个错误，请查看 Console 日志。", "确定");
                return;
            }

            var json        = BlueprintSerializer.ToJson(result.Data);
            string defaultName = string.IsNullOrEmpty(result.Data.BlueprintName)
                ? "blueprint" : result.Data.BlueprintName;
            string path = EditorUtility.SaveFilePanel("导出蓝图 JSON", "Assets", defaultName, "json");
            if (string.IsNullOrEmpty(path)) return;

            System.IO.File.WriteAllText(path, json, System.Text.Encoding.UTF8);

            int totalBindings = 0, boundBindings = 0;
            foreach (var a in result.Data.Actions)
                foreach (var sb in a.SceneBindings)
                { totalBindings++; if (!string.IsNullOrEmpty(sb.StableObjectId)) boundBindings++; }

            string exportTime = System.DateTime.Now.ToString("HH:mm:ss");
            _onExportSucceeded(exportTime);

            string successMsg = report.HasWarnings
                ? $"蓝图已导出（{report.WarningCount} 条警告）：\n{path}"
                : $"蓝图已导出到:\n{path}\n\n行动数: {result.Data.Actions.Length}\n过渡数: {result.Data.Transitions.Length}";
            EditorUtility.DisplayDialog(
                report.HasWarnings ? "导出完成（有警告）" : "导出成功",
                successMsg, "确定");

            SBLog.Info(SBLogTags.Export, $"蓝图已导出到: {path} " +
                $"(行动数: {result.Data.Actions.Length}, 过渡数: {result.Data.Transitions.Length}, " +
                $"绑定数: {boundBindings}/{totalBindings})");
        }
    }
}
