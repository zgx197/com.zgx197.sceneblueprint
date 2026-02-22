#nullable enable
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Editor.WindowServices;
using SceneBlueprint.Runtime;
using UnityEngine;

namespace SceneBlueprint.Editor.WindowServices.Binding
{
    /// <summary>
    /// 场景绑定收集器——负责"图 → 外部"方向：
    /// <list type="bullet">
    /// <item><see cref="SyncToScene"/>：将 BindingContext 持久化到场景 SceneBlueprintManager</item>
    /// <item><see cref="CollectForExport"/>：收集导出用的 SceneBindingData 列表</item>
    /// </list>
    /// </summary>
    public class SceneBindingCollector : ISceneBindingCollector
    {
        private readonly IBlueprintReadContext _ctx;
        private readonly BindingContext        _bindingContext;
        private readonly ISceneBindingStore    _store;
        private IEditorSpatialModeDescriptor?  _spatialDescriptor;

        public SceneBindingCollector(
            IBlueprintReadContext ctx,
            BindingContext        bindingContext,
            ISceneBindingStore    store)
        {
            _ctx            = ctx;
            _bindingContext = bindingContext;
            _store          = store;
        }

        // ══════════════════════════════════════════
        //  公开 API
        // ══════════════════════════════════════════

        /// <summary>将当前绑定同步持久化到场景的 SceneBlueprintManager。</summary>
        public void SyncToScene()
        {
            var vm    = _ctx.ViewModel;
            var asset = _ctx.CurrentAsset;
            if (vm == null || asset == null) return;

            var graph    = vm.Graph;
            var registry = _ctx.ActionRegistry;
            var groups   = new List<SubGraphBindingGroup>();

            foreach (var sgf in graph.SubGraphFrames)
            {
                var group = new SubGraphBindingGroup
                {
                    SubGraphFrameId = sgf.Id,
                    SubGraphTitle   = sgf.Title
                };
                var seen = new HashSet<string>();
                foreach (var nodeId in sgf.ContainedNodeIds)
                {
                    var node = graph.FindNode(nodeId);
                    if (node?.UserData is not ActionNodeData ad) continue;
                    if (!registry.TryGet(ad.ActionTypeId, out var def)) continue;

                    foreach (var prop in def.Properties)
                    {
                        if (prop.Type != PropertyType.SceneBinding) continue;
                        string sk = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                        if (!seen.Add(sk)) continue;
                        group.Bindings.Add(new SceneBindingSlot
                        {
                            BindingKey         = sk,
                            BindingType        = prop.SceneBindingType ?? BindingType.Transform,
                            DisplayName        = prop.DisplayName,
                            SourceActionTypeId = ad.ActionTypeId,
                            BoundObject        = _bindingContext.Get(sk)
                        });
                    }
                }
                if (group.Bindings.Count > 0) groups.Add(group);
            }

            var topLevel = CollectTopLevelBindings(graph, registry);
            if (topLevel?.Bindings.Count > 0) groups.Add(topLevel);

            _store.SaveBindingGroups(asset, groups);

            int total = groups.Sum(g => g.Bindings.Count);
            int bound = groups.Sum(g => g.Bindings.Count(b => b.IsBound));
            SBLog.Info(SBLogTags.Binding,
                $"已同步到场景: 分组={groups.Count}, 绑定={bound}/{total}");
        }

        /// <summary>为导出收集绑定数据。优先读持久化存储，降级读 BindingContext 内存数据。</summary>
        public List<BlueprintExporter.SceneBindingData>? CollectForExport()
        {
            var asset    = _ctx.CurrentAsset;
            var registry = _ctx.ActionRegistry;

            if (asset != null
                && _store.TryLoadBindingGroups(asset, out var groups)
                && groups.Count > 0)
            {
                var list = new List<BlueprintExporter.SceneBindingData>();
                foreach (var g in groups)
                    foreach (var b in g.Bindings)
                    {
                        EncodeBinding(b.BoundObject, b.BindingType,
                            out var sid, out var at, out var spj);
                        list.Add(new BlueprintExporter.SceneBindingData
                        {
                            BindingKey         = b.BindingKey,
                            BindingType        = b.BindingType.ToString(),
                            StableObjectId     = sid,
                            AdapterType        = at,
                            SpatialPayloadJson = spj,
                            SourceSubGraph     = g.SubGraphTitle,
                            SourceActionTypeId = b.SourceActionTypeId
                        });
                    }
                return list.Count > 0 ? list : null;
            }

            if (_bindingContext.Count > 0)
            {
                var typeMap = BuildBindingTypeMap(registry);
                var list    = new List<BlueprintExporter.SceneBindingData>();
                foreach (var kvp in _bindingContext.All)
                {
                    string resolvedKey = ResolveBindingKey(kvp.Key, typeMap);
                    var    bindingType = typeMap.TryGetValue(resolvedKey, out var bt)
                        ? bt : BindingType.Transform;
                    EncodeBinding(kvp.Value, bindingType,
                        out var sid, out var at, out var spj);
                    list.Add(new BlueprintExporter.SceneBindingData
                    {
                        BindingKey         = resolvedKey,
                        StableObjectId     = sid,
                        AdapterType        = at,
                        SpatialPayloadJson = spj
                    });
                }
                return list.Count > 0 ? list : null;
            }

            return null;
        }

        // ══════════════════════════════════════════
        //  私有辅助
        // ══════════════════════════════════════════

        private SubGraphBindingGroup? CollectTopLevelBindings(Graph graph, ActionRegistry registry)
        {
            var contained = new HashSet<string>(
                graph.SubGraphFrames.SelectMany(f => f.ContainedNodeIds));

            var group = new SubGraphBindingGroup
            {
                SubGraphFrameId = "__toplevel__",
                SubGraphTitle   = "顶层节点"
            };
            var seen = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (contained.Contains(node.Id)) continue;
                if (node.UserData is not ActionNodeData ad) continue;
                if (!registry.TryGet(ad.ActionTypeId, out var def)) continue;

                foreach (var prop in def.Properties)
                {
                    if (prop.Type != PropertyType.SceneBinding) continue;
                    string sk = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                    if (!seen.Add(sk)) continue;
                    group.Bindings.Add(new SceneBindingSlot
                    {
                        BindingKey         = sk,
                        BindingType        = prop.SceneBindingType ?? BindingType.Transform,
                        DisplayName        = prop.DisplayName,
                        SourceActionTypeId = ad.ActionTypeId,
                        BoundObject        = _bindingContext.Get(sk)
                    });
                }
            }
            return group.Bindings.Count > 0 ? group : null;
        }

        private Dictionary<string, BindingType> BuildBindingTypeMap(ActionRegistry registry)
        {
            var map = new Dictionary<string, BindingType>();
            var vm  = _ctx.ViewModel;
            if (vm == null) return map;

            foreach (var node in vm.Graph.Nodes)
            {
                if (node.UserData is not ActionNodeData ad) continue;
                if (!registry.TryGet(ad.ActionTypeId, out var def)) continue;
                foreach (var prop in def.Properties)
                {
                    if (prop.Type != PropertyType.SceneBinding || string.IsNullOrEmpty(prop.Key)) continue;
                    map[BindingScopeUtility.BuildScopedKey(node.Id, prop.Key)] =
                        prop.SceneBindingType ?? BindingType.Transform;
                }
            }
            return map;
        }

        private static string ResolveBindingKey(string key, Dictionary<string, BindingType> typeMap)
        {
            if (string.IsNullOrEmpty(key) || BindingScopeUtility.IsScopedKey(key)) return key;
            string? matched = null;
            foreach (var sk in typeMap.Keys)
            {
                if (BindingScopeUtility.ExtractRawBindingKey(sk) != key) continue;
                if (matched != null) return key;
                matched = sk;
            }
            return matched ?? key;
        }

        private void EncodeBinding(
            GameObject?  obj,
            BindingType  type,
            out string   stableObjectId,
            out string   adapterType,
            out string   spatialPayloadJson)
        {
            _spatialDescriptor ??= SpatialModeRegistry.GetProjectModeDescriptor();

            if (obj == null)
            {
                stableObjectId     = "";
                adapterType        = _spatialDescriptor.AdapterType;
                spatialPayloadJson = "{}";
                return;
            }

            var payload        = _spatialDescriptor.BindingCodec.Encode(obj, type);
            stableObjectId     = payload.StableObjectId;
            adapterType        = string.IsNullOrEmpty(payload.AdapterType)
                ? _spatialDescriptor.AdapterType : payload.AdapterType;
            spatialPayloadJson = string.IsNullOrEmpty(payload.SerializedSpatialData)
                ? "{}" : payload.SerializedSpatialData;
        }
    }
}
