#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace SceneBlueprint.Contract
{
    /// <summary>
    /// Flow 域节点统一编译 metadata 读写辅助。
    /// 当前第一版覆盖 Flow.Join / Flow.Filter，把“图结构汇合规则”和“过滤比较规则”前移到编译层。
    /// </summary>
    public static class FlowCompiledActionMetadata
    {
        public const string MetadataKeyPrefix = "compiled.flow.";
        private static readonly Type? UnityJsonUtilityType = ResolveUnityJsonUtilityType();
        private static readonly MethodInfo? UnityJsonFromJsonMethod = ResolveUnityJsonFromJsonMethod();
        private static readonly MethodInfo? UnityJsonToJsonMethod = ResolveUnityJsonToJsonMethod();

        public static string BuildMetadataKey(string? actionId)
        {
            return CompiledMetadataTransportUtility.BuildMetadataKey(MetadataKeyPrefix, actionId);
        }

        public static bool TryRead(PropertyValue[]? metadata, string? actionId, out FlowCompiledAction? compiledAction)
        {
            return CompiledMetadataTransportUtility.TryReadPayload(
                metadata,
                MetadataKeyPrefix,
                actionId,
                TryParse,
                out compiledAction);
        }

        public static bool TryParse(string? json, out FlowCompiledAction? compiledAction)
        {
            compiledAction = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                if (TryParseWithUnityJson(json, out compiledAction))
                {
                    return IsValid(compiledAction);
                }

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var serializer = new DataContractJsonSerializer(typeof(FlowCompiledAction));
                compiledAction = serializer.ReadObject(stream) as FlowCompiledAction;
                return IsValid(compiledAction);
            }
            catch
            {
                compiledAction = null;
                return false;
            }
        }

        public static string Serialize(FlowCompiledAction compiledAction)
        {
            if (TrySerializeWithUnityJson(compiledAction, out var json))
            {
                return json;
            }

            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(FlowCompiledAction));
            serializer.WriteObject(stream, compiledAction);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static bool IsValid(FlowCompiledAction? compiledAction)
        {
            return compiledAction != null
                   && compiledAction.SchemaVersion > 0
                   && !string.IsNullOrWhiteSpace(compiledAction.ActionId)
                   && !string.IsNullOrWhiteSpace(compiledAction.ActionTypeId);
        }

        private static bool TryParseWithUnityJson(string json, out FlowCompiledAction? compiledAction)
        {
            compiledAction = null;
            if (UnityJsonFromJsonMethod == null)
            {
                return false;
            }

            try
            {
                compiledAction = UnityJsonFromJsonMethod
                    .MakeGenericMethod(typeof(FlowCompiledAction))
                    .Invoke(null, new object[] { json }) as FlowCompiledAction;
                return compiledAction != null;
            }
            catch
            {
                compiledAction = null;
                return false;
            }
        }

        private static bool TrySerializeWithUnityJson(FlowCompiledAction compiledAction, out string json)
        {
            json = string.Empty;
            if (UnityJsonToJsonMethod == null)
            {
                return false;
            }

            try
            {
                json = (string?)UnityJsonToJsonMethod.Invoke(null, new object[] { compiledAction }) ?? string.Empty;
                return !string.IsNullOrWhiteSpace(json);
            }
            catch
            {
                json = string.Empty;
                return false;
            }
        }

        private static Type? ResolveUnityJsonUtilityType()
        {
            return Type.GetType("UnityEngine.JsonUtility, UnityEngine.CoreModule")
                   ?? Type.GetType("UnityEngine.JsonUtility, UnityEngine");
        }

        private static MethodInfo? ResolveUnityJsonFromJsonMethod()
        {
            return UnityJsonUtilityType?.GetMethod(
                "FromJson",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);
        }

        private static MethodInfo? ResolveUnityJsonToJsonMethod()
        {
            return UnityJsonUtilityType?.GetMethod(
                "ToJson",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(object) },
                modifiers: null);
        }
    }

    [DataContract]
    [Serializable]
    public sealed class FlowCompiledAction
    {
        [DataMember(Order = 0)]
        public int SchemaVersion = 1;

        [DataMember(Order = 1)]
        public string ActionId = string.Empty;

        [DataMember(Order = 2)]
        public string ActionTypeId = string.Empty;

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public FlowJoinCompiledData? Join;

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public FlowFilterCompiledData? Filter;

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public FlowBranchCompiledData? Branch;

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public FlowDelayCompiledData? Delay;
    }

    [DataContract]
    [Serializable]
    public sealed class FlowJoinCompiledData
    {
        [DataMember(Order = 0)]
        public int RequiredCount;

        [DataMember(Order = 1)]
        public string[] IncomingActionIds = Array.Empty<string>();

        [DataMember(Order = 2)]
        public string IncomingActionSummary = string.Empty;

        [DataMember(Order = 3)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }

    [DataContract]
    [Serializable]
    public sealed class FlowFilterCompiledData
    {
        [DataMember(Order = 0)]
        public string Operator = "==";

        [DataMember(Order = 1)]
        public string ConstValueText = "0";

        [DataMember(Order = 2)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 3)]
        public bool MissingCompareInputPasses = true;

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }

    [DataContract]
    [Serializable]
    public sealed class FlowBranchCompiledData
    {
        [DataMember(Order = 0)]
        public bool ConditionValue;

        [DataMember(Order = 1)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 2)]
        public string RoutedPort = string.Empty;

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }

    [DataContract]
    [Serializable]
    public sealed class FlowDelayCompiledData
    {
        [DataMember(Order = 0)]
        public float RawDelaySeconds = 1f;

        [DataMember(Order = 1)]
        public float EffectiveDelaySeconds = 1f;

        [DataMember(Order = 2)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }
}
