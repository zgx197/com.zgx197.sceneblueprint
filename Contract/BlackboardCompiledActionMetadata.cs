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
    /// Blackboard 域节点统一编译 metadata 读写辅助。
    /// 把变量解析结果与值写入摘要前移到编译层，供 Inspector、导出与运行时共享。
    /// </summary>
    public static class BlackboardCompiledActionMetadata
    {
        public const string MetadataKeyPrefix = "compiled.blackboard.";
        private static readonly Type? UnityJsonUtilityType = ResolveUnityJsonUtilityType();
        private static readonly MethodInfo? UnityJsonFromJsonMethod = ResolveUnityJsonFromJsonMethod();
        private static readonly MethodInfo? UnityJsonToJsonMethod = ResolveUnityJsonToJsonMethod();

        public static string BuildMetadataKey(string? actionId)
        {
            return CompiledMetadataTransportUtility.BuildMetadataKey(MetadataKeyPrefix, actionId);
        }

        public static bool TryRead(PropertyValue[]? metadata, string? actionId, out BlackboardCompiledAction? compiledAction)
        {
            return CompiledMetadataTransportUtility.TryReadPayload(
                metadata,
                MetadataKeyPrefix,
                actionId,
                TryParse,
                out compiledAction);
        }

        public static bool TryParse(string? json, out BlackboardCompiledAction? compiledAction)
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
                var serializer = new DataContractJsonSerializer(typeof(BlackboardCompiledAction));
                compiledAction = serializer.ReadObject(stream) as BlackboardCompiledAction;
                return IsValid(compiledAction);
            }
            catch
            {
                compiledAction = null;
                return false;
            }
        }

        public static string Serialize(BlackboardCompiledAction compiledAction)
        {
            if (TrySerializeWithUnityJson(compiledAction, out var json))
            {
                return json;
            }

            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(BlackboardCompiledAction));
            serializer.WriteObject(stream, compiledAction);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static bool IsValid(BlackboardCompiledAction? compiledAction)
        {
            return compiledAction != null
                   && compiledAction.SchemaVersion > 0
                   && !string.IsNullOrWhiteSpace(compiledAction.ActionId)
                   && !string.IsNullOrWhiteSpace(compiledAction.ActionTypeId);
        }

        private static bool TryParseWithUnityJson(string json, out BlackboardCompiledAction? compiledAction)
        {
            compiledAction = null;
            if (UnityJsonFromJsonMethod == null)
            {
                return false;
            }

            try
            {
                compiledAction = UnityJsonFromJsonMethod
                    .MakeGenericMethod(typeof(BlackboardCompiledAction))
                    .Invoke(null, new object[] { json }) as BlackboardCompiledAction;
                return compiledAction != null;
            }
            catch
            {
                compiledAction = null;
                return false;
            }
        }

        private static bool TrySerializeWithUnityJson(BlackboardCompiledAction compiledAction, out string json)
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
    public sealed class BlackboardCompiledAction
    {
        [DataMember(Order = 0)]
        public int SchemaVersion = 1;

        [DataMember(Order = 1)]
        public string ActionId = string.Empty;

        [DataMember(Order = 2)]
        public string ActionTypeId = string.Empty;

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public BlackboardGetCompiledData? Get;

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public BlackboardSetCompiledData? Set;
    }

    [DataContract]
    [Serializable]
    public sealed class BlackboardVariableCompiledData
    {
        [DataMember(Order = 0)]
        public int VariableIndex = -1;

        [DataMember(Order = 1)]
        public string Scope = string.Empty;

        [DataMember(Order = 2)]
        public string VariableName = string.Empty;

        [DataMember(Order = 3)]
        public string VariableType = string.Empty;

        [DataMember(Order = 4)]
        public string VariableSummary = string.Empty;
    }

    [DataContract]
    [Serializable]
    public sealed class BlackboardGetCompiledData
    {
        [DataMember(Order = 0)]
        public BlackboardVariableCompiledData Variable = new();

        [DataMember(Order = 1)]
        public string AccessSummary = string.Empty;

        [DataMember(Order = 2, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }

    [DataContract]
    [Serializable]
    public sealed class BlackboardSetCompiledData
    {
        [DataMember(Order = 0)]
        public BlackboardVariableCompiledData Variable = new();

        [DataMember(Order = 1)]
        public string RawValueText = string.Empty;

        [DataMember(Order = 2)]
        public string NormalizedValueText = string.Empty;

        [DataMember(Order = 3)]
        public string AccessSummary = string.Empty;

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public SemanticDescriptorSet Semantics = new();
    }
}
