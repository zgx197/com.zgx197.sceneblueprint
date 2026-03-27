#nullable enable
using SceneBlueprint.Contract;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;

namespace SceneBlueprint.Editor.WindowServices.Binding
{
    /// <summary>
    /// 场景绑定恢复器——仅负责“场景 → 图”方向：通过 MarkerId 反查场景中的 Marker，
    /// 填充到 <see cref="BindingContext"/>。
    /// </summary>
    public class SceneBindingRestorer
    {
        private readonly IBlueprintReadContext _ctx;
        private readonly BindingContext        _bindingContext;

        public SceneBindingRestorer(
            IBlueprintReadContext ctx,
            BindingContext        bindingContext)
        {
            _ctx            = ctx;
            _bindingContext = bindingContext;
        }

        /// <summary>
        /// 蓝图加载后从场景恢复绑定到 BindingContext。
        /// 通过 PropertyBag 中存储的 MarkerId 在场景中查找对应的 Marker。
        /// </summary>
        public void RestoreFromScene()
        {
            var vm    = _ctx.ViewModel;
            var asset = _ctx.CurrentAsset;
            if (vm == null || asset == null) return;

            _bindingContext.Clear();

            var registry = _ctx.ActionRegistry;
            foreach (var node in vm.Graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var actionDef)) continue;

                foreach (var prop in actionDef.Properties)
                {
                    if (prop.SceneBindingType == null) continue;
                    string scopedKey = BindingScopeUtility.BuildScopedKey(node.Id, prop.Key);

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
