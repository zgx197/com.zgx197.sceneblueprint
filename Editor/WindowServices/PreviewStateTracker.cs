#nullable enable
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Core;
using PreviewDirtyIndex = SceneBlueprint.Editor.WindowServices.Preview.PreviewDirtyIndex;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 预览状态跟踪器——纯数据层，无调度副作用。
    /// 封装脏标记队列、MarkerId↔NodeId 索引、图形状快照、标记签名快照四组状态，
    /// 供 <see cref="NodePreviewScheduler"/> 在调度层之上统一访问。
    /// </summary>
    public sealed class PreviewStateTracker
    {
        // ── 脏标记队列 ──
        private bool                   _dirtyAll;
        private readonly HashSet<string> _dirtyNodeIds = new();

        // ── MarkerId ↔ NodeId 双向索引 ──
        private readonly PreviewDirtyIndex _index = new PreviewDirtyIndex();

        // ── 图形状快照 ──
        private readonly HashSet<string> _observedNodeIds          = new();
        private int                      _observedSubGraphCount    = -1;

        // ── 标记签名快照 ──
        private readonly Dictionary<string, int> _observedMarkerSignatures = new();

        // ═══════════════════════════════════════
        //  脏标记
        // ═══════════════════════════════════════

        public bool IsDirtyAll => _dirtyAll;

        /// <summary>标记全量脏。返回 true 表示状态有变化。</summary>
        public bool MarkAll()
        {
            if (_dirtyAll) return false;
            _dirtyAll = true;
            _dirtyNodeIds.Clear();
            return true;
        }

        /// <summary>追加节点 ID 到脏集合。返回实际新增数量。</summary>
        public int MarkNodes(IEnumerable<string> nodeIds)
        {
            if (_dirtyAll) return 0;
            int count = 0;
            foreach (var id in nodeIds)
                if (!string.IsNullOrEmpty(id) && _dirtyNodeIds.Add(id)) count++;
            return count;
        }

        /// <summary>消费并清空当前脏状态。返回 (isAll, nodeIdSnapshot)。</summary>
        public (bool isAll, List<string> nodeIds) ConsumeDirty()
        {
            bool all     = _dirtyAll;
            var  nodes   = new List<string>(_dirtyNodeIds);
            _dirtyAll = false;
            _dirtyNodeIds.Clear();
            return (all, nodes);
        }

        // ═══════════════════════════════════════
        //  MarkerId ↔ NodeId 索引
        // ═══════════════════════════════════════

        public void UpdateIndex(string nodeId, ActionNodeData? data)
        {
            string? markerId = null;
            if (data != null && TryGetRandomAreaMarkerId(data, out var mid)) markerId = mid;
            _index.Update(nodeId, markerId);
        }

        public void RemoveFromIndex(string nodeId) => _index.Remove(nodeId);

        public void RebuildIndex(IEnumerable<Node> nodes)
        {
            _index.Rebuild(nodes.Select(n =>
            {
                string? mid = null;
                if (n.UserData is ActionNodeData d && TryGetRandomAreaMarkerId(d, out var m)) mid = m;
                return (n.Id, mid);
            }));
        }

        public List<string> GetNodesByMarkerIds(IReadOnlyCollection<string> markerIds)
        {
            var mids = new HashSet<string>(markerIds.Where(id => !string.IsNullOrEmpty(id)));
            if (mids.Count == 0) return new List<string>();
            return new List<string>(_index.GetNodesByMarkerIds(mids));
        }

        // ═══════════════════════════════════════
        //  图形状快照
        // ═══════════════════════════════════════

        public bool HasShapeSnapshot => _observedSubGraphCount >= 0;

        public void SyncShapeSnapshot(Graph graph)
        {
            _observedNodeIds.Clear();
            foreach (var n in graph.Nodes) _observedNodeIds.Add(n.Id);
            _observedSubGraphCount = graph.SubGraphFrames.Count;
        }

        /// <summary>
        /// 检测图结构变化。返回 (changed, added, removed)。
        /// 若首次调用（无快照）则同步快照并返回 changed=false。
        /// </summary>
        public (bool changed, List<string> added, List<string> removed) DetectShapeChange(Graph graph)
        {
            var empty = new List<string>();
            if (!HasShapeSnapshot)
            {
                SyncShapeSnapshot(graph);
                return (false, empty, empty);
            }

            bool subChanged = graph.SubGraphFrames.Count != _observedSubGraphCount;
            if (!subChanged && graph.Nodes.Count == _observedNodeIds.Count)
                return (false, empty, empty);

            var currentIds = new HashSet<string>(graph.Nodes.Select(n => n.Id));
            var removed    = _observedNodeIds.Where(id => !currentIds.Contains(id)).ToList();
            var added      = currentIds.Where(id => !_observedNodeIds.Contains(id)).ToList();

            bool changed = subChanged || removed.Count > 0 || added.Count > 0;
            return (changed, added, removed);
        }

        // ═══════════════════════════════════════
        //  标记签名快照
        // ═══════════════════════════════════════

        /// <summary>
        /// 同步标记签名快照。
        /// <paramref name="signatures"/> 由外部（有 Unity 依赖的调度层）预先计算后传入。
        /// </summary>
        public void SyncMarkerSignatureSnapshot(IReadOnlyDictionary<string, int> signatures)
        {
            _observedMarkerSignatures.Clear();
            foreach (var kv in signatures)
                _observedMarkerSignatures[kv.Key] = kv.Value;
        }

        /// <summary>
        /// 对比当前签名与快照，返回签名发生变化的 MarkerId 列表。
        /// <paramref name="currentSignatures"/> 由外部计算后传入。
        /// </summary>
        public List<string> CollectChangedMarkerIds(IReadOnlyDictionary<string, int> currentSignatures)
        {
            var changed = new List<string>();
            foreach (var kv in currentSignatures)
            {
                if (!_observedMarkerSignatures.TryGetValue(kv.Key, out int prev) || prev != kv.Value)
                    changed.Add(kv.Key);
            }
            return changed;
        }

        // ═══════════════════════════════════════
        //  重置
        // ═══════════════════════════════════════

        public void Reset()
        {
            _dirtyAll = false;
            _dirtyNodeIds.Clear();
            _index.Clear();
            _observedNodeIds.Clear();
            _observedMarkerSignatures.Clear();
            _observedSubGraphCount = -1;
        }

        // ═══════════════════════════════════════
        //  静态辅助（纯 C#，无 Unity 依赖）
        // ═══════════════════════════════════════

        internal static bool TryGetRandomAreaMarkerId(ActionNodeData data, out string markerId)
        {
            markerId = "";
            if (data.ActionTypeId != "Location.RandomArea") return false;
            markerId = data.Properties.Get<string>("area") ?? "";
            return !string.IsNullOrEmpty(markerId);
        }

        /// <summary>
        /// 量化三分量为哈希整数。由外部（NodePreviewScheduler）展开 Vector3 后调用。
        /// </summary>
        internal static int QuantizeComponents(float x, float y, float z)
        {
            unchecked
            {
                return ((int)(x * 1000f) * 31
                      + (int)(y * 1000f)) * 31
                      + (int)(z * 1000f);
            }
        }
    }
}
