#nullable enable

namespace SceneBlueprint.SpatialAbstraction
{
    /// <summary>
    /// 场景对象稳定标识服务接口。
    /// 为不同引擎对象生成可持久化的稳定 ID。
    /// </summary>
    public interface ISceneObjectIdentityService
    {
        string GetOrCreateStableId(object sceneObject);

        bool TryResolve(string stableId, out object? sceneObject);
    }
}
