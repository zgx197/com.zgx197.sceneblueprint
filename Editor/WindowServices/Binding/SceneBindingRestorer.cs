#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.WindowServices;
using SceneBlueprint.Runtime;

namespace SceneBlueprint.Editor.WindowServices.Binding
{
    /// <summary>
    /// 场景绑定恢复器——仅负责"场景 → 图"方向：将场景持久化数据或 MarkerId 反查结果
    /// 填充到 <see cref="BindingContext"/>。
    /// </summary>
    public class SceneBindingRestorer
    {
        private readonly IBlueprintReadContext _ctx;
        private readonly BindingContext        _bindingContext;
        private readonly ISceneBindingStore    _store;

        public SceneBindingRestorer(
            IBlueprintReadContext ctx,
            BindingContext        bindingContext,
            ISceneBindingStore    store)
        {
            _ctx            = ctx;
            _bindingContext = bindingContext;
            _store          = store;
        }

        /// <summary>
        /// 蓝图加载后从场景恢复绑定到 BindingContext。
        /// 策略1：SceneBindingStore → 策略2：PropertyBag MarkerId 反查。
        /// </summary>
        public void RestoreFromScene()
        {
            var vm    = _ctx.ViewModel;
            var asset = _ctx.CurrentAsset;
            if (vm == null || asset == null) return;

            _bindingContext.Clear();

            // 策略1：从持久化存储恢复
            if (_store.TryLoadBindingGroups(asset, out var groups))
            {
                foreach (var g in groups)
                    foreach (var b in g.Bindings)
                        if (!string.IsNullOrEmpty(b.BindingKey) && b.BoundObject != null)
                            _bindingContext.Set(b.BindingKey, b.BoundObject);
            }

            // 策略2：对未恢复的绑定用 MarkerId 在场景中回退查找
            var registry = _ctx.ActionRegistry;
            foreach (var node in vm.Graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;
                    string scopedKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);
                    if (_bindingContext.Get(scopedKey) != null) continue;

                    var storedId = data.Properties.Get<string>(prop.Key);
                    if (string.IsNullOrEmpty(storedId)) continue;

                    var marker = SceneMarkerSelectionBridge.FindMarkerInScene(storedId);
                    if (marker != null) _bindingContext.Set(scopedKey, marker.gameObject);
                }
            }

            int restored = _bindingContext.BoundCount;
            if (restored > 0)
                SBLog.Info(SBLogTags.Binding, $"已从场景恢复 {restored} 个绑定");
        }
    }
}
