#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 低频查询接口——System 通过此接口读取蓝图静态数据。
    /// <para>
    /// 设计原则：将"通用业务逻辑"与"具体存储方式"解耦。
    /// Package 侧实现从 BlueprintFrame 读取，mini_game 侧实现从 SBBlueprintData 读取。
    /// </para>
    /// <para>
    /// "低频"指相对于 States 数组直接索引而言——属性查询涉及字符串比较，
    /// 但蓝图规模（10~200 节点）下性能完全可接受。
    /// </para>
    /// </summary>
    public interface IActionQuery
    {
        /// <summary>获取指定 TypeId 的所有 Action 索引列表（null 表示该类型不存在）</summary>
        IReadOnlyList<int>? GetActionIndices(string typeId);

        /// <summary>获取指定 Action 的字符串属性值（找不到返回 null）</summary>
        string? GetProperty(int actionIndex, string key);

        /// <summary>获取指定 Action 的 float 属性值（解析失败返回 defaultValue）</summary>
        float GetPropertyFloat(int actionIndex, string key, float defaultValue = 0f);

        /// <summary>获取指定 Action 的 int 属性值（解析失败返回 defaultValue）</summary>
        int GetPropertyInt(int actionIndex, string key, int defaultValue = 0);

        /// <summary>获取指定 Action 的 bool 属性值（解析失败返回 defaultValue）</summary>
        bool GetPropertyBool(int actionIndex, string key, bool defaultValue = false);

        /// <summary>获取指定 Action 的 TypeId</summary>
        string GetTypeId(int actionIndex);

        /// <summary>Action 总数</summary>
        int ActionCount { get; }

        /// <summary>获取指定 Action 的入边信息（用于 CompositeConditionSystem 构建连接掩码等）</summary>
        IReadOnlyList<IncomingTransitionInfo>? GetIncomingTransitions(int actionIndex);

        /// <summary>蓝图是否已完成</summary>
        bool IsCompleted { get; set; }
    }

    /// <summary>
    /// 入边信息（轻量值类型，用于 IActionQuery.GetIncomingTransitions）。
    /// </summary>
    public readonly struct IncomingTransitionInfo
    {
        public readonly int FromActionIndex;
        public readonly int FromPortHash;
        public readonly int ToPortHash;

        public IncomingTransitionInfo(int fromActionIndex, int fromPortHash, int toPortHash)
        {
            FromActionIndex = fromActionIndex;
            FromPortHash = fromPortHash;
            ToPortHash = toPortHash;
        }
    }
}
