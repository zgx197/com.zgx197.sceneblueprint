#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using SceneBlueprint.Contract;

namespace SceneBlueprint.Runtime.State
{
    public sealed class RuntimeStateInspector : IRuntimeStateInspector
    {
        private readonly IRuntimeStateBackend? _backend;
        private readonly Func<IReadOnlyList<IRuntimeStateDomain>> _domainsProvider;

        public RuntimeStateInspector()
            : this(null)
        {
        }

        public RuntimeStateInspector(
            IRuntimeStateBackend? backend,
            Func<IReadOnlyList<IRuntimeStateDomain>>? domainsProvider = null)
        {
            _backend = backend;
            _domainsProvider = domainsProvider ?? (() => Array.Empty<IRuntimeStateDomain>());
        }

        public ObservationResult Inspect(ObservationRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Observation request must be valid.", nameof(request));
            }

            var records = RuntimeStateProjectionSupport.ResolveEntries(
                request.TargetKind,
                request.DomainId,
                request.EntryRef,
                includedDomains: null,
                _backend,
                _domainsProvider);

            var entries = new List<ObservationEntry>(records.Count);
            for (var index = 0; index < records.Count; index++)
            {
                var entry = records[index];
                if (!entry.Descriptor.IsInspectable)
                {
                    continue;
                }

                entries.Add(RuntimeStateProjectionSupport.CreateObservationEntry(
                    entry,
                    request.IncludeChildren,
                    request.MaxDepth,
                    request.FieldFilter));
            }

            return new ObservationResult(
                request.TargetKind,
                entries,
                generatedAtTick: null,
                appliedFieldFilter: request.FieldFilter);
        }
    }

    internal static class RuntimeStateProjectionSupport
    {
        private const string RootPath = "$";
        private const string RootFieldName = "Entry";

        private static readonly StringComparer PathComparer = StringComparer.Ordinal;
        private static readonly BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public;

        internal static List<RuntimeStateEntryRecord> ResolveEntries(
            ObservationTargetKind targetKind,
            StateDomainId? domainId,
            RuntimeEntryRef? entryRef,
            IReadOnlyList<StateDomainId>? includedDomains,
            IRuntimeStateBackend? backend,
            Func<IReadOnlyList<IRuntimeStateDomain>> domainsProvider)
        {
            if (backend is null)
            {
                return new List<RuntimeStateEntryRecord>();
            }

            var result = new List<RuntimeStateEntryRecord>();

            switch (targetKind)
            {
                case ObservationTargetKind.Host:
                {
                    var includeSet = CreateIncludedDomainSet(includedDomains);
                    var domains = domainsProvider();
                    for (var index = 0; index < domains.Count; index++)
                    {
                        var domain = domains[index];
                        if (includeSet is not null && !includeSet.Contains(domain.DomainId))
                        {
                            continue;
                        }

                        AppendDomainEntries(result, backend, domain);
                    }

                    break;
                }

                case ObservationTargetKind.Domain:
                {
                    if (!domainId.HasValue || !domainId.Value.IsValid)
                    {
                        break;
                    }

                    var domains = domainsProvider();
                    for (var index = 0; index < domains.Count; index++)
                    {
                        if (domains[index].DomainId != domainId.Value)
                        {
                            continue;
                        }

                        AppendDomainEntries(result, backend, domains[index]);
                        break;
                    }

                    break;
                }

                case ObservationTargetKind.Entry:
                {
                    if (entryRef.HasValue && backend.TryGetEntry(entryRef.Value, out var entry) && entry is not null)
                    {
                        result.Add(entry);
                    }

                    break;
                }
            }

            result.Sort(static (left, right) =>
            {
                var domainComparison = string.CompareOrdinal(left.EntryRef.DomainId.Value, right.EntryRef.DomainId.Value);
                return domainComparison != 0
                    ? domainComparison
                    : string.CompareOrdinal(left.EntryRef.EntryId, right.EntryRef.EntryId);
            });
            return result;
        }

        internal static ObservationEntry CreateObservationEntry(
            RuntimeStateEntryRecord entry,
            bool includeChildren,
            int? maxDepth,
            FieldFilter? fieldFilter)
        {
            return new ObservationEntry(
                CreateLogicalEntryKey(entry),
                CreateSampleId(entry),
                entry.EntryRef.DomainId,
                BuildEntryProjection(entry, includeChildren, maxDepth, fieldFilter),
                entry.OwnerRef.RuntimeInstanceId,
                entry.EntryRef);
        }

        internal static SnapshotEntry CreateSnapshotEntry(
            RuntimeStateEntryRecord entry,
            FieldFilter? fieldFilter,
            IReadOnlyList<IRuntimeStateDomain>? domains = null,
            RuntimeSnapshotExporterRegistry? exporterRegistry = null)
        {
            if (!entry.Descriptor.AllowSnapshot)
            {
                return CreateNoneSnapshotEntry(entry);
            }

            if (TryResolveSnapshotPolicy(entry, domains, out var policy))
            {
                switch (policy.ExportMode)
                {
                    case SnapshotExportMode.None:
                        return CreateNoneSnapshotEntry(entry);

                    case SnapshotExportMode.Summary:
                        return CreateSummarySnapshotEntry(entry, fieldFilter);

                    case SnapshotExportMode.State:
                        if (exporterRegistry is not null
                            && exporterRegistry.TryExport(
                                entry,
                                fieldFilter,
                                policy.PreferredExporterId,
                                policy.AllowExporterFallback,
                                out var policyStateExport)
                            && policyStateExport.Payload is not null)
                        {
                            return CreateStateSnapshotEntry(entry, policyStateExport);
                        }

                        return policy.AllowSummaryFallback
                            ? CreateSummarySnapshotEntry(entry, fieldFilter)
                            : CreateNoneSnapshotEntry(entry);
                }
            }

            if (exporterRegistry is not null
                && exporterRegistry.TryExport(
                    entry,
                    fieldFilter,
                    preferredExporterId: null,
                    allowExporterFallback: true,
                    out var stateExport)
                && stateExport.Payload is not null)
            {
                return CreateStateSnapshotEntry(entry, stateExport);
            }

            return CreateSummarySnapshotEntry(entry, fieldFilter);
        }

        internal static SnapshotCapability CreateCapability(
            RuntimeStateEntryRecord entry,
            IReadOnlyList<IRuntimeStateDomain>? domains = null,
            RuntimeSnapshotExporterRegistry? exporterRegistry = null)
        {
            if (!entry.Descriptor.AllowSnapshot)
            {
                return CreateNoneCapability(entry, isExplicitPolicy: false, policySourceId: "descriptor.allowSnapshot:false");
            }

            if (TryResolveSnapshotPolicy(entry, domains, out var policy))
            {
                switch (policy.ExportMode)
                {
                    case SnapshotExportMode.None:
                        return CreateNoneCapability(entry, policy.IsExplicitPolicy, policy.PolicySourceId);

                    case SnapshotExportMode.Summary:
                        return CreateSummaryCapability(entry, policy.IsExplicitPolicy, policy.PolicySourceId);

                    case SnapshotExportMode.State:
                        if (exporterRegistry is not null
                            && exporterRegistry.TryCreateCapability(
                                entry,
                                policy.PreferredExporterId,
                                policy.AllowExporterFallback,
                                policy.IsExplicitPolicy,
                                policy.PolicySourceId,
                                out var policyCapability)
                            && policyCapability.IsValid)
                        {
                            return policyCapability;
                        }

                        return policy.AllowSummaryFallback
                            ? CreateSummaryCapability(entry, policy.IsExplicitPolicy, policy.PolicySourceId)
                            : CreateNoneCapability(entry, policy.IsExplicitPolicy, policy.PolicySourceId);
                }
            }

            if (exporterRegistry is not null
                && exporterRegistry.TryCreateCapability(
                    entry,
                    preferredExporterId: null,
                    allowExporterFallback: true,
                    isExplicitPolicy: true,
                    policySourceId: null,
                    out var exportedCapability)
                && exportedCapability.IsValid)
            {
                return exportedCapability;
            }

            return CreateSummaryCapability(entry, isExplicitPolicy: false, policySourceId: null);
        }

        private static SnapshotEntry CreateNoneSnapshotEntry(RuntimeStateEntryRecord entry)
        {
            return new SnapshotEntry(
                CreateLogicalEntryKey(entry),
                entry.EntryRef.DomainId,
                SnapshotExportMode.None,
                SnapshotRestoreMode.None,
                SnapshotPayloadKind.None,
                payload: null,
                entry.OwnerRef.RuntimeInstanceId,
                entry.EntryRef);
        }

        private static SnapshotEntry CreateSummarySnapshotEntry(RuntimeStateEntryRecord entry, FieldFilter? fieldFilter)
        {
            return new SnapshotEntry(
                CreateLogicalEntryKey(entry),
                entry.EntryRef.DomainId,
                SnapshotExportMode.Summary,
                SnapshotRestoreMode.None,
                SnapshotPayloadKind.SummaryFields,
                BuildEntryProjection(entry, includeChildren: true, maxDepth: null, fieldFilter),
                entry.OwnerRef.RuntimeInstanceId,
                entry.EntryRef);
        }

        private static SnapshotEntry CreateStateSnapshotEntry(RuntimeStateEntryRecord entry, RuntimeStateSnapshotExport stateExport)
        {
            return new SnapshotEntry(
                CreateLogicalEntryKey(entry),
                entry.EntryRef.DomainId,
                SnapshotExportMode.State,
                stateExport.RestoreMode,
                SnapshotPayloadKind.StatePayload,
                stateExport.Payload,
                entry.OwnerRef.RuntimeInstanceId,
                entry.EntryRef);
        }

        private static SnapshotCapability CreateNoneCapability(
            RuntimeStateEntryRecord entry,
            bool isExplicitPolicy,
            string? policySourceId)
        {
            return new SnapshotCapability(
                entry.EntryRef,
                SnapshotExportMode.None,
                SnapshotRestoreMode.None,
                isExplicitPolicy,
                policySourceId: policySourceId);
        }

        private static SnapshotCapability CreateSummaryCapability(
            RuntimeStateEntryRecord entry,
            bool isExplicitPolicy,
            string? policySourceId)
        {
            return new SnapshotCapability(
                entry.EntryRef,
                SnapshotExportMode.Summary,
                SnapshotRestoreMode.None,
                isExplicitPolicy,
                policySourceId: policySourceId);
        }

        private static ObservationFieldNode BuildEntryProjection(
            RuntimeStateEntryRecord entry,
            bool includeChildren,
            int? maxDepth,
            FieldFilter? fieldFilter)
        {
            var root = CreateRootNode(entry, includeChildren);
            if (includeChildren && maxDepth.HasValue)
            {
                root = ApplyMaxDepth(root, currentDepth: 0, maxDepth.Value);
            }

            if (!fieldFilter.HasValue)
            {
                return root;
            }

            return ApplyFieldFilter(root, fieldFilter.Value) ?? CreateEmptyProjectionRoot(root);
        }

        private static ObservationFieldNode CreateRootNode(RuntimeStateEntryRecord entry, bool includeChildren)
        {
            var children = includeChildren
                ? new[]
                {
                    CreateDescriptorNode(entry.Descriptor),
                    CreateOwnerNode(entry.OwnerRef),
                    CreateScalarNode("SlotKey", "SlotKey", entry.SlotKey),
                    CreateStateNode("State", "State", entry.State, new HashSet<object>(ReferenceEqualityComparer.Instance)),
                }
                : Array.Empty<ObservationFieldNode>();

            return new ObservationFieldNode(
                RootPath,
                RootFieldName,
                ObservationValueKind.Object,
                valueSummary: $"{entry.Descriptor.Id}@{entry.EntryRef.EntryId}",
                typeName: entry.State?.GetType().FullName ?? entry.Descriptor.DebugName,
                children: children,
                isTruncated: !includeChildren);
        }

        private static ObservationFieldNode CreateDescriptorNode(StateDescriptor descriptor)
        {
            return new ObservationFieldNode(
                "Descriptor",
                "Descriptor",
                ObservationValueKind.Object,
                typeName: typeof(StateDescriptor).FullName,
                children: new[]
                {
                    CreateScalarNode("Descriptor.Id", "Id", descriptor.Id),
                    CreateScalarNode("Descriptor.DebugName", "DebugName", descriptor.DebugName),
                    CreateScalarNode("Descriptor.DomainId", "DomainId", descriptor.DomainId.ToString()),
                    CreateScalarNode("Descriptor.Lifetime", "Lifetime", descriptor.Lifetime.ToString()),
                    CreateScalarNode("Descriptor.IsInspectable", "IsInspectable", descriptor.IsInspectable),
                    CreateScalarNode("Descriptor.AllowSnapshot", "AllowSnapshot", descriptor.AllowSnapshot),
                });
        }

        private static ObservationFieldNode CreateOwnerNode(StateOwnerRef ownerRef)
        {
            return new ObservationFieldNode(
                "Owner",
                "Owner",
                ObservationValueKind.Object,
                typeName: typeof(StateOwnerRef).FullName,
                children: new[]
                {
                    CreateScalarNode("Owner.Kind", "Kind", ownerRef.Kind.ToString()),
                    CreateScalarNode("Owner.LogicalKey", "LogicalKey", ownerRef.LogicalKey),
                    CreateNullableScalarNode("Owner.RuntimeInstanceId", "RuntimeInstanceId", ownerRef.RuntimeInstanceId),
                });
        }

        private static ObservationFieldNode CreateStateNode(
            string path,
            string fieldName,
            object? value,
            HashSet<object> visitedObjects)
        {
            if (value is null)
            {
                return new ObservationFieldNode(path, fieldName, ObservationValueKind.Null, typeName: null);
            }

            var valueType = value.GetType();
            if (TryCreateScalarSummary(valueType, value, out var summary))
            {
                return new ObservationFieldNode(
                    path,
                    fieldName,
                    ObservationValueKind.Scalar,
                    valueSummary: summary,
                    typeName: valueType.FullName);
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                return CreateCollectionNode(path, fieldName, enumerable, valueType);
            }

            if (!ShouldReflectObject(valueType))
            {
                return new ObservationFieldNode(
                    path,
                    fieldName,
                    ObservationValueKind.Summary,
                    valueSummary: value.ToString(),
                    typeName: valueType.FullName);
            }

            if (!valueType.IsValueType && !visitedObjects.Add(value))
            {
                return new ObservationFieldNode(
                    path,
                    fieldName,
                    ObservationValueKind.Summary,
                    valueSummary: "CycleDetected",
                    typeName: valueType.FullName,
                    isTruncated: true);
            }

            try
            {
                var children = CreateObjectChildren(path, value, valueType, visitedObjects);
                if (children.Count == 0)
                {
                    return new ObservationFieldNode(
                        path,
                        fieldName,
                        ObservationValueKind.Summary,
                        valueSummary: value.ToString(),
                        typeName: valueType.FullName);
                }

                return new ObservationFieldNode(
                    path,
                    fieldName,
                    ObservationValueKind.Object,
                    typeName: valueType.FullName,
                    children: children);
            }
            finally
            {
                if (!valueType.IsValueType)
                {
                    visitedObjects.Remove(value);
                }
            }
        }

        private static List<ObservationFieldNode> CreateObjectChildren(
            string path,
            object value,
            Type valueType,
            HashSet<object> visitedObjects)
        {
            var nodes = new List<ObservationFieldNode>();

            var properties = valueType.GetProperties(MemberFlags);
            Array.Sort(properties, static (left, right) => string.CompareOrdinal(left.Name, right.Name));
            for (var index = 0; index < properties.Length; index++)
            {
                var property = properties[index];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                nodes.Add(CreateStateNode(
                    ComposeChildPath(path, property.Name),
                    property.Name,
                    property.GetValue(value),
                    visitedObjects));
            }

            var fields = valueType.GetFields(MemberFlags);
            Array.Sort(fields, static (left, right) => string.CompareOrdinal(left.Name, right.Name));
            for (var index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                if (field.IsSpecialName)
                {
                    continue;
                }

                nodes.Add(CreateStateNode(
                    ComposeChildPath(path, field.Name),
                    field.Name,
                    field.GetValue(value),
                    visitedObjects));
            }

            return nodes;
        }

        private static ObservationFieldNode CreateCollectionNode(
            string path,
            string fieldName,
            IEnumerable enumerable,
            Type valueType)
        {
            var count = TryGetCollectionCount(enumerable);
            var children = count.HasValue
                ? new[] { CreateScalarNode(ComposeChildPath(path, "Count"), "Count", count.Value) }
                : Array.Empty<ObservationFieldNode>();

            return new ObservationFieldNode(
                path,
                fieldName,
                ObservationValueKind.Collection,
                valueSummary: count.HasValue ? $"Count={count.Value.ToString(CultureInfo.InvariantCulture)}" : valueType.Name,
                typeName: valueType.FullName,
                children: children,
                isTruncated: true);
        }

        private static ObservationFieldNode ApplyMaxDepth(ObservationFieldNode node, int currentDepth, int maxDepth)
        {
            if (currentDepth >= maxDepth)
            {
                return node.Children.Count == 0
                    ? node
                    : CloneNode(node, Array.Empty<ObservationFieldNode>(), isTruncated: true);
            }

            if (node.Children.Count == 0)
            {
                return node;
            }

            var trimmedChildren = new ObservationFieldNode[node.Children.Count];
            var changed = false;
            for (var index = 0; index < node.Children.Count; index++)
            {
                var trimmedChild = ApplyMaxDepth(node.Children[index], currentDepth + 1, maxDepth);
                trimmedChildren[index] = trimmedChild;
                changed |= !ReferenceEquals(trimmedChild, node.Children[index]) || trimmedChild.IsTruncated;
            }

            return changed
                ? CloneNode(node, trimmedChildren, isTruncated: node.IsTruncated || changed)
                : node;
        }

        private static ObservationFieldNode? ApplyFieldFilter(ObservationFieldNode node, FieldFilter filter)
        {
            return filter.Mode switch
            {
                FieldFilterMode.AllowAll => node,
                FieldFilterMode.IncludeListed => ApplyIncludeFilter(node, filter.Paths),
                FieldFilterMode.ExcludeListed => ApplyExcludeFilter(node, filter.Paths),
                _ => node,
            };
        }

        private static ObservationFieldNode? ApplyIncludeFilter(
            ObservationFieldNode node,
            IReadOnlyList<string> paths)
        {
            var relativePath = ToRelativePath(node.Path);
            var keepSelf = relativePath.Length == 0 || MatchesIncludedPath(relativePath, paths);
            var filteredChildren = FilterChildren(node, paths, includeMode: true);
            if (!keepSelf && filteredChildren.Count == 0)
            {
                return null;
            }

            return CloneNode(node, filteredChildren, isTruncated: node.IsTruncated || filteredChildren.Count != node.Children.Count);
        }

        private static ObservationFieldNode? ApplyExcludeFilter(
            ObservationFieldNode node,
            IReadOnlyList<string> paths)
        {
            var relativePath = ToRelativePath(node.Path);
            if (relativePath.Length != 0 && MatchesExcludedPath(relativePath, paths))
            {
                return null;
            }

            var filteredChildren = FilterChildren(node, paths, includeMode: false);
            return CloneNode(node, filteredChildren, isTruncated: node.IsTruncated || filteredChildren.Count != node.Children.Count);
        }

        private static List<ObservationFieldNode> FilterChildren(
            ObservationFieldNode node,
            IReadOnlyList<string> paths,
            bool includeMode)
        {
            var filteredChildren = new List<ObservationFieldNode>(node.Children.Count);
            for (var index = 0; index < node.Children.Count; index++)
            {
                var child = node.Children[index];
                var filtered = includeMode
                    ? ApplyIncludeFilter(child, paths)
                    : ApplyExcludeFilter(child, paths);
                if (filtered is not null)
                {
                    filteredChildren.Add(filtered);
                }
            }

            return filteredChildren;
        }

        private static bool MatchesIncludedPath(string relativePath, IReadOnlyList<string> paths)
        {
            for (var index = 0; index < paths.Count; index++)
            {
                var candidate = NormalizePath(paths[index]);
                if (candidate.Length == 0)
                {
                    continue;
                }

                if (PathComparer.Equals(relativePath, candidate)
                    || relativePath.StartsWith(candidate + ".", StringComparison.Ordinal)
                    || candidate.StartsWith(relativePath + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesExcludedPath(string relativePath, IReadOnlyList<string> paths)
        {
            for (var index = 0; index < paths.Count; index++)
            {
                var candidate = NormalizePath(paths[index]);
                if (candidate.Length == 0)
                {
                    continue;
                }

                if (PathComparer.Equals(relativePath, candidate)
                    || relativePath.StartsWith(candidate + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static ObservationFieldNode CreateEmptyProjectionRoot(ObservationFieldNode template)
        {
            return CloneNode(template, Array.Empty<ObservationFieldNode>(), isTruncated: true);
        }

        private static ObservationFieldNode CloneNode(
            ObservationFieldNode node,
            IReadOnlyList<ObservationFieldNode> children,
            bool isTruncated)
        {
            return new ObservationFieldNode(
                node.Path,
                node.FieldName,
                node.ValueKind,
                node.ValueSummary,
                node.TypeName,
                node.Tags,
                children,
                isTruncated);
        }

        private static HashSet<StateDomainId>? CreateIncludedDomainSet(IReadOnlyList<StateDomainId>? includedDomains)
        {
            if (includedDomains is null)
            {
                return null;
            }

            var set = new HashSet<StateDomainId>();
            for (var index = 0; index < includedDomains.Count; index++)
            {
                if (includedDomains[index].IsValid)
                {
                    set.Add(includedDomains[index]);
                }
            }

            return set;
        }

        private static bool TryResolveSnapshotPolicy(
            RuntimeStateEntryRecord entry,
            IReadOnlyList<IRuntimeStateDomain>? domains,
            out RuntimeSnapshotPolicy policy)
        {
            if (domains is not null)
            {
                for (var index = 0; index < domains.Count; index++)
                {
                    if (domains[index].DomainId != entry.EntryRef.DomainId
                        || domains[index] is not IRuntimeStateSnapshotPolicyProvider provider)
                    {
                        continue;
                    }

                    if (provider.TryGetSnapshotPolicy(entry, out policy) && policy.IsValid)
                    {
                        return true;
                    }
                }
            }

            policy = RuntimeSnapshotPolicy.Invalid;
            return false;
        }

        private static void AppendDomainEntries(
            List<RuntimeStateEntryRecord> result,
            IRuntimeStateBackend backend,
            IRuntimeStateDomain domain)
        {
            var entryRefs = domain.EnumerateEntries();
            for (var index = 0; index < entryRefs.Count; index++)
            {
                if (backend.TryGetEntry(entryRefs[index], out var entry) && entry is not null)
                {
                    result.Add(entry);
                }
            }
        }

        private static string CreateLogicalEntryKey(RuntimeStateEntryRecord entry)
        {
            return string.Concat(
                entry.EntryRef.DomainId.Value,
                "|",
                entry.OwnerRef.Kind.ToString(),
                "|",
                entry.OwnerRef.LogicalKey,
                "|",
                entry.SlotKey);
        }

        private static string CreateSampleId(RuntimeStateEntryRecord entry)
        {
            return entry.EntryRef.ToString();
        }

        private static bool TryCreateScalarSummary(Type type, object value, out string summary)
        {
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            if (underlyingType.IsEnum)
            {
                summary = value.ToString() ?? string.Empty;
                return true;
            }

            if (underlyingType == typeof(string)
                || underlyingType == typeof(char)
                || underlyingType == typeof(bool)
                || underlyingType == typeof(byte)
                || underlyingType == typeof(sbyte)
                || underlyingType == typeof(short)
                || underlyingType == typeof(ushort)
                || underlyingType == typeof(int)
                || underlyingType == typeof(uint)
                || underlyingType == typeof(long)
                || underlyingType == typeof(ulong)
                || underlyingType == typeof(float)
                || underlyingType == typeof(double)
                || underlyingType == typeof(decimal)
                || underlyingType == typeof(Guid)
                || underlyingType == typeof(DateTime)
                || underlyingType == typeof(DateTimeOffset)
                || underlyingType == typeof(TimeSpan)
                || underlyingType == typeof(StateDomainId)
                || underlyingType == typeof(StateOwnerRef)
                || underlyingType == typeof(RuntimeEntryRef)
                || underlyingType == typeof(RuntimeTick))
            {
                summary = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                return true;
            }

            summary = string.Empty;
            return false;
        }

        private static bool ShouldReflectObject(Type type)
        {
            if (type == typeof(string))
            {
                return false;
            }

            return true;
        }

        private static int? TryGetCollectionCount(IEnumerable enumerable)
        {
            if (enumerable is ICollection collection)
            {
                return collection.Count;
            }

            var countProperty = enumerable.GetType().GetProperty("Count", MemberFlags);
            if (countProperty?.CanRead == true && countProperty.PropertyType == typeof(int))
            {
                return (int?)countProperty.GetValue(enumerable);
            }

            return null;
        }

        private static string ComposeChildPath(string parentPath, string childName)
        {
            return parentPath == RootPath ? childName : string.Concat(parentPath, ".", childName);
        }

        private static string ToRelativePath(string path)
        {
            return path == RootPath ? string.Empty : path;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
        }

        private static ObservationFieldNode CreateScalarNode(string path, string fieldName, object value)
        {
            return new ObservationFieldNode(
                path,
                fieldName,
                ObservationValueKind.Scalar,
                valueSummary: Convert.ToString(value, CultureInfo.InvariantCulture),
                typeName: value.GetType().FullName);
        }

        private static ObservationFieldNode CreateNullableScalarNode(string path, string fieldName, string? value)
        {
            return value is null
                ? new ObservationFieldNode(path, fieldName, ObservationValueKind.Null, typeName: typeof(string).FullName)
                : CreateScalarNode(path, fieldName, value);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
