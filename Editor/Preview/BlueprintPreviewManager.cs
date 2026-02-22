#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.Markers.Pipeline;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Preview
{
    /// <summary>
    /// Blueprint 预览管理器——负责生成和管理 Blueprint 节点的 SceneView 预览数据。
    /// <para>
    /// 工作流程：
    /// 1. SceneBlueprintWindow 在资源加载时调用 RefreshAllPreviews
    /// 2. 根据节点类型生成预览数据（如 Location.RandomArea 生成位置列表）
    /// 3. AreaMarkerRenderer 在绘制时读取预览数据并绘制到 SceneView
    /// 4. 预览可见性受 MarkerLayerSystem.IsPreviewVisible() 控制
    /// </para>
    /// </summary>
    public class BlueprintPreviewManager
    {
        // ── 多实例注册表（A3：按 sessionKey 独立注册，支持多窗口）──
        private static readonly Dictionary<string, BlueprintPreviewManager> _registry = new();

        /// <summary>供 Session 调用：注册实例。sessionKey 建议使用 BlueprintId 或 session.GetHashCode()。</summary>
        internal static void Register(string sessionKey, BlueprintPreviewManager manager)
            => _registry[sessionKey] = manager;

        /// <summary>供 Session 调用：注销实例。</summary>
        internal static void Unregister(string sessionKey) => _registry.Remove(sessionKey);

        /// <summary>返回所有已注册实例的预览（多窗口联合输出）。场景渲染代码使用此方法。</summary>
        public static IEnumerable<PreviewData> GetAllRegisteredPreviews()
        {
            foreach (var mgr in _registry.Values)
                foreach (var p in mgr.GetCurrentBlueprintPreviews())
                    yield return p;
        }

        /// <summary>尝试按 sessionKey 查找实例。</summary>
        internal static bool TryGet(string sessionKey, out BlueprintPreviewManager? manager)
            => _registry.TryGetValue(sessionKey, out manager);

        // 所有预览数据：nodeId -> PreviewData
        private readonly Dictionary<string, PreviewData> _allPreviews = new();
        private readonly Dictionary<string, int> _nodePreviewSignatures = new();

        // 当前激活的 Blueprint ID
        private string? _currentBlueprintId;

        /// <summary>当前是否存在任何预览缓存</summary>
        public bool HasAnyPreviews => _allPreviews.Count > 0;

        /// <summary>获取当前 Blueprint 的所有预览（返回所有预览，不过滤 BlueprintId）</summary>
        public IEnumerable<PreviewData> GetCurrentBlueprintPreviews()
        {
            // 如果没有设置 BlueprintId，返回所有预览（兼容窗口状态恢复场景）
            if (string.IsNullOrEmpty(_currentBlueprintId))
                return _allPreviews.Values;
            
            var result = _allPreviews.Values
                .Where(p => p.BlueprintId == _currentBlueprintId)
                .ToList();
            
            return result;
        }

        /// <summary>
        /// 获取当前 Blueprint 预览中引用到的 MarkerId 集合。
        /// </summary>
        public IReadOnlyCollection<string> GetCurrentPreviewMarkerIds()
        {
            return GetCurrentBlueprintPreviews()
                .Select(p => p.SourceMarkerId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 刷新所有预览（Blueprint 加载时调用）
        /// </summary>
        public void RefreshAllPreviews(string blueprintId, Graph graph)
        {
            // 切换 Blueprint 时清空旧预览
            if (_currentBlueprintId != blueprintId)
            {
                _allPreviews.Clear();
                _nodePreviewSignatures.Clear();
                _currentBlueprintId = blueprintId;
            }

            // 清理已从图中删除的节点预览，避免 stale preview 残留。
            var aliveNodeIds = new HashSet<string>(graph.Nodes.Select(n => n.Id));
            int prunedCount = 0;
            if (_allPreviews.Count > 0)
            {
                var staleNodeIds = new List<string>();
                foreach (var pair in _allPreviews)
                {
                    if (!aliveNodeIds.Contains(pair.Key))
                        staleNodeIds.Add(pair.Key);
                }

                foreach (var staleNodeId in staleNodeIds)
                {
                    _allPreviews.Remove(staleNodeId);
                    _nodePreviewSignatures.Remove(staleNodeId);
                    prunedCount++;
                }

                if (prunedCount > 0)
                {
                    SBLog.Debug(
                        SBLogTags.Pipeline,
                        "RefreshAllPreviews: 清理过期预览 {0} 条, blueprint={1}",
                        prunedCount,
                        blueprintId);
                }
            }

            // 遍历所有节点，生成预览
            foreach (var node in graph.Nodes)
            {
                if (node.UserData is ActionNodeData nodeData)
                {
                    RefreshPreviewForNode(blueprintId, node.Id, nodeData);
                }
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// 刷新单个节点的预览（节点属性修改时调用）
        /// </summary>
        public void RefreshPreviewForNode(string blueprintId, string nodeId, ActionNodeData nodeData)
        {
            if (nodeData.ActionTypeId == "Location.RandomArea")
            {
                var areaBindingIdForLog = nodeData.Properties.Get<string>("area") ?? "";
                SBLog.Info(
                    SBLogTags.Pipeline,
                    "RefreshPreviewForNode: blueprint={0}, node={1}, action={2}, areaId='{3}'",
                    blueprintId,
                    nodeId,
                    nodeData.ActionTypeId,
                    areaBindingIdForLog);
            }

            // 根据节点类型生成预览
            switch (nodeData.ActionTypeId)
            {
                case "Location.RandomArea":
                    ReadRandomAreaSettings(
                        nodeData,
                        out string areaBindingId,
                        out int count,
                        out string distribution,
                        out float minSpacing);

                    SceneMarker? marker = string.IsNullOrEmpty(areaBindingId)
                        ? null
                        : FindMarkerById(areaBindingId);

                    int signature = ComputeRandomAreaPreviewSignature(
                        areaBindingId,
                        count,
                        distribution,
                        minSpacing,
                        marker);

                    if (_nodePreviewSignatures.TryGetValue(nodeId, out int cachedSignature)
                        && cachedSignature == signature
                        && _allPreviews.ContainsKey(nodeId))
                    {
                        SBLog.Debug(
                            SBLogTags.Pipeline,
                            "RefreshPreviewForNode跳过(签名未变化): node={0}, action={1}",
                            nodeId,
                            nodeData.ActionTypeId);
                        return;
                    }

                    GenerateLocationPreview(blueprintId, nodeId, nodeData);
                    _nodePreviewSignatures[nodeId] = signature;
                    break;

                // 未来可以支持其他类型
                // case "Patrol.Path":
                //     GeneratePatrolPathPreview(blueprintId, nodeId, nodeData);
                //     break;

                default:
                    // 不支持预览的节点，移除预览数据
                    _nodePreviewSignatures.Remove(nodeId);
                    RemovePreview(nodeId);
                    break;
            }
        }

        /// <summary>
        /// 移除指定节点的预览（节点删除时调用）
        /// </summary>
        public void RemovePreview(string nodeId)
        {
            _nodePreviewSignatures.Remove(nodeId);
            if (_allPreviews.Remove(nodeId))
            {
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// 批量移除预览（用于节点批量删除场景，避免逐条重绘）。
        /// </summary>
        public int RemovePreviews(IEnumerable<string> nodeIds, bool repaint = true)
        {
            int removedCount = 0;
            foreach (var nodeId in nodeIds)
            {
                if (string.IsNullOrEmpty(nodeId))
                    continue;

                _nodePreviewSignatures.Remove(nodeId);
                if (_allPreviews.Remove(nodeId))
                    removedCount++;
            }

            if (removedCount > 0 && repaint)
                SceneView.RepaintAll();

            return removedCount;
        }

        /// <summary>
        /// 清除所有预览
        /// </summary>
        public void ClearAllPreviews()
        {
            _allPreviews.Clear();
            _nodePreviewSignatures.Clear();
            _currentBlueprintId = null;
            SceneView.RepaintAll();
        }

        private static int QuantizeFloat(float value)
        {
            return Mathf.RoundToInt(value * 1000f);
        }

        private static int ComputeVector3Signature(Vector3 value)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + QuantizeFloat(value.x);
                hash = hash * 31 + QuantizeFloat(value.y);
                hash = hash * 31 + QuantizeFloat(value.z);
                return hash;
            }
        }

        private static int ComputeQuaternionSignature(Quaternion value)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + QuantizeFloat(value.x);
                hash = hash * 31 + QuantizeFloat(value.y);
                hash = hash * 31 + QuantizeFloat(value.z);
                hash = hash * 31 + QuantizeFloat(value.w);
                return hash;
            }
        }

        private static int ComputeSceneMarkerSignature(SceneMarker? marker)
        {
            if (marker == null)
                return int.MinValue;

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (marker.MarkerTypeId?.GetHashCode() ?? 0);
                hash = hash * 31 + ComputeVector3Signature(marker.transform.position);
                hash = hash * 31 + ComputeQuaternionSignature(marker.transform.rotation);
                hash = hash * 31 + ComputeVector3Signature(marker.transform.lossyScale);

                if (marker is AreaMarker areaMarker)
                {
                    hash = hash * 31 + (int)areaMarker.Shape;
                    hash = hash * 31 + ComputeVector3Signature(areaMarker.BoxSize);
                    hash = hash * 31 + QuantizeFloat(areaMarker.Height);

                    if (areaMarker.Vertices != null)
                    {
                        hash = hash * 31 + areaMarker.Vertices.Count;
                        foreach (var vertex in areaMarker.Vertices)
                            hash = hash * 31 + ComputeVector3Signature(vertex);
                    }
                }

                return hash;
            }
        }

        private static int ComputeRandomAreaPreviewSignature(
            string areaBindingId,
            int count,
            string distribution,
            float minSpacing,
            SceneMarker? marker)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (areaBindingId?.GetHashCode() ?? 0);
                hash = hash * 31 + count;
                hash = hash * 31 + (distribution?.GetHashCode() ?? 0);
                hash = hash * 31 + QuantizeFloat(minSpacing);
                hash = hash * 31 + ComputeSceneMarkerSignature(marker);
                return hash;
            }
        }

        private static void ReadRandomAreaSettings(
            ActionNodeData nodeData,
            out string areaBindingId,
            out int count,
            out string distribution,
            out float minSpacing)
        {
            areaBindingId = nodeData.Properties.Get<string>("area") ?? "";
            count = nodeData.Properties.Get<int>("count");
            if (count == 0) count = 5;

            distribution = nodeData.Properties.Get<string>("distribution") ?? "";
            if (string.IsNullOrEmpty(distribution)) distribution = "Poisson";

            minSpacing = nodeData.Properties.Get<float>("minSpacing");
            if (minSpacing == 0f) minSpacing = 2.0f;
        }

        /// <summary>
        /// 生成 Location.RandomArea 节点的位置预览
        /// </summary>
        private void GenerateLocationPreview(string blueprintId, string nodeId, ActionNodeData nodeData)
        {
            // 1. 读取节点配置
            var areaBindingId = nodeData.Properties.Get<string>("area");
            var count = nodeData.Properties.Get<int>("count");
            if (count == 0) count = 5;
            var distribution = nodeData.Properties.Get<string>("distribution");
            if (string.IsNullOrEmpty(distribution)) distribution = "Poisson";
            var minSpacing = nodeData.Properties.Get<float>("minSpacing");
            if (minSpacing == 0f) minSpacing = 2.0f;

            SBLog.Info(
                SBLogTags.Pipeline,
                "GenerateLocationPreview参数: node={0}, areaId='{1}', count={2}, distribution={3}, minSpacing={4}",
                nodeId,
                areaBindingId ?? "",
                count,
                distribution,
                minSpacing);

            if (string.IsNullOrEmpty(areaBindingId))
            {
                SBLog.Debug(SBLogTags.Pipeline, $"GenerateLocationPreview跳过: areaId为空, node={nodeId}");
                RemovePreview(nodeId);
                return;
            }

            // 2. 查询 AreaMarker
            var marker = FindMarkerById(areaBindingId);
            
            if (marker is not AreaMarker areaMarker)
            {
                SBLog.Warn(
                    SBLogTags.Pipeline,
                    "GenerateLocationPreview失败: 未找到AreaMarker, node={0}, areaId='{1}', foundType={2}, markerCacheCount={3}",
                    nodeId,
                    areaBindingId,
                    marker != null ? marker.GetType().Name : "(null)",
                    MarkerCache.Count);
                RemovePreview(nodeId);
                return;
            }

            // 3. 生成位置列表
            Vector3[] positions;
            try
            {
                positions = DistributionAlgorithms.Generate(
                    areaMarker, count, distribution, minSpacing);
            }
            catch (System.Exception ex)
            {
                SBLog.Warn(
                    SBLogTags.Pipeline,
                    "GenerateLocationPreview异常: node={0}, areaId='{1}', ex={2}",
                    nodeId,
                    areaBindingId,
                    ex.Message);
                UnityEngine.Debug.LogWarning($"[BlueprintPreview] 生成位置失败: {ex.Message}");
                RemovePreview(nodeId);
                return;
            }

            // 4. 缓存预览数据
            _allPreviews[nodeId] = new PreviewData
            {
                BlueprintId = blueprintId,
                NodeId = nodeId,
                SourceMarkerId = areaBindingId,
                Positions = positions,
                PreviewType = PreviewType.SpawnPositions,
                Timestamp = EditorApplication.timeSinceStartup
            };

            SBLog.Info(
                SBLogTags.Pipeline,
                "GenerateLocationPreview成功: node={0}, areaId='{1}', positionCount={2}",
                nodeId,
                areaBindingId,
                positions.Length);
        }


        /// <summary>
        /// 根据 MarkerId 查找 Marker
        /// </summary>
        private SceneMarker? FindMarkerById(string markerId)
        {
            var allMarkers = MarkerCache.GetAll();
            return allMarkers.FirstOrDefault(m => m.MarkerId == markerId);
        }
    }
}
