#nullable enable

namespace SceneBlueprint.SpatialAbstraction
{
    /// <summary>
    /// 绑定作用域策略接口。
    /// 以 nodeId 为作用域，生成全局唯一的 scoped key。
    /// 键格式：nodeId/bindingKey。
    /// </summary>
    public interface IBindingScopePolicy
    {
        string BuildScopedKey(string nodeId, string bindingKey);
    }
}
