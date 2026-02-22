#nullable enable
using SceneBlueprint.SpatialAbstraction;
using SceneBlueprint.SpatialAbstraction.Defaults;

namespace SceneBlueprint.Adapters.Unity3D
{
    /// <summary>
    /// Unity3D Adapter 兼容工厂入口。
    /// C3 之后保留该类型用于兼容旧调用方。
    /// </summary>
    public static class Unity3DAdapterPlaceholders
    {
        public static IScenePlacementPolicy CreatePlacementPolicy()
        {
            // 当前放置仍通过 Unity3DAdapterServices.TryGetSceneViewPlacement 使用 SceneView 上下文。
            // 接口化的无上下文放置策略在后续阶段继续收敛。
            return new NullScenePlacementPolicy();
        }

        public static ISceneObjectIdentityService CreateIdentityService()
        {
            return new Unity3DSceneObjectIdentityService();
        }

        public static ISpatialBindingCodec CreateBindingCodec(ISceneObjectIdentityService identityService)
        {
            return new Unity3DSpatialBindingCodec(identityService);
        }
    }
}
