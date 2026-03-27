#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Core;

namespace SceneBlueprint.Editor.Drawers
{
    /// <summary>
    /// 为 EntityRef 的 BySceneRef 模式提供当前蓝图可见的静态场景对象候选。
    /// 候选负责蓝图定义层可读性，真正进入运行时主链的仍然是稳定 SceneObjectId。
    /// </summary>
    public static class EntityRefSceneCandidateProvider
    {
        public static IReadOnlyList<EntityRefSceneCandidate> BuildCandidates(Graph? graph)
        {
            if (graph == null)
            {
                return Array.Empty<EntityRefSceneCandidate>();
            }

            var proxies = new List<SceneObjectProxyData>();
            foreach (var node in graph.Nodes)
            {
                if (node.UserData is SceneObjectProxyData proxyData)
                {
                    proxies.Add(proxyData);
                }
            }

            return BuildCandidates(proxies);
        }

        public static IReadOnlyList<EntityRefSceneCandidate> BuildCandidates(IEnumerable<SceneObjectProxyData>? proxies)
        {
            if (proxies == null)
            {
                return Array.Empty<EntityRefSceneCandidate>();
            }

            var unique = new Dictionary<string, EntityRefSceneCandidate>(StringComparer.Ordinal);
            foreach (var proxy in proxies)
            {
                if (proxy == null
                    || proxy.IsBroken
                    || string.IsNullOrWhiteSpace(proxy.SceneObjectId))
                {
                    continue;
                }

                var sceneObjectId = EntityRefSceneIdentityConventions.NormalizeSceneObjectId(proxy.SceneObjectId);
                if (unique.ContainsKey(sceneObjectId))
                {
                    continue;
                }

                unique.Add(sceneObjectId, new EntityRefSceneCandidate(
                    sceneObjectId,
                    EntityRefSceneIdentityConventions.BuildHumanReadableAlias(sceneObjectId, proxy.DisplayName),
                    EntityRefSceneIdentityConventions.NormalizeObjectType(proxy.ObjectType),
                    proxy.DisplayName ?? string.Empty));
            }

            return unique.Values
                .OrderBy(candidate => candidate.HumanReadableAlias, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.ObjectType, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.SceneObjectId, StringComparer.Ordinal)
                .ToArray();
        }

        public static int FindCandidateIndex(string sceneObjectId, IReadOnlyList<EntityRefSceneCandidate>? candidates)
        {
            return EntityRefSceneIdentityConventions.FindCandidateIndex(sceneObjectId, candidates);
        }

        public static string ResolveSceneObjectIdFromPopup(
            string currentValue,
            int popupIndex,
            IReadOnlyList<EntityRefSceneCandidate>? candidates)
        {
            if (popupIndex <= 0 || candidates == null)
            {
                return currentValue;
            }

            var candidateIndex = popupIndex - 1;
            if (candidateIndex < 0 || candidateIndex >= candidates.Count)
            {
                return currentValue;
            }

            return candidates[candidateIndex].SceneObjectId;
        }
    }

    public readonly struct EntityRefSceneCandidate
    {
        public string SceneObjectId { get; }
        public string HumanReadableAlias { get; }
        public string ObjectType { get; }
        public string SourceDisplayName { get; }
        public string SubjectSummary => EntityRefSceneIdentityConventions.BuildSubjectSummary(SceneObjectId, SourceDisplayName, ObjectType);
        public string RuntimeIdentity => EntityRefSceneIdentityConventions.BuildRuntimeIdentity(SceneObjectId);

        public string DisplayLabel => $"{HumanReadableAlias} [{ObjectType}] ({SceneObjectId})";

        public EntityRefSceneCandidate(
            string sceneObjectId,
            string humanReadableAlias,
            string objectType,
            string sourceDisplayName)
        {
            SceneObjectId = sceneObjectId ?? string.Empty;
            HumanReadableAlias = humanReadableAlias ?? string.Empty;
            ObjectType = objectType ?? string.Empty;
            SourceDisplayName = sourceDisplayName ?? string.Empty;
        }
    }
}
