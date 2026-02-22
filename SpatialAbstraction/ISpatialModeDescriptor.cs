#nullable enable

namespace SceneBlueprint.SpatialAbstraction
{
    /// <summary>
    /// 空间模式描述器（引擎无关核心契约）。
    /// 定义模式标识、适配器类型及绑定编解码能力。
    /// </summary>
    public interface ISpatialModeDescriptor
    {
        string ModeId { get; }
        string DisplayName { get; }
        string AdapterType { get; }
        ISpatialBindingCodec BindingCodec { get; }
    }
}
