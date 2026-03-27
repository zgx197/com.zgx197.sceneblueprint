#nullable enable
using System;
using System.Runtime.Serialization;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// 条件监听描述符——WatchCondition 节点的导出数据。
    /// <para>
    /// 描述"监听什么条件"的纯数据结构，由 sbdef 导出，运行时由 SignalSystem 消费。
    /// </para>
    /// <para>
    /// 示例：监听 Boss 的 HP 低于 30%
    /// <code>
    /// new ConditionWatchDescriptor {
    ///     ConditionType = "HPThreshold",
    ///     Target = EntityRef.FromRole("Boss"),
    ///     Parameters = new[] {
    ///         new ConditionParameter { Key = "op", Value = "&lt;=" },
    ///         new ConditionParameter { Key = "threshold", Value = "0.3" },
    ///     },
    ///     Timeout = 0f,   // 0 = 无超时
    /// };
    /// </code>
    /// </para>
    /// </summary>
    [DataContract]
    [Serializable]
    public class ConditionWatchDescriptor
    {
        /// <summary>条件类型 ID（对应 IConditionEvaluator.TypeId）</summary>
        [DataMember(Order = 0)]
        public string ConditionType = "";

        /// <summary>监听目标实体</summary>
        [DataMember(Order = 1)]
        public EntityRef Target = new();

        /// <summary>条件参数（键值对）</summary>
        [DataMember(Order = 2)]
        public ConditionParameter[] Parameters = Array.Empty<ConditionParameter>();

        /// <summary>超时时间（秒），0 = 无超时（永久等待）</summary>
        [DataMember(Order = 3)]
        public float Timeout;

        /// <summary>获取参数值（未找到返回 defaultValue）</summary>
        public string GetParameter(string key, string defaultValue = "")
        {
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (Parameters[i].Key == key)
                    return Parameters[i].Value;
            }
            return defaultValue;
        }
    }

    /// <summary>条件参数（键值对）</summary>
    [DataContract]
    [Serializable]
    public class ConditionParameter
    {
        [DataMember(Order = 0)]
        public string Key = "";

        [DataMember(Order = 1)]
        public string Value = "";
    }
}
