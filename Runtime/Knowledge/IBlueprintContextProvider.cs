#nullable enable
using SceneBlueprint.Contract.Knowledge;

namespace SceneBlueprint.Runtime.Knowledge
{
    /// <summary>
    /// 蓝图实时上下文采集接口。
    /// 编辑器层实现此接口，提供当前蓝图状态数据。
    /// </summary>
    public interface IBlueprintContextProvider
    {
        /// <summary>
        /// 采集当前蓝图的实时上下文。
        /// </summary>
        BlueprintContext GetCurrentContext();

        /// <summary>
        /// 当前是否有活跃的蓝图编辑会话。
        /// </summary>
        bool HasActiveSession { get; }
    }
}
