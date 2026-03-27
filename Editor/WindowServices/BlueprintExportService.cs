#nullable enable
using System;
using UnityEditor;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Settings;
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

        /// <summary>
        /// 当前蓝图资产的关卡 ID（从 BlueprintAsset.LevelId 读取）。
        /// </summary>
        private int LevelId => _read.CurrentAsset?.LevelId ?? 0;

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

            string bpName = asset != null ? asset.BlueprintName : "SceneBlueprint";
            string? bpId  = asset?.BlueprintId;

            var exportOptions = new BlueprintExporter.ExportOptions
            {
                AdapterType = _read.GetAdapterType()
            };

            // 将业务侧导出设置写入 UserData，供 IExportEnricher 读取
            if (LevelId > 0)
            {
                exportOptions.UserData[ActionCompilationUserDataKeys.LevelId] = LevelId;
                exportOptions.UserData[ActionCompilationUserDataKeys.MonsterMappingSnapshot] = SceneBlueprintSettingsService.MonsterMapping;
            }

            var result = BlueprintExporter.Export(
                viewModel.Graph, registry, sceneBindings,
                blueprintId:   bpId,
                blueprintName: bpName,
                options:       exportOptions,
                variables:     asset?.Variables);

            var validationSummary = ValidationMessagePresentation.BuildSummary(result.Messages);
            foreach (var msg in result.Messages)
            {
                switch (msg.Level)
                {
                    case Error:   SBLog.Error(SBLogTags.Export, msg.Message); break;
                    case Warning: SBLog.Warn(SBLogTags.Export, msg.Message);  break;
                    default:      SBLog.Info(SBLogTags.Export, msg.Message);  break;
                }
            }

            if (validationSummary.ErrorCount > 0)
            {
                EditorUtility.DisplayDialog("编译失败，无法导出",
                    ValidationMessagePresentation.BuildDialogMessage(
                        "导出阶段发现阻断问题，当前无法继续写出蓝图 JSON。",
                        result.Messages),
                    "确定");
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

            string successMsg = report.HasWarnings || validationSummary.WarningCount > 0
                ? $"蓝图已导出到:\n{path}\n\n分析警告 {report.WarningCount} 条，导出警告 {validationSummary.WarningCount} 条。"
                : $"蓝图已导出到:\n{path}\n\n行动数: {result.Data.Actions.Length}\n过渡数: {result.Data.Transitions.Length}";
            EditorUtility.DisplayDialog(
                report.HasWarnings || validationSummary.WarningCount > 0 ? "导出完成（有警告）" : "导出成功",
                successMsg, "确定");

            SBLog.Info(SBLogTags.Export, $"蓝图已导出到: {path} " +
                $"(行动数: {result.Data.Actions.Length}, 过渡数: {result.Data.Transitions.Length}, " +
                $"绑定数: {boundBindings}/{totalBindings})");
        }
    }
}
