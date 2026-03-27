#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 端口触发事件（统一值类型，两端共享）。
    /// <para>
    /// 全部使用 int（hash），满足 qtn unmanaged 约束。
    /// DEBUG 模式下保留原始字符串用于调试显示。
    /// </para>
    /// </summary>
    public struct PortEvent
    {
        /// <summary>源 Action 索引</summary>
        public int FromActionIndex;

        /// <summary>源端口 hash（"out".GetHashCode() 等）</summary>
        public int FromPortHash;

        /// <summary>目标 Action 索引</summary>
        public int ToActionIndex;

        /// <summary>目标端口 hash</summary>
        public int ToPortHash;

#if UNITY_EDITOR || DEBUG
        /// <summary>调试用：源端口原始字符串</summary>
        public string? DebugFromPortId;
        /// <summary>调试用：目标端口原始字符串</summary>
        public string? DebugToPortId;
#endif

        public PortEvent(int fromIdx, int fromHash, int toIdx, int toHash)
        {
            FromActionIndex = fromIdx;
            FromPortHash = fromHash;
            ToActionIndex = toIdx;
            ToPortHash = toHash;
#if UNITY_EDITOR || DEBUG
            DebugFromPortId = null;
            DebugToPortId = null;
#endif
        }

        public override string ToString()
        {
#if UNITY_EDITOR || DEBUG
            var from = DebugFromPortId ?? FromPortHash.ToString();
            var to = DebugToPortId ?? ToPortHash.ToString();
            return $"PortEvent({FromActionIndex}:{from} → {ToActionIndex}:{to})";
#else
            return $"PortEvent({FromActionIndex}:{FromPortHash} → {ToActionIndex}:{ToPortHash})";
#endif
        }
    }
}
