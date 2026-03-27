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
    /// Trigger / Interaction 这类主体引用节点的统一编译 metadata 读写辅助。
    /// 当前继续按 actionId 写入顶层 transport metadata 运输壳，
    /// 供导出、Inspector 与运行时共享。
    /// </summary>
    public static class EntityRefActionCompiledMetadata
    {
        public const string MetadataKeyPrefix = "compiled.entity-ref-action.";
        private static readonly Type? UnityJsonUtilityType = ResolveUnityJsonUtilityType();
        private static readonly MethodInfo? UnityJsonFromJsonMethod = ResolveUnityJsonFromJsonMethod();
        private static readonly MethodInfo? UnityJsonToJsonMethod = ResolveUnityJsonToJsonMethod();

        public static string BuildMetadataKey(string? actionId)
        {
            return CompiledMetadataTransportUtility.BuildMetadataKey(MetadataKeyPrefix, actionId);
        }

        public static bool TryRead(PropertyValue[]? metadata, string? actionId, out EntityRefCompiledAction? compiledAction)
        {
            return CompiledMetadataTransportUtility.TryReadPayload(
                metadata,
                MetadataKeyPrefix,
                actionId,
                TryParse,
                out compiledAction);
        }

        public static bool TryParse(string? json, out EntityRefCompiledAction? compiledAction)
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
                var serializer = new DataContractJsonSerializer(typeof(EntityRefCompiledAction));
                compiledAction = serializer.ReadObject(stream) as EntityRefCompiledAction;
                return IsValid(compiledAction);
            }
            catch
            {
                compiledAction = null;
                return false;
            }
        }

        public static string Serialize(EntityRefCompiledAction compiledAction)
        {
            if (TrySerializeWithUnityJson(compiledAction, out var json))
            {
                return json;
            }

            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(EntityRefCompiledAction));
            serializer.WriteObject(stream, compiledAction);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static bool IsValid(EntityRefCompiledAction? compiledAction)
        {
            return compiledAction != null
                   && compiledAction.SchemaVersion > 0
                   && !string.IsNullOrWhiteSpace(compiledAction.ActionId)
                   && !string.IsNullOrWhiteSpace(compiledAction.ActionTypeId);
        }

        private static bool TryParseWithUnityJson(string json, out EntityRefCompiledAction? compiledAction)
        {
            compiledAction = null;
            if (UnityJsonFromJsonMethod == null)
            {
                return false;
            }

            try
            {
                compiledAction = UnityJsonFromJsonMethod
                    .MakeGenericMethod(typeof(EntityRefCompiledAction))
                    .Invoke(null, new object[] { json }) as EntityRefCompiledAction;
                return compiledAction != null;
            }
            catch
            {
                compiledAction = null;
                return false;
            }
        }

        private static bool TrySerializeWithUnityJson(EntityRefCompiledAction compiledAction, out string json)
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
    public sealed class EntityRefCompiledAction
    {
        [DataMember(Order = 0)]
        public int SchemaVersion = 1;

        [DataMember(Order = 1)]
        public string ActionId = string.Empty;

        [DataMember(Order = 2)]
        public string ActionTypeId = string.Empty;

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public TriggerEnterAreaCompiledData? TriggerEnterArea;

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public InteractionApproachTargetCompiledData? InteractionApproachTarget;
    }

    [DataContract]
    [Serializable]
    public sealed class CompiledSceneBindingInfo
    {
        [DataMember(Order = 0)]
        public string BindingKey = string.Empty;

        [DataMember(Order = 1)]
        public string BindingType = string.Empty;

        [DataMember(Order = 2)]
        public string SceneObjectId = string.Empty;

        [DataMember(Order = 3)]
        public string StableObjectId = string.Empty;

        [DataMember(Order = 4)]
        public string SpatialPayloadJson = string.Empty;

        [DataMember(Order = 5)]
        public string Summary = string.Empty;
    }

    [DataContract]
    [Serializable]
    public sealed class TriggerEnterAreaCompiledData
    {
        [DataMember(Order = 0)]
        public bool RequireFullyInside;

        [DataMember(Order = 1)]
        public CompiledEntityRefInfo Subject = new();

        [DataMember(Order = 2)]
        public CompiledSceneBindingInfo TriggerArea = new();

        [DataMember(Order = 3)]
        public string SubjectSummary = string.Empty;

        [DataMember(Order = 4)]
        public string TriggerAreaSummary = string.Empty;

        [DataMember(Order = 5)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }

    [DataContract]
    [Serializable]
    public sealed class InteractionApproachTargetCompiledData
    {
        [DataMember(Order = 0)]
        public CompiledEntityRefInfo Subject = new();

        [DataMember(Order = 1)]
        public CompiledEntityRefInfo Target = new();

        [DataMember(Order = 2)]
        public float Range;

        [DataMember(Order = 3)]
        public string SubjectSummary = string.Empty;

        [DataMember(Order = 4)]
        public string TargetSummary = string.Empty;

        [DataMember(Order = 5)]
        public string ConditionSummary = string.Empty;

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }
}
