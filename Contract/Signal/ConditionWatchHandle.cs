#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 条件监听运行时句柄。
    /// 用于把 WatchCondition 节点在 runtime 中的注册、触发、释放口径，
    /// 从裸 actionIndex 提升为带 conditionType 语义的正式标识。
    /// </summary>
    public readonly struct ConditionWatchHandle : IEquatable<ConditionWatchHandle>
    {
        public ConditionWatchHandle(int actionIndex, string? conditionType)
        {
            ActionIndex = actionIndex;
            ConditionType = ConditionWatchSemanticUtility.NormalizeConditionType(conditionType);
        }

        public int ActionIndex { get; }

        public string ConditionType { get; }

        public bool IsValid => ActionIndex >= 0 && !string.IsNullOrEmpty(ConditionType);

        public bool Equals(ConditionWatchHandle other)
        {
            return ActionIndex == other.ActionIndex
                && string.Equals(ConditionType, other.ConditionType, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is ConditionWatchHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ActionIndex * 397) ^ (ConditionType?.GetHashCode() ?? 0);
            }
        }

        public override string ToString()
        {
            return IsValid
                ? $"{ConditionType}@{ActionIndex}"
                : $"InvalidWatch@{ActionIndex}";
        }

        public static bool operator ==(ConditionWatchHandle left, ConditionWatchHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConditionWatchHandle left, ConditionWatchHandle right)
        {
            return !left.Equals(right);
        }
    }
}
