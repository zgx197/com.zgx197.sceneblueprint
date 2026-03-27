#nullable enable
using System;
using System.Runtime.Serialization;

namespace SceneBlueprint.Contract
{
    [DataContract]
    [Serializable]
    public sealed class SemanticDescriptorSet
    {
        [DataMember(Order = 0, EmitDefaultValue = false)]
        public SubjectSemanticDescriptor[] Subjects = Array.Empty<SubjectSemanticDescriptor>();

        [DataMember(Order = 1, EmitDefaultValue = false)]
        public TargetSemanticDescriptor[] Targets = Array.Empty<TargetSemanticDescriptor>();

        [DataMember(Order = 2, EmitDefaultValue = false)]
        public ConditionSemanticDescriptor[] Conditions = Array.Empty<ConditionSemanticDescriptor>();

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public ValueSemanticDescriptor[] Values = Array.Empty<ValueSemanticDescriptor>();

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public GraphSemanticDescriptor[] Graphs = Array.Empty<GraphSemanticDescriptor>();

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public EventContextSemanticDescriptor[] EventContexts = Array.Empty<EventContextSemanticDescriptor>();
    }

    [DataContract]
    [Serializable]
    public sealed class SubjectSemanticDescriptor
    {
        [DataMember(Order = 0)]
        public string Slot = "subject";

        [DataMember(Order = 1)]
        public string Kind = "entity-ref";

        [DataMember(Order = 2)]
        public string Reference = string.Empty;

        [DataMember(Order = 3)]
        public string Summary = string.Empty;

        [DataMember(Order = 4)]
        public string CompiledSubjectId = string.Empty;

        [DataMember(Order = 5)]
        public string PublicSubjectId = string.Empty;

        [DataMember(Order = 6)]
        public string RuntimeEntityId = string.Empty;

        [DataMember(Order = 7)]
        public string BindingKey = string.Empty;

        [DataMember(Order = 8)]
        public string BindingType = string.Empty;

        [DataMember(Order = 9)]
        public string StableObjectId = string.Empty;

        [DataMember(Order = 10)]
        public string SceneObjectId = string.Empty;
    }

    [DataContract]
    [Serializable]
    public sealed class TargetSemanticDescriptor
    {
        [DataMember(Order = 0)]
        public string Slot = "target";

        [DataMember(Order = 1)]
        public string Kind = "entity-ref";

        [DataMember(Order = 2)]
        public string Reference = string.Empty;

        [DataMember(Order = 3)]
        public string Summary = string.Empty;

        [DataMember(Order = 4)]
        public string CompiledSubjectId = string.Empty;

        [DataMember(Order = 5)]
        public string PublicSubjectId = string.Empty;

        [DataMember(Order = 6)]
        public string RuntimeEntityId = string.Empty;

        [DataMember(Order = 7)]
        public string BindingKey = string.Empty;

        [DataMember(Order = 8)]
        public string BindingType = string.Empty;

        [DataMember(Order = 9)]
        public string StableObjectId = string.Empty;

        [DataMember(Order = 10)]
        public string SceneObjectId = string.Empty;
    }

    [DataContract]
    [Serializable]
    public sealed class ConditionSemanticDescriptor
    {
        [DataMember(Order = 0)]
        public string Kind = string.Empty;

        [DataMember(Order = 1)]
        public string Type = string.Empty;

        [DataMember(Order = 2)]
        public string Summary = string.Empty;

        [DataMember(Order = 3)]
        public string ParameterSummary = string.Empty;

        [DataMember(Order = 4)]
        public string ParametersRaw = string.Empty;

        [DataMember(Order = 5)]
        public string Operator = string.Empty;

        [DataMember(Order = 6)]
        public string SignalTag = string.Empty;

        [DataMember(Order = 7)]
        public bool IsWildcardPattern;

        [DataMember(Order = 8)]
        public float TimeoutSeconds;

        [DataMember(Order = 9)]
        public bool Repeat;

        [DataMember(Order = 10)]
        public string Mode = string.Empty;

        [DataMember(Order = 11)]
        public int RequiredCount;

        [DataMember(Order = 12)]
        public string RoutedPort = string.Empty;

        [DataMember(Order = 13, EmitDefaultValue = false)]
        public string[] ConnectedPortIds = Array.Empty<string>();

        [DataMember(Order = 14, EmitDefaultValue = false)]
        public string[] IncomingActionIds = Array.Empty<string>();
    }

    [DataContract]
    [Serializable]
    public sealed class ValueSemanticDescriptor
    {
        [DataMember(Order = 0)]
        public string Kind = string.Empty;

        [DataMember(Order = 1)]
        public string Key = string.Empty;

        [DataMember(Order = 2)]
        public string ValueType = string.Empty;

        [DataMember(Order = 3)]
        public string RawValue = string.Empty;

        [DataMember(Order = 4)]
        public string NormalizedValue = string.Empty;

        [DataMember(Order = 5)]
        public string Summary = string.Empty;
    }

    [DataContract]
    [Serializable]
    public sealed class GraphSemanticDescriptor
    {
        [DataMember(Order = 0)]
        public string Kind = string.Empty;

        [DataMember(Order = 1)]
        public string Summary = string.Empty;

        [DataMember(Order = 2)]
        public string Mode = string.Empty;

        [DataMember(Order = 3)]
        public string RoutedPort = string.Empty;

        [DataMember(Order = 4)]
        public int RequiredCount;

        [DataMember(Order = 5)]
        public int ConnectedCount;

        [DataMember(Order = 6)]
        public int ConnectedMask;

        [DataMember(Order = 7, EmitDefaultValue = false)]
        public string[] ConnectedPortIds = Array.Empty<string>();

        [DataMember(Order = 8, EmitDefaultValue = false)]
        public string[] IncomingActionIds = Array.Empty<string>();
    }

    [DataContract]
    [Serializable]
    public sealed class EventContextSemanticDescriptor
    {
        [DataMember(Order = 0)]
        public string Kind = string.Empty;

        [DataMember(Order = 1)]
        public string EventKind = string.Empty;

        [DataMember(Order = 2)]
        public string SignalTag = string.Empty;

        [DataMember(Order = 3)]
        public string PayloadSummary = string.Empty;

        [DataMember(Order = 4)]
        public string Summary = string.Empty;
    }
}
