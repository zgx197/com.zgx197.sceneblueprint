#nullable enable

namespace SceneBlueprint.SpatialAbstraction
{
    /// <summary>
    /// 场景放置策略接口。
    /// 由不同 Adapter 提供 2D/3D 或特定游戏的坐标落点规则。
    /// </summary>
    public interface IScenePlacementPolicy
    {
        bool TryGetPlacement(in ScenePlacementRequest request, out ScenePlacementResult result);
    }
}
