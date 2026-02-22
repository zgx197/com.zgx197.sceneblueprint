#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Editor.WindowServices.Preview
{
    /// <summary>
    /// MarkerId ↔ NodeId 双向索引——纯 C# 逻辑，无 Unity 依赖，可独立单测。
    /// <para>
    /// 从 <see cref="NodePreviewScheduler"/> 提取，解耦索引维护逻辑与调度逻辑。
    /// 维护 "哪个 MarkerId 被哪些 NodeId 引用" 的双向映射，供脏标记判断使用。
    /// </para>
    /// </summary>
    public sealed class PreviewDirtyIndex
    {
        // markerId → {nodeId...}
        private readonly Dictionary<string, HashSet<string>> _markerToNodes = new();
        // nodeId → markerId
        private readonly Dictionary<string, string>          _nodeToMarker  = new();

        /// <summary>当前索引中的节点数量（用于调试/测试）</summary>
        public int NodeCount => _nodeToMarker.Count;

        /// <summary>当前索引中的 Marker 数量（用于调试/测试）</summary>
        public int MarkerCount => _markerToNodes.Count;

        // ── 索引更新 ──

        /// <summary>
        /// 注册或更新 nodeId → markerId 的映射。
        /// <paramref name="markerId"/> 为 null 或空字符串时仅删除旧映射。
        /// </summary>
        public void Update(string nodeId, string? markerId)
        {
            Remove(nodeId);
            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(markerId)) return;

            _nodeToMarker[nodeId] = markerId;
            if (!_markerToNodes.TryGetValue(markerId, out var set))
            {
                set = new HashSet<string>();
                _markerToNodes[markerId] = set;
            }
            set.Add(nodeId);
        }

        /// <summary>移除 nodeId 的全部映射</summary>
        public void Remove(string nodeId)
        {
            if (!_nodeToMarker.TryGetValue(nodeId, out var markerId)) return;
            _nodeToMarker.Remove(nodeId);
            if (_markerToNodes.TryGetValue(markerId, out var set))
            {
                set.Remove(nodeId);
                if (set.Count == 0) _markerToNodes.Remove(markerId);
            }
        }

        /// <summary>清空全部映射</summary>
        public void Clear()
        {
            _markerToNodes.Clear();
            _nodeToMarker.Clear();
        }

        /// <summary>批量重建索引（Replace 语义：先 Clear，再 Rebuild）</summary>
        public void Rebuild(IEnumerable<(string nodeId, string? markerId)> entries)
        {
            Clear();
            foreach (var (nodeId, markerId) in entries)
                Update(nodeId, markerId);
        }

        // ── 查询 ──

        /// <summary>通过 markerId 集合反查所有引用它们的 nodeId</summary>
        public HashSet<string> GetNodesByMarkerIds(IEnumerable<string> markerIds)
        {
            var result = new HashSet<string>();
            foreach (var mid in markerIds)
                if (_markerToNodes.TryGetValue(mid, out var set))
                    foreach (var nid in set) result.Add(nid);
            return result;
        }

        /// <summary>尝试获取 nodeId 绑定的 markerId</summary>
        public bool TryGetMarkerId(string nodeId, out string markerId)
            => _nodeToMarker.TryGetValue(nodeId, out markerId!);
    }
}
