#nullable enable
using System;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 条件监听运行时注册对象。
    /// 统一承载 WatchCondition 节点 runtime 需要传递给 Bus / Evaluator 的正式输入。
    /// </summary>
    public sealed class ConditionWatchRegistration
    {
        public ConditionWatchRegistration(int actionIndex, ConditionWatchDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            ActionIndex = actionIndex;
            Descriptor = descriptor;
            ConditionType = ConditionWatchSemanticUtility.NormalizeConditionType(descriptor.ConditionType);
            Handle = new ConditionWatchHandle(actionIndex, ConditionType);
        }

        public int ActionIndex { get; }

        public string ConditionType { get; }

        public ConditionWatchDescriptor Descriptor { get; }

        public ConditionWatchHandle Handle { get; }
    }
}
