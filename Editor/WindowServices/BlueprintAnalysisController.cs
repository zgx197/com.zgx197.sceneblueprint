#nullable enable
using System;
using System.Collections.Generic;
using NodeGraph.Math;
using NodeGraph.View;
using SceneBlueprint.Core;
using SceneBlueprint.Editor;
using SceneBlueprint.Editor.Session;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using UnityEditor;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 蓝图分析控制器。
    /// <para>
    /// 职责（从 SceneBlueprintWindow 提取）：
    /// - 防抖调度（Debounce）：图变化后延迟 0.6s 才触发分析，避免高频重分析
    /// - 执行 BlueprintAnalyzer.Analyze()，缓存 LastReport
    /// - 将分析结果映射为节点覆盖颜色写入 ViewModel
    /// - 执行标记绑定一致性验证（RunBindingValidation）
    /// </para>
    /// </summary>
    public class BlueprintAnalysisController : IDisposable, ISessionService
    {
        // ── 常量 ──
        private const double DebounceSeconds = 0.6;

        // ── 依赖 ──
        private readonly IBlueprintReadContext   _read;
        private readonly IBlueprintUIContext     _ui;
        private readonly Func<BlueprintProfile?> _getProfile;

        // ── 状态 ──
        private double _debounceUntil;
        private bool   _pollHooked;

        // ── 结果 ──

        /// <summary>最近一次分析报告（null 表示尚未运行）</summary>
        public AnalysisReport? LastReport { get; private set; }

        // ── 构造 ──

        /// <param name="ctx">窗口上下文</param>
        /// <param name="getProfile">
        /// 获取 BlueprintProfile 的委托（Profile 在 InitializeWithGraph 后才存在，需延迟求值）
        /// </param>
        public BlueprintAnalysisController(
            IBlueprintReadContext  read,
            IBlueprintUIContext    ui,
            Func<BlueprintProfile?> getProfile)
        {
            _read       = read;
            _ui         = ui;
            _getProfile = getProfile;
        }

        // ── 公开 API ──

        /// <summary>
        /// 调度一次防抖分析。图结构变化（命令执行、Undo/Redo）后调用。
        /// 每次调用都刷新截止时间；到期后由 PollDebounce 触发。
        /// </summary>
        public void Schedule()
        {
            _debounceUntil = EditorApplication.timeSinceStartup + DebounceSeconds;
            if (!_pollHooked)
            {
                EditorApplication.update += PollDebounce;
                _pollHooked = true;
            }
        }

        /// <summary>
        /// 立即执行分析并返回报告（跳过防抖）。
        /// 适用于：蓝图加载后的首次分析、导出前的强制校验。
        /// </summary>
        public AnalysisReport ForceRunNow()
        {
            // 如果防抖回调还挂着，先摘掉，避免重复触发
            if (_pollHooked)
            {
                EditorApplication.update -= PollDebounce;
                _pollHooked = false;
            }
            return RunAnalysis();
        }

        /// <summary>
        /// 执行标记绑定一致性验证并将结果输出到 Console。
        /// 在蓝图加载后自动调用，检查缺失/孤立/类型不匹配的标记绑定。
        /// </summary>
        public ValidationReport RunBindingValidation()
        {
            var vm = _read.ViewModel;
            if (vm == null) return new ValidationReport();

            var report = MarkerBindingValidator.Validate(vm.Graph, _read.ActionRegistry);
            MarkerBindingValidator.LogReport(report);
            return report;
        }

        // ── IDisposable ──

        void ISessionService.OnSessionDisposed() => Dispose();

        /// <summary>反注册 EditorApplication.update，防止窗口关闭后野指针回调</summary>
        public void Dispose()
        {
            if (_pollHooked)
            {
                EditorApplication.update -= PollDebounce;
                _pollHooked = false;
            }
        }

        // ── 内部 ──

        /// <summary>挂载在 EditorApplication.update，防抖到期后触发分析并自动反注册</summary>
        private void PollDebounce()
        {
            if (EditorApplication.timeSinceStartup < _debounceUntil) return;
            EditorApplication.update -= PollDebounce;
            _pollHooked = false;
            RunAnalysis();
        }

        /// <summary>执行分析、更新 LastReport、应用节点覆盖色、触发重绘</summary>
        private AnalysisReport RunAnalysis()
        {
            var vm      = _read.ViewModel;
            var profile = _getProfile();

            if (vm == null || profile == null)
            {
                LastReport = AnalysisReport.Empty;
                ApplyOverlayColors(LastReport);
                _ui.RequestRepaint();
                return LastReport;
            }

            var analyzer = SceneBlueprintProfile.CreateAnalyzer(
                profile.NodeTypes,
                _read.ActionRegistry);

            LastReport = analyzer.Analyze(vm.Graph);

            foreach (var d in LastReport.Diagnostics)
            {
                switch (d.Severity)
                {
                    case DiagnosticSeverity.Error:   SBLog.Error(SBLogTags.Export, d.ToString()); break;
                    case DiagnosticSeverity.Warning: SBLog.Warn(SBLogTags.Export,  d.ToString()); break;
                    default:                         SBLog.Info(SBLogTags.Export,  d.ToString()); break;
                }
            }

            ApplyOverlayColors(LastReport);
            _ui.RequestRepaint();
            return LastReport;
        }

        /// <summary>将分析报告映射为节点覆盖颜色写入 ViewModel（Error=红，Warning=黄，无问题=清空）</summary>
        private void ApplyOverlayColors(AnalysisReport report)
        {
            var vm = _read.ViewModel;
            if (vm == null) return;

            if (report.Diagnostics.Count == 0)
            {
                vm.NodeOverlayColors = null;
                return;
            }

            var colors = new Dictionary<string, Color4>();
            foreach (var d in report.Diagnostics)
            {
                if (d.NodeId == null) continue;
                var color = d.Severity == DiagnosticSeverity.Error
                    ? new Color4(0.95f, 0.2f, 0.2f, 1f)
                    : new Color4(1f, 0.75f, 0.1f, 1f);
                // Error 优先：覆盖已有的 Warning 色
                if (!colors.ContainsKey(d.NodeId) || d.Severity == DiagnosticSeverity.Error)
                    colors[d.NodeId] = color;
            }
            vm.NodeOverlayColors = colors.Count > 0 ? colors : null;
        }
    }
}

