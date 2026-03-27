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
    /// Signal 节点统一编译产物的 metadata 读写辅助。
    /// 当前继续按 actionId 写入顶层 transport metadata 运输壳，
    /// 供导出期、Inspector 预览和运行时共享。
    /// </summary>
    public static class SignalCompiledActionMetadata
    {
        public const string MetadataKeyPrefix = "compiled.signal.";
        private static readonly Type? UnityJsonUtilityType = ResolveUnityJsonUtilityType();
        private static readonly MethodInfo? UnityJsonFromJsonMethod = ResolveUnityJsonFromJsonMethod();
        private static readonly MethodInfo? UnityJsonToJsonMethod = ResolveUnityJsonToJsonMethod();

        public static string BuildMetadataKey(string? actionId)
        {
            return CompiledMetadataTransportUtility.BuildMetadataKey(MetadataKeyPrefix, actionId);
        }

        public static bool TryRead(PropertyValue[]? metadata, string? actionId, out SignalCompiledAction? compiledAction)
        {
            return CompiledMetadataTransportUtility.TryReadPayload(
                metadata,
                MetadataKeyPrefix,
                actionId,
                TryParse,
                out compiledAction);
        }

        public static bool TryParse(string? json, out SignalCompiledAction? compiledAction)
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
                    return compiledAction != null
                        && compiledAction.SchemaVersion > 0
                        && !string.IsNullOrWhiteSpace(compiledAction.ActionId)
                        && !string.IsNullOrWhiteSpace(compiledAction.ActionTypeId);
                }

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var serializer = new DataContractJsonSerializer(typeof(SignalCompiledAction));
                compiledAction = serializer.ReadObject(stream) as SignalCompiledAction;
                return compiledAction != null
                    && compiledAction.SchemaVersion > 0
                    && !string.IsNullOrWhiteSpace(compiledAction.ActionId)
                    && !string.IsNullOrWhiteSpace(compiledAction.ActionTypeId);
            }
            catch
            {
                compiledAction = null;
                return false;
            }
        }

        public static string Serialize(SignalCompiledAction compiledAction)
        {
            if (TrySerializeWithUnityJson(compiledAction, out var json))
            {
                return json;
            }

            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(SignalCompiledAction));
            serializer.WriteObject(stream, compiledAction);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static bool TryParseWithUnityJson(string json, out SignalCompiledAction? compiledAction)
        {
            compiledAction = null;
            if (UnityJsonFromJsonMethod == null)
            {
                return false;
            }

            try
            {
                compiledAction = UnityJsonFromJsonMethod
                    .MakeGenericMethod(typeof(SignalCompiledAction))
                    .Invoke(null, new object[] { json }) as SignalCompiledAction;
                return compiledAction != null;
            }
            catch
            {
                compiledAction = null;
                return false;
            }
        }

        private static bool TrySerializeWithUnityJson(SignalCompiledAction compiledAction, out string json)
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
    public sealed class SignalCompiledAction
    {
        [DataMember(Order = 0)]
        public int SchemaVersion = 1;

        [DataMember(Order = 1)]
        public string ActionId = string.Empty;

        [DataMember(Order = 2)]
        public string ActionTypeId = string.Empty;

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public SignalEmitCompiledData? Emit;

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public SignalWaitSignalCompiledData? WaitSignal;

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public SignalWatchConditionCompiledData? WatchCondition;

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public SignalCompositeConditionCompiledData? CompositeCondition;
    }

    [DataContract]
    [Serializable]
    public sealed class CompiledEntityRefInfo
    {
        [DataMember(Order = 0)]
        public string Serialized = string.Empty;

        [DataMember(Order = 1)]
        public string Summary = string.Empty;
    }

    [DataContract]
    [Serializable]
    public sealed class SignalEmitCompiledData
    {
        [DataMember(Order = 0)]
        public string SignalTag = string.Empty;

        [DataMember(Order = 1)]
        public CompiledEntityRefInfo Subject = new();

        [DataMember(Order = 2)]
        public CompiledEntityRefInfo Instigator = new();

        [DataMember(Order = 3)]
        public CompiledEntityRefInfo Target = new();

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }

    [DataContract]
    [Serializable]
    public sealed class SignalWaitSignalCompiledData
    {
        [DataMember(Order = 0)]
        public string SignalTag = string.Empty;

        [DataMember(Order = 1)]
        public bool IsWildcardPattern;

        [DataMember(Order = 2)]
        public float TimeoutSeconds;

        [DataMember(Order = 3)]
        public CompiledEntityRefInfo SubjectFilter = new();

        [DataMember(Order = 4)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }

    [DataContract]
    [Serializable]
    public sealed class SignalWatchConditionCompiledData
    {
        [DataMember(Order = 0)]
        public string ConditionType = string.Empty;

        [DataMember(Order = 1)]
        public float TimeoutSeconds;

        [DataMember(Order = 2)]
        public bool Repeat;

        [DataMember(Order = 3)]
        public string ParametersRaw = string.Empty;

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public ConditionParameter[] Parameters = Array.Empty<ConditionParameter>();

        [DataMember(Order = 5)]
        public ConditionWatchDescriptor Descriptor = new();

        [DataMember(Order = 6)]
        public CompiledEntityRefInfo Target = new();

        [DataMember(Order = 7)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 8, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }

    [DataContract]
    [Serializable]
    public sealed class SignalCompositeConditionCompiledData
    {
        [DataMember(Order = 0)]
        public string Mode = "AND";

        [DataMember(Order = 1)]
        public float TimeoutSeconds;

        [DataMember(Order = 2)]
        public int ConnectedMask;

        [DataMember(Order = 3)]
        public int ConnectedCount;

        [DataMember(Order = 4)]
        public string[] ConnectedPortIds = Array.Empty<string>();

        [DataMember(Order = 5)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }
}
