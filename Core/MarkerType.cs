#nullable enable

namespace SceneBlueprint.Core
{
    /// <summary>
    /// 场景标记类型 ID 常量——字符串标识,取代旧的 MarkerType 枚举。
    /// <para>
    /// 用于 <see cref="MarkerRequirement"/> 声明 Action 需要什么类型的标记，
    /// 以及 SceneMarker 组件标识自身类型。
    /// </para>
    /// <para>
    /// 预定义了内置类型的 ID，但不限于此——任意字符串均可作为标记类型 ID，
    /// 新增标记类型只需在 Editor 层提供 <c>IMarkerDefinitionProvider</c> 即可。
    /// </para>
    /// </summary>
    public static class MarkerTypeIds
    {
        /// <summary>单点标记——位置 + 朝向（如刷怪点、摄像机位、VFX 播放点）</summary>
        public const string Point = "Point";

        /// <summary>区域标记——多边形或 Box 区域（如触发区、刷怪区、灯光区）</summary>
        public const string Area = "Area";
    }
}
