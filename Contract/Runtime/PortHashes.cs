#nullable enable

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 预计算的常用端口 hash 常量，避免运行时重复 GetHashCode()。
    /// </summary>
    public static class PortHashes
    {
        public static readonly int Out = "out".GetHashCode();
        public static readonly int In = "in".GetHashCode();
        public static readonly int OnTimeout = "onTimeout".GetHashCode();
        public static readonly int OnTriggered = "onTriggered".GetHashCode();
        public static readonly int Pass = "pass".GetHashCode();
        public static readonly int Reject = "reject".GetHashCode();
        public static readonly int OnWaveStart = "onWaveStart".GetHashCode();
        public static readonly int OnAllCleared = "onAllCleared".GetHashCode();
        public static readonly int Cond0 = "cond0".GetHashCode();
        public static readonly int Cond1 = "cond1".GetHashCode();
        public static readonly int Cond2 = "cond2".GetHashCode();
        public static readonly int Cond3 = "cond3".GetHashCode();
    }
}
