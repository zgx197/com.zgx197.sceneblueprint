#nullable enable
using SceneBlueprint.Core;
using SceneBlueprint.Contract;

namespace SceneBlueprint.SpatialAbstraction
{
    /// <summary>
    /// 场景绑定编解码接口。
    /// 负责把引擎对象编码为跨层可传输的绑定载荷。
    /// </summary>
    public interface ISpatialBindingCodec
    {
        SceneBindingPayload Encode(object sceneObject, BindingType bindingType);

        bool TryDecode(in SceneBindingPayload payload, out object? sceneObject);
    }
}
