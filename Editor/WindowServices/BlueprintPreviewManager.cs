#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Pipeline;
using PreviewEngine = SceneBlueprint.Editor.Preview.BlueprintPreviewManager;
using SceneBlueprint.Editor.Session;
using SceneBlueprint.Runtime.Markers;
using UnityEditor;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 节点预览调度器（从 SceneBlueprintWindow 提取）。
    /// 负责维护脏集合、MarkerId↔NodeId 索引、图形状快照，并通过
    /// EditorApplication.delayCall 调度合批刷新，实际渲染委托给
    /// <see cref="Preview.BlueprintPreviewManager"/>（预览计算引擎单例）。
    /// </summary>
    public class NodePreviewScheduler : ISessionService
    {
        // ── 依赖 ──
        private readonly IBlueprintReadContext _read;
        private readonly IBlueprintUIContext   _ui;
        private readonly Func<string>          _getPreviewContextId;
        private readonly PreviewEngine         _previewEngine;

        // ── 状态跟踪器（脏队列 + 索引 + 快照） ──
        private readonly PreviewStateTracker _tracker = new PreviewStateTracker();
        private bool _flushScheduled;

        // ── 构造 ──

        public NodePreviewScheduler(
            IBlueprintReadContext read,
            IBlueprintUIContext   ui,
            Func<string>          getPreviewContextId,
            PreviewEngine         previewEngine)
        {
            _read                = read;
            _ui                  = ui;
            _getPreviewContextId = getPreviewContextId;
            _previewEngine       = previewEngine;
            // D-Preview: 自订阅命令历史变化，任何指令执行后自动完成结构快照同步
            var vm0 = read.ViewModel;
            if (vm0 != null) vm0.Commands.OnHistoryChanged += OnCommandHistoryChanged;
        }

        void ISessionService.OnSessionDisposed()
        {
            var vm = _read.ViewModel;
            if (vm != null) vm.Commands.OnHistoryChanged -= OnCommandHistoryChanged;
            ResetState();
        }

        private void OnCommandHistoryChanged()
        {
            DetectGraphShapeChange();
        }

        // ── 脏标记 API ──

        public void MarkDirtyAll(string reason)
        {
            if (_tracker.MarkAll())
            {
                SBLog.Debug(SBLogTags.Pipeline, "NodePreviewScheduler.MarkDirtyAll: {0}", reason);
                ScheduleFlush();
            }
        }

        public void MarkDirtyForNode(string nodeId, string reason)
        {
            if (!string.IsNullOrEmpty(nodeId)) MarkDirtyForNodes(new[] { nodeId }, reason);
        }

        public void MarkDirtyForNodes(IEnumerable<string> nodeIds, string reason)
        {
            int added = _tracker.MarkNodes(nodeIds);
            if (added > 0)
            {
                SBLog.Debug(SBLogTags.Pipeline,
                    "NodePreviewScheduler.MarkDirtyForNodes: added={0}, reason={1}", added, reason);
                ScheduleFlush();
            }
        }

        public int MarkDirtyForNodesByAreaMarkerIds(IEnumerable<string> markerIds, string reason)
        {
            if (_read.ViewModel == null) return 0;
            var mids = markerIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (mids.Count == 0) return 0;
            var nids = _tracker.GetNodesByMarkerIds(mids);
            if (nids.Count == 0) { RebuildMarkerNodeIndex(); nids = _tracker.GetNodesByMarkerIds(mids); }
            MarkDirtyForNodes(nids, reason);
            return nids.Count;
        }

        public int MarkDirtyForAllRandomAreaNodes(string reason)
        {
            var vm = _read.ViewModel;
            if (vm == null) return 0;
            var ids = vm.Graph.Nodes
                .Where(n => (n.UserData as ActionNodeData)?.ActionTypeId == "Location.RandomArea")
                .Select(n => n.Id).ToList();
            MarkDirtyForNodes(ids, reason);
            return ids.Count;
        }

        public int MarkDirtyForUncachedRandomAreaNodes(string reason)
        {
            var vm = _read.ViewModel;
            if (vm == null) return 0;
            var cached = new HashSet<string>(
                _previewEngine
                    .GetCurrentBlueprintPreviews().Select(p => p.NodeId));
            var ids = vm.Graph.Nodes
                .Where(n => (n.UserData as ActionNodeData)?.ActionTypeId == "Location.RandomArea"
                         && !cached.Contains(n.Id))
                .Select(n => n.Id).ToList();
            MarkDirtyForNodes(ids, reason);
            return ids.Count;
        }

        // ══ 调度 & 刷新 ══

        /// <summary>刷新计划：封装一次 flush 的意图，隔离脏状态消费与引擎调用。</summary>
        private sealed class FlushPlan
        {
            public readonly bool   DoAll;
            public readonly IReadOnlyList<string> DirtyNodeIds;
            public readonly string ContextId;
            public bool IsEmpty => !DoAll && DirtyNodeIds.Count == 0;

            public FlushPlan(bool doAll, IReadOnlyList<string> dirtyNodeIds, string contextId)
            { DoAll = doAll; DirtyNodeIds = dirtyNodeIds; ContextId = contextId; }
        }

        public void ScheduleFlush()
        {
            if (_flushScheduled) return;
            _flushScheduled = true;
            EditorApplication.delayCall -= FlushDirtyPreviews;
            EditorApplication.delayCall += FlushDirtyPreviews;
        }

        private void FlushDirtyPreviews()
        {
            EditorApplication.delayCall -= FlushDirtyPreviews;
            _flushScheduled = false;

            var vm = _read.ViewModel;
            if (vm == null) { _tracker.ConsumeDirty(); return; }

            // 阶段1: 消费脏状态 → 构建刷新计划
            var plan = BuildFlushPlan();
            if (plan.IsEmpty) return;

            // 阶段2: 执行刷新（调用预览引擎）
            bool anyFlushed = ExecuteFlush(vm.Graph, plan);

            // 阶段3: 后置状态同步（签名快照 + SceneView 重绘）
            if (anyFlushed) PostFlushSync(plan.DoAll);
        }

        /// <summary>阶段1: 消费脏标记，返回刷新计划。</summary>
        private FlushPlan BuildFlushPlan()
        {
            var (doAll, snapshot) = _tracker.ConsumeDirty();
            return new FlushPlan(doAll, snapshot, _getPreviewContextId());
        }

        /// <summary>阶段2: 根据计划调用预览引擎。返回是否实际触发了刺激。</summary>
        private bool ExecuteFlush(Graph graph, FlushPlan plan)
        {
            MarkerCache.SetDirty();

            if (plan.DoAll)
            {
                _previewEngine.RefreshAllPreviews(plan.ContextId, graph);
                SBLog.Debug(SBLogTags.Pipeline,
                    "NodePreviewScheduler.Flush: 全量, nodeCount={0}", graph.Nodes.Count);
                return true;
            }

            int refreshed = 0;
            var toRemove  = new List<string>();
            foreach (var nid in plan.DirtyNodeIds)
            {
                var node = graph.FindNode(nid);
                if (node?.UserData is ActionNodeData nd)
                {
                    _previewEngine.RefreshPreviewForNode(plan.ContextId, nid, nd);
                    refreshed++;
                }
                else { toRemove.Add(nid); }
            }

            int removedCnt = toRemove.Count > 0
                ? _previewEngine.RemovePreviews(toRemove, repaint: false)
                : 0;

            SBLog.Debug(SBLogTags.Pipeline,
                "NodePreviewScheduler.Flush: 局部, refreshed={0}, removed={1}", refreshed, removedCnt);
            return refreshed > 0 || removedCnt > 0;
        }

        /// <summary>阶段3: 同步签名快照（局部刷新时额外触发 SceneView 重绘）。</summary>
        private void PostFlushSync(bool wasFullRefresh)
        {
            SyncMarkerSignatureSnapshot();
            if (!wasFullRefresh) UnityEditor.SceneView.RepaintAll();
        }

        // ── 标记签名快照 ──

        public void SyncMarkerSignatureSnapshot()
        {
            var mids = _previewEngine.GetCurrentPreviewMarkerIds();
            if (mids.Count == 0) return;
            _tracker.SyncMarkerSignatureSnapshot(ComputeSignatures(mids));
        }

        public List<string> CollectChangedPreviewMarkerIds(IReadOnlyCollection<string> previewMarkerIds)
            => _tracker.CollectChangedMarkerIds(ComputeSignatures(previewMarkerIds));

        // ── 图形状快照 ──

        public void SyncGraphShapeSnapshot(Graph graph) => _tracker.SyncShapeSnapshot(graph);

        /// <summary>
        /// 检测图结构变化（节点增删、子图数量变化）。
        /// 对新增节点更新索引并标脏；对删除节点清理索引并标脏；最后同步快照。
        /// 对应原 SceneBlueprintWindow.DetectPreviewGraphShapeChange。
        /// </summary>
        public void DetectGraphShapeChange()
        {
            var vm = _read.ViewModel;
            if (vm == null) return;
            var graph = vm.Graph;

            var (changed, added, removed) = _tracker.DetectShapeChange(graph);
            if (!changed) return;

            SBLog.Debug(SBLogTags.Pipeline,
                "NodePreviewScheduler.DetectGraphShapeChange: added={0}, removed={1}",
                added.Count, removed.Count);

            if (added.Count > 0)
            {
                foreach (var nid in added)
                    _tracker.UpdateIndex(nid, graph.FindNode(nid)?.UserData as ActionNodeData);
                MarkDirtyForNodes(added, "GraphShapeChanged.NodeAdded");
            }
            if (removed.Count > 0)
            {
                foreach (var nid in removed) _tracker.RemoveFromIndex(nid);
                MarkDirtyForNodes(removed, "GraphShapeChanged.NodeRemoved");
            }
            _tracker.SyncShapeSnapshot(graph);
        }

        // ── MarkerId ↔ NodeId 索引管理 ──

        /// <summary>数据变更通知（非命令路径的属性修改，如 Inspector 编辑）。</summary>
        public void NotifyNodeDataChanged(string nodeId, ActionNodeData? data)
            => _tracker.UpdateIndex(nodeId, data);

        public void RebuildMarkerNodeIndex()
        {
            var vm = _read.ViewModel;
            if (vm == null) { _tracker.Reset(); return; }
            _tracker.RebuildIndex(vm.Graph.Nodes);
        }

        // ── 重置 ──

        public void ResetState()
        {
            _flushScheduled = false;
            _tracker.Reset();
        }

        // ── 静态辅助 ──

        // ── 标记签名计算（有 Unity 依赖，不进入 PreviewStateTracker）──

        private static Dictionary<string, int> ComputeSignatures(IEnumerable<string> markerIds)
        {
            var lookup = BuildMarkerLookup();
            var result = new Dictionary<string, int>();
            foreach (var mid in markerIds)
            {
                result[mid] = lookup.TryGetValue(mid, out var m)
                    ? ComputeMarkerSignature(m)
                    : int.MinValue;
            }
            return result;
        }

        private static Dictionary<string, SceneMarker> BuildMarkerLookup()
        {
            var d = new Dictionary<string, SceneMarker>();
            foreach (var m in MarkerCache.GetAll())
                if (m != null && !string.IsNullOrEmpty(m.MarkerId)) d[m.MarkerId] = m;
            return d;
        }

        private static int ComputeMarkerSignature(SceneMarker marker)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (marker.MarkerTypeId?.GetHashCode() ?? 0);
                var pos = marker.transform.position;
                var rot = marker.transform.rotation;
                h = h * 31 + PreviewStateTracker.QuantizeComponents(pos.x, pos.y, pos.z);
                h = h * 31 + PreviewStateTracker.QuantizeComponents(rot.x, rot.y, rot.z);
                h = h * 31 + (int)(rot.w * 1000f);
                if (marker is AreaMarker am)
                {
                    h = h * 31 + (int)am.Shape;
                    h = h * 31 + PreviewStateTracker.QuantizeComponents(am.BoxSize.x, am.BoxSize.y, am.BoxSize.z);
                    h = h * 31 + (int)(am.Height * 1000f);
                    if (am.Vertices != null)
                    {
                        h = h * 31 + am.Vertices.Count;
                        foreach (var v in am.Vertices)
                            h = h * 31 + PreviewStateTracker.QuantizeComponents(v.x, v.y, v.z);
                    }
                }
                return h;
            }
        }
    }
}
