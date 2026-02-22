#nullable enable
using System.Collections.Generic;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Runtime.Markers;
using UnityEngine;

namespace SceneBlueprint.Editor.WindowServices
{
    /// <summary>
    /// 选中节点 → MarkerId 映射服务。
    /// 将原 OnBlueprintSelectionChanged 中的内联逻辑提取为可独立测试的服务。
    /// <para>
    /// 维护 nodeId → markerIds 投影缓存；命令执行或图结构变化后调用
    /// <see cref="InvalidateCache"/> 使缓存失效，下次 Resolve 时重建。
    /// </para>
    /// </summary>
    public sealed class SelectionMarkerResolver
    {
        private readonly IBlueprintReadContext _ctx;
        private readonly BindingContext        _bindingContext;

        // nodeId → List<markerId>（仅缓存上次 Resolve 的结果）
        private readonly Dictionary<string, List<string>> _nodeToMarkers = new();
        private bool _cacheDirty = true;

        public SelectionMarkerResolver(IBlueprintReadContext ctx, BindingContext bindingContext)
        {
            _ctx            = ctx;
            _bindingContext = bindingContext;
        }

        /// <summary>使缓存失效（命令执行、Undo、图结构变化时调用）</summary>
        public void InvalidateCache() => _cacheDirty = true;

        /// <summary>
        /// 将选中节点 ID 集合解析为对应的场景 MarkerId 列表。
        /// 内部维护投影缓存，同一批选中节点只解析一次。
        /// </summary>
        public IReadOnlyList<string> Resolve(IEnumerable<string> selectedNodeIds)
        {
            var vm = _ctx.ViewModel;
            if (vm == null) return System.Array.Empty<string>();

            if (_cacheDirty)
            {
                RebuildCache(vm.Graph, _ctx.ActionRegistry);
                _cacheDirty = false;
            }

            var result = new List<string>();
            foreach (var nodeId in selectedNodeIds)
            {
                if (_nodeToMarkers.TryGetValue(nodeId, out var mids))
                    result.AddRange(mids);
            }
            return result;
        }

        // ── 缓存重建 ──

        private void RebuildCache(NodeGraph.Core.Graph graph, ActionRegistry registry)
        {
            _nodeToMarkers.Clear();
            foreach (var node in graph.Nodes)
            {
                var mids = CollectMarkerIdsForNode(node, registry);
                if (mids.Count > 0)
                    _nodeToMarkers[node.Id] = mids;
            }
        }

        private List<string> CollectMarkerIdsForNode(NodeGraph.Core.Node node, ActionRegistry registry)
        {
            var result = new List<string>();
            if (node.UserData is not ActionNodeData data) return result;
            if (!registry.TryGet(data.ActionTypeId, out var actionDef)) return result;

            foreach (var prop in actionDef.Properties)
            {
                if (prop.SceneBindingType == null) continue;

                string scopedKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                var boundObj     = ResolveBindingObject(scopedKey, data, prop.Key);
                if (boundObj == null) continue;

                var markerComp = boundObj.GetComponent<SceneMarker>();
                if (markerComp != null && !string.IsNullOrEmpty(markerComp.MarkerId))
                    result.Add(markerComp.MarkerId);
            }
            return result;
        }

        private GameObject? ResolveBindingObject(string scopedKey, ActionNodeData data, string propKey)
        {
            // 优先从运行时绑定上下文取
            var obj = _bindingContext.Get(scopedKey);
            if (obj != null) return obj;

            // 降级：用属性中存储的 MarkerId 在场景中反查
            var storedId = data.Properties.Get<string>(propKey);
            if (string.IsNullOrEmpty(storedId)) return null;

            var marker = SceneMarkerSelectionBridge.FindMarkerInScene(storedId);
            if (marker != null)
            {
                _bindingContext.Set(scopedKey, marker.gameObject);
                return marker.gameObject;
            }
            return null;
        }
    }
}
