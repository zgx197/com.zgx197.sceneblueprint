#nullable enable
using System.Collections.Generic;
using NodeGraph.Commands;
using NodeGraph.Core;
using NodeGraph.Math;
using SceneBlueprint.Editor;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Session;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 子蓝图操作控制器——集中管理所有 SubGraphFrame 相关的命令调度。
    /// <para>
    /// 将原先散落在 <c>BlueprintEditorSession</c>（CollapseAll、Create）
    /// 和 <c>SceneBlueprintWindow.ContextMenu</c>（Toggle、Ungroup、Group）中的
    /// 子蓝图操作收拢至此，Session/Window 均通过本控制器发起操作。
    /// </para>
    /// </summary>
    internal sealed class SubGraphController : ISessionService
    {
        private readonly IBlueprintReadContext _read;
        private readonly IBlueprintUIContext   _ui;
        private readonly IWindowCallbacks      _callbacks;

        public SubGraphController(
            IBlueprintReadContext read,
            IBlueprintUIContext   ui,
            IWindowCallbacks      callbacks)
        {
            _read      = read;
            _ui        = ui;
            _callbacks = callbacks;
        }

        // ══ 批量操作 ══

        /// <summary>折叠或展开所有子蓝图框。</summary>
        public void CollapseAll(bool collapse)
        {
            var vm = _read.ViewModel;
            if (vm == null) return;
            foreach (var sgf in vm.Graph.SubGraphFrames)
            {
                if (sgf.IsCollapsed != collapse)
                    vm.Commands.Execute(new ToggleSubGraphCollapseCommand(sgf.Id));
            }
            _ui.RequestRepaint();
            _callbacks.Repaint();
        }

        // ══ 单帧操作 ══

        /// <summary>在画布中心创建空子蓝图框，使用 <see cref="SceneBlueprintProfile.DefaultSubGraphBoundaryPorts"/>。</summary>
        public void CreateEmpty(string title)
        {
            if (string.IsNullOrEmpty(title)) return;
            var vm = _read.ViewModel;
            if (vm == null) return;

            var winSize      = _callbacks.GetWindowSize();
            var canvasCenter = new Vec2(
                (vm.PanOffset.X * -1f + winSize.x / 2f) / vm.ZoomLevel,
                (vm.PanOffset.Y * -1f + winSize.y / 2f) / vm.ZoomLevel);

            vm.Commands.Execute(new CreateSubGraphCommand(
                new Graph(new GraphSettings()),
                title,
                canvasCenter,
                SceneBlueprintProfile.DefaultSubGraphBoundaryPorts));

            _ui.RequestRepaint();
            _callbacks.Repaint();
            SBLog.Info(SBLogTags.Blueprint, $"已创建子蓝图: {title}");
        }

        /// <summary>将选中节点打包成子蓝图。</summary>
        public void GroupSelected(string title, IReadOnlyList<string> selectedNodeIds)
        {
            if (string.IsNullOrEmpty(title) || selectedNodeIds.Count == 0) return;
            var vm = _read.ViewModel;
            if (vm == null) return;

            vm.Commands.Execute(new GroupNodesCommand(title, new List<string>(selectedNodeIds)));
            vm.Selection.ClearSelection();
            _ui.RequestRepaint();
            _callbacks.Repaint();
            SBLog.Info(SBLogTags.Blueprint, $"已创建子蓝图: {title}（包含 {selectedNodeIds.Count} 个节点）");
        }

        /// <summary>切换单个子蓝图的折叠状态。</summary>
        public void Toggle(string frameId)
        {
            var vm = _read.ViewModel;
            if (vm == null) return;
            vm.Commands.Execute(new ToggleSubGraphCollapseCommand(frameId));
            _ui.RequestRepaint();
            _callbacks.Repaint();
        }

        /// <summary>解散子蓝图（保留内部节点，移除 SubGraphFrame）。</summary>
        public void Ungroup(string frameId)
        {
            var vm = _read.ViewModel;
            if (vm == null) return;
            vm.Commands.Execute(new UngroupSubGraphCommand(frameId));
            vm.Selection.ClearSelection();
            _ui.RequestRepaint();
            _callbacks.Repaint();
        }

        // ── ISessionService ──
        void ISessionService.OnSessionDisposed() { }
    }
}
