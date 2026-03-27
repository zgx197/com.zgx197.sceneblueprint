#nullable enable
using System.Collections.Generic;
using NodeGraph.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Export;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Editor.WindowServices;
using UnityEngine;

namespace SceneBlueprint.Editor.WindowServices.Binding
{
    /// <summary>
    /// 场景绑定收集器——负责"图 → 外部"方向：
    /// <list type="bullet">
    /// <item><see cref="CollectForExport"/>：收集导出用的 SceneBindingData 列表</item>
    /// </list>
    /// </summary>
    public class SceneBindingCollector : ISceneBindingCollector
    {
        private readonly IBlueprintReadContext _ctx;
        private readonly BindingContext        _bindingContext;
        private IEditorSpatialModeDescriptor?  _spatialDescriptor;

        public SceneBindingCollector(
            IBlueprintReadContext ctx,
            BindingContext        bindingContext)
        {
            _ctx            = ctx;
            _bindingContext = bindingContext;
        }

        // ══════════════════════════════════════════
        //  公开 API
        // ══════════════════════════════════════════

        /// <summary>为导出收集绑定数据。从 BindingContext（内存）采集。</summary>
        public List<BlueprintExporter.SceneBindingData>? CollectForExport()
        {
            var registry = _ctx.ActionRegistry;

            if (_bindingContext.Count == 0) return null;

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

        // ══════════════════════════════════════════
        //  私有辅助
        // ══════════════════════════════════════════

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
