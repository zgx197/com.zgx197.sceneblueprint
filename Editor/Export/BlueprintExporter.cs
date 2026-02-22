#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor;
using SceneBlueprint.Editor.Templates;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;
using SceneBlueprint.Runtime.Templates;

namespace SceneBlueprint.Editor.Export
{
    /// <summary>
    /// 蓝图导出器。将编辑器中的 Graph 转换为运行时可消费的 SceneBlueprintData。
    ///
    /// 导出流程：
    /// 1. 展平子蓝图：跳过 __SubGraphBoundary 节点，合并穿过边界的连线
    /// 2. 遍历所有节点 → ActionEntry（属性扁平化为 PropertyValue[]）
    /// 3. 遍历所有连线 → TransitionEntry（含 ConditionData）
    /// 4. 合并场景绑定（从 Manager 或 BindingContext 读取）
    /// 5. 返回 SceneBlueprintData
    ///
    /// 注：图合法性校验（节点可达性、Required 端口等）已由 BlueprintAnalyzer 在导出前完成，
    /// Exporter 本身只负责"Graph → Contract"转换。
    /// </summary>
    public static class BlueprintExporter
    {
        /// <summary>
        /// 导出选项。
        /// </summary>
        public sealed class ExportOptions
        {
            /// <summary>空间适配器类型标识（C2 默认 Unity3D）。</summary>
            public string AdapterType = "Unity3D";
        }

        /// <summary>
        /// 场景绑定数据（由调用方从 Manager 或 BindingContext 提供）。
        /// </summary>
        public class SceneBindingData
        {
            public string BindingKey = "";
            public string BindingType = "";
            public string StableObjectId = "";
            public string AdapterType = "";
            public string SpatialPayloadJson = "";
            public string SourceSubGraph = "";
            public string SourceActionTypeId = "";
        }

        /// <summary>
        /// 将 Graph 导出为 SceneBlueprintData（兼容旧接口，无场景绑定）。
        /// </summary>
        public static ExportResult Export(
            Graph graph,
            ActionRegistry registry,
            string? blueprintId = null,
            string? blueprintName = null)
        {
            return Export(graph, registry, null, blueprintId, blueprintName, null);
        }

        /// <summary>
        /// 将 Graph 导出为 SceneBlueprintData，合并场景绑定数据。
        /// </summary>
        /// <param name="graph">编辑器中的蓝图图</param>
        /// <param name="registry">ActionRegistry</param>
        /// <param name="sceneBindings">场景绑定数据（来自 Manager），可为 null</param>
        /// <param name="blueprintId">蓝图 ID（可选）</param>
        /// <param name="blueprintName">蓝图名称（可选）</param>
        public static ExportResult Export(
            Graph graph,
            ActionRegistry registry,
            List<SceneBindingData>? sceneBindings,
            string? blueprintId = null,
            string? blueprintName = null,
            ExportOptions? options = null,
            VariableDeclaration[]? variables = null)
        {
            var messages = new List<ValidationMessage>();
            var exportOptions = options ?? new ExportOptions();

            // ── Step 0: 收集边界节点 ID（用于展平过滤）──
            var boundaryNodeIds = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                if (node.TypeId == SubGraphConstants.BoundaryNodeTypeId)
                    boundaryNodeIds.Add(node.Id);
            }

            // ── Step 1: 节点 → ActionEntry（跳过边界节点）──
            var actions = new List<ActionEntry>();
            foreach (var node in graph.Nodes)
            {
                if (boundaryNodeIds.Contains(node.Id)) continue;

                var entry = ExportNode(node, registry, messages, exportOptions);
                if (entry != null)
                    actions.Add(entry);
            }

            // ── Step 2: 展平连线（合并穿过边界节点的连线）──
            var transitions = ExportEdgesFlattened(graph, boundaryNodeIds, registry, messages);

            // ── Step 2.5: 计算 Flow.Join 节点的入边数 ──
            EnrichFlowJoinWithInEdgeCount(actions, transitions.ToList(), messages);

            // ── Step 3: 合并场景绑定（同时将 bindingKey 统一升级为 scoped key）──
            MergeSceneBindings(
                actions,
                sceneBindings ?? new List<SceneBindingData>(),
                messages,
                exportOptions,
                graph);

            // ── Step 3.5: 收集 Annotation 数据（后处理）──
            EnrichBindingsWithAnnotations(actions, registry, messages);

            // ── Step 4: 组装 ──
            var allVariables = MergeVariables(variables, graph, registry);
            var dataConnections = ExportDataConnections(graph, boundaryNodeIds, registry);
            var data = new SceneBlueprintData
            {
                BlueprintId = blueprintId ?? graph.Id,
                BlueprintName = blueprintName ?? "",
                Version = 2,
                ExportTime = DateTime.UtcNow.ToString("o"),
                Actions = actions.ToArray(),
                Transitions = transitions.ToArray(),
                Variables = allVariables,
                DataConnections = dataConnections
            };

            return new ExportResult(data, messages);
        }

        // ══════════════════════════════════════
        //  节点导出
        // ══════════════════════════════════════

        private static ActionEntry? ExportNode(
            Node node,
            ActionRegistry registry,
            List<ValidationMessage> messages,
            ExportOptions options)
        {
            var nodeData = node.UserData as ActionNodeData;
            if (nodeData == null)
            {
                messages.Add(ValidationMessage.Warning(
                    $"节点 '{node.Id}' 无 ActionNodeData，已跳过"));
                return null;
            }

            var entry = new ActionEntry
            {
                Id = node.Id,
                TypeId = nodeData.ActionTypeId
            };

            // 导出属性
            if (registry.TryGet(nodeData.ActionTypeId, out var def))
            {
                var properties = new List<PropertyValue>();
                var sceneBindings = new List<SceneBindingEntry>();

                foreach (var prop in def.Properties)
                {
                    if (prop.Type == PropertyType.SceneBinding)
                    {
                        // SceneBinding 提升为独立条目
                        var value = nodeData.Properties.Get<string>(prop.Key) ?? "";
                        if (!string.IsNullOrEmpty(value))
                        {
                            sceneBindings.Add(new SceneBindingEntry
                            {
                                BindingKey = prop.Key,
                                BindingType = prop.SceneBindingType?.ToString() ?? "Transform",
                                // 节点属性里的值通常是业务侧标识（如 MarkerId），
                                // C2 起同时作为稳定 ID 回退值。
                                SceneObjectId = value,
                                StableObjectId = value,
                                AdapterType = options.AdapterType,
                                SpatialPayloadJson = "{}"
                            });
                        }
                    }
                    else
                    {
                        var pv = ExportPropertyValue(prop, nodeData.Properties);
                        if (pv != null)
                            properties.Add(pv);
                    }
                }

                entry.Properties = properties.ToArray();
                entry.SceneBindings = sceneBindings.ToArray();

                var portDefaults = new List<PortDefaultValue>();
                foreach (var port in def.Ports)
                {
                    if (port.Kind == PortKind.Data &&
                        port.Direction == PortDirection.Input &&
                        port.DefaultValue != null)
                    {
                        portDefaults.Add(new PortDefaultValue
                        {
                            PortId       = port.Id,
                            DefaultValue = port.DefaultValue.ToString() ?? ""
                        });
                    }
                }
                entry.PortDefaults = portDefaults.ToArray();
            }
            else
            {
                messages.Add(ValidationMessage.Error(
                    $"节点 '{node.Id}' 的类型 '{nodeData.ActionTypeId}' 未在 ActionRegistry 中注册"));
            }

            return entry;
        }

        private static PropertyValue? ExportPropertyValue(
            PropertyDefinition prop, PropertyBag bag)
        {
            var raw = bag.GetRaw(prop.Key);
            if (raw == null) return null;

            string valueType = PropertyTypeToString(prop.Type);
            string value = SerializePropertyValue(prop.Type, raw);

            return new PropertyValue
            {
                Key = prop.Key,
                ValueType = valueType,
                Value = value
            };
        }

        private static string PropertyTypeToString(PropertyType type)
        {
            return type switch
            {
                PropertyType.Float => "float",
                PropertyType.Int => "int",
                PropertyType.Bool => "bool",
                PropertyType.String => "string",
                PropertyType.Enum => "enum",
                PropertyType.AssetRef => "assetRef",
                PropertyType.Vector2 => "vector2",
                PropertyType.Vector3 => "vector3",
                PropertyType.Color => "color",
                PropertyType.Tag => "tag",
                PropertyType.StructList => "json",
                PropertyType.VariableSelector => "int",
                _ => "string"
            };
        }

        /// <summary>
        /// 合并用户声明变量与图中节点的 OutputVariables。
        /// 节点产出变量使用 DJB2 合成 Index（10000–19999），与用户声明变量（0–9999）不冲突。
        /// </summary>
        private static VariableDeclaration[] MergeVariables(
            VariableDeclaration[]? userVars, Graph graph, ActionRegistry registry)
        {
            var result = new List<VariableDeclaration>(userVars ?? Array.Empty<VariableDeclaration>());
            var seen = new HashSet<string>(result.Select(v => v.Name));

            foreach (var node in graph.Nodes)
            {
                if (node.UserData is not ActionNodeData data) continue;
                if (!registry.TryGet(data.ActionTypeId, out var def)) continue;

                foreach (var outVar in def.OutputVariables)
                {
                    if (seen.Contains(outVar.Name)) continue;
                    seen.Add(outVar.Name);
                    result.Add(new VariableDeclaration
                    {
                        Index        = NodeOutputVarIndex(outVar.Name),
                        Name         = outVar.Name,
                        Type         = outVar.Type,
                        Scope        = outVar.Scope,
                        InitialValue = ""
                    });
                }
            }

            return result.ToArray();
        }

        /// <summary>DJB2 hash of name → 10000–19999（与编辑器端计算逻辑一致）。</summary>
        private static int NodeOutputVarIndex(string name)
        {
            uint h = 5381;
            foreach (char c in name) h = ((h << 5) + h) + c;
            return 10000 + (int)(h % 10000);
        }

        private static string SerializePropertyValue(PropertyType type, object value)
        {
            return type switch
            {
                PropertyType.Float => Convert.ToSingle(value).ToString("G", CultureInfo.InvariantCulture),
                PropertyType.Int => Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture),
                PropertyType.Bool => Convert.ToBoolean(value) ? "true" : "false",
                PropertyType.StructList => value.ToString() ?? "[]",
                PropertyType.VariableSelector => Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
        }

        // ══════════════════════════════════════
        //  连线导出（展平子蓝图）
        // ══════════════════════════════════════

        /// <summary>
        /// 展平导出所有连线。对于穿过边界节点的连线，合并为直接连接。
        ///
        /// 边界节点（RepresentativeNode）充当中继：
        /// - 外部 A → Rep.inPort (edge1)，Rep.inPort → 内部 B (edge2) → 合并为 A → B
        /// - 内部 X → Rep.outPort (edge3)，Rep.outPort → 外部 Y (edge4) → 合并为 X → Y
        /// - 不涉及边界节点的连线直接导出
        /// </summary>
        private static TransitionEntry[] ExportEdgesFlattened(
            Graph graph, HashSet<string> boundaryNodeIds, ActionRegistry registry, List<ValidationMessage> messages)
        {
            if (boundaryNodeIds.Count == 0)
            {
                // 无子蓝图，直接导出所有控制流连线（跳过 Data 边）
                var simple = new List<TransitionEntry>();
                foreach (var edge in graph.Edges)
                {
                    if (IsDataEdge(edge, graph)) continue;
                    var entry = ExportEdgeDirect(edge, graph, registry, messages);
                    if (entry != null) simple.Add(entry);
                }
                return simple.ToArray();
            }

            // 1. 建立边界端口的入边和出边索引（支持多对多）
            //    incomingToPort[portId] = 所有连入此边界端口的 source port 列表
            //    outgoingFromPort[portId] = 所有从此边界端口连出的 target port 列表
            var incomingToPort = new Dictionary<string, List<NodeGraph.Core.Port>>();
            var outgoingFromPort = new Dictionary<string, List<NodeGraph.Core.Port>>();

            foreach (var edge in graph.Edges)
            {
                var sp = graph.FindPort(edge.SourcePortId);
                var tp = graph.FindPort(edge.TargetPortId);
                if (sp == null || tp == null) continue;

                bool sourceIsBoundary = boundaryNodeIds.Contains(sp.NodeId);
                bool targetIsBoundary = boundaryNodeIds.Contains(tp.NodeId);

                if (!sourceIsBoundary && targetIsBoundary)
                {
                    if (!incomingToPort.TryGetValue(edge.TargetPortId, out var list))
                    {
                        list = new List<NodeGraph.Core.Port>();
                        incomingToPort[edge.TargetPortId] = list;
                    }
                    list.Add(sp);
                }
                else if (sourceIsBoundary && !targetIsBoundary)
                {
                    if (!outgoingFromPort.TryGetValue(edge.SourcePortId, out var list))
                    {
                        list = new List<NodeGraph.Core.Port>();
                        outgoingFromPort[edge.SourcePortId] = list;
                    }
                    list.Add(tp);
                }
            }

            // 2. 遍历所有连线，分类处理（跳过 Data 边）
            var transitions = new List<TransitionEntry>();

            foreach (var edge in graph.Edges)
            {
                if (IsDataEdge(edge, graph)) continue;
                var sp = graph.FindPort(edge.SourcePortId);
                var tp = graph.FindPort(edge.TargetPortId);
                if (sp == null || tp == null)
                {
                    messages.Add(ValidationMessage.Warning(
                        $"连线 '{edge.Id}' 的端口未找到，已跳过"));
                    continue;
                }

                bool sourceIsBoundary = boundaryNodeIds.Contains(sp.NodeId);
                bool targetIsBoundary = boundaryNodeIds.Contains(tp.NodeId);

                if (!sourceIsBoundary && !targetIsBoundary)
                {
                    // 两端都不是边界节点 → 直接导出
                    transitions.Add(new TransitionEntry
                    {
                        FromActionId = sp.NodeId,
                        FromPortId = sp.SemanticId,
                        ToActionId = tp.NodeId,
                        ToPortId = tp.SemanticId,
                        Condition = new ConditionData { Type = "Immediate" }
                    });
                }
                else if (sourceIsBoundary && !targetIsBoundary)
                {
                    // 边界.outPort → 非边界：查找谁连入了这个边界端口
                    if (incomingToPort.TryGetValue(edge.SourcePortId, out var sources))
                    {
                        foreach (var realSource in sources)
                        {
                            transitions.Add(new TransitionEntry
                            {
                                FromActionId = realSource.NodeId,
                                FromPortId = realSource.SemanticId,
                                ToActionId = tp.NodeId,
                                ToPortId = tp.SemanticId,
                                Condition = new ConditionData { Type = "Immediate" }
                            });
                        }
                    }
                }
                else if (!sourceIsBoundary && targetIsBoundary)
                {
                    // 非边界 → 边界.inPort：查找这个边界端口连向谁
                    if (outgoingFromPort.TryGetValue(edge.TargetPortId, out var targets))
                    {
                        foreach (var realTarget in targets)
                        {
                            transitions.Add(new TransitionEntry
                            {
                                FromActionId = sp.NodeId,
                                FromPortId = sp.SemanticId,
                                ToActionId = realTarget.NodeId,
                                ToPortId = realTarget.SemanticId,
                                Condition = new ConditionData { Type = "Immediate" }
                            });
                        }
                    }
                }
                // sourceIsBoundary && targetIsBoundary → 跳过
            }

            return transitions.ToArray();
        }

        /// <summary>导出图中所有 Data 边为 DataConnectionEntry。展平处理和控制流边类似，但不需要 Condition。</summary>
        private static DataConnectionEntry[] ExportDataConnections(
            Graph graph, HashSet<string> boundaryNodeIds, ActionRegistry registry)
        {
            var result = new List<DataConnectionEntry>();

            foreach (var edge in graph.Edges)
            {
                if (!IsDataEdge(edge, graph)) continue;

                var sp = graph.FindPort(edge.SourcePortId);
                var tp = graph.FindPort(edge.TargetPortId);
                if (sp == null || tp == null) continue;

                // 跳过涉及边界节点的 Data 边（展平阐题不属于本期范围）
                if (boundaryNodeIds.Contains(sp.NodeId) || boundaryNodeIds.Contains(tp.NodeId)) continue;

                result.Add(new DataConnectionEntry
                {
                    FromActionId = sp.NodeId,
                    FromPortId   = sp.SemanticId,
                    ToActionId   = tp.NodeId,
                    ToPortId     = tp.SemanticId,
                });
            }

            return result.ToArray();
        }

        /// <summary>判断一条边是否为 Data 边（依据源端口的 PortKind）。</summary>
        private static bool IsDataEdge(Edge edge, Graph graph)
        {
            var sp = graph.FindPort(edge.SourcePortId);
            return sp?.Kind == PortKind.Data;
        }

        private static TransitionEntry? ExportEdgeDirect(
            Edge edge, Graph graph, ActionRegistry registry, List<ValidationMessage> messages)
        {
            var sourcePort = graph.FindPort(edge.SourcePortId);
            var targetPort = graph.FindPort(edge.TargetPortId);

            if (sourcePort == null || targetPort == null)
            {
                messages.Add(ValidationMessage.Warning(
                    $"连线 '{edge.Id}' 的端口未找到，已跳过"));
                return null;
            }

            return new TransitionEntry
            {
                FromActionId = sourcePort.NodeId,
                FromPortId = sourcePort.SemanticId,
                ToActionId = targetPort.NodeId,
                ToPortId = targetPort.SemanticId,
                Condition = new ConditionData { Type = "Immediate" }
            };
        }

        // ══════════════════════════════════════
        //  场景绑定合并
        // ══════════════════════════════════════

        /// <summary>
        /// 将 Manager 中的场景绑定数据合并到对应 ActionEntry 的 SceneBindings 字段。
        /// </summary>
        private static void MergeSceneBindings(
            List<ActionEntry> actions,
            List<SceneBindingData> sceneBindings,
            List<ValidationMessage> messages,
            ExportOptions options,
            Graph graph)
        {
            // 建立 bindingKey → data 索引
            var bindingMap = new Dictionary<string, SceneBindingData>();
            foreach (var bd in sceneBindings)
            {
                if (!string.IsNullOrEmpty(bd.BindingKey))
                    bindingMap[bd.BindingKey] = bd;
            }

            // 更新每个 ActionEntry 中已存在的 SceneBindingEntry
            foreach (var action in actions)
            {
                if (action.SceneBindings.Length == 0) continue;

                foreach (var sb in action.SceneBindings)
                {
                    string rawBindingKey = sb.BindingKey;
                    string scopedBindingKey = BindingScopeUtility.BuildScopedKey(action.Id, rawBindingKey);

                    sb.BindingKey = scopedBindingKey;

                    if (bindingMap.TryGetValue(scopedBindingKey, out var data)
                        || bindingMap.TryGetValue(rawBindingKey, out data))
                    {
                        // 仅使用 V2 语义：StableObjectId 作为唯一绑定标识。
                        var stableId = data.StableObjectId;
                        if (string.IsNullOrEmpty(stableId))
                            stableId = sb.StableObjectId;

                        sb.StableObjectId = stableId;
                        sb.AdapterType = !string.IsNullOrEmpty(data.AdapterType)
                            ? data.AdapterType
                            : options.AdapterType;
                        sb.SpatialPayloadJson = !string.IsNullOrEmpty(data.SpatialPayloadJson)
                            ? data.SpatialPayloadJson
                            : "{}";

                        // 为兼容运行时消费端，SceneObjectId 同步写入稳定 ID。
                        sb.SceneObjectId = stableId;

                        sb.SourceSubGraph = data.SourceSubGraph;
                        sb.SourceActionTypeId = data.SourceActionTypeId;

                        if (string.IsNullOrEmpty(stableId))
                        {
                            messages.Add(ValidationMessage.Warning(
                                $"场景绑定 '{sb.BindingKey}' (Action: {action.Id}) 未配置场景对象"));
                        }
                    }
                    else
                    {
                        // 无 Manager 绑定时，沿用节点导出的 StableObjectId，并补齐字段。
                        sb.SceneObjectId = sb.StableObjectId;

                        if (string.IsNullOrEmpty(sb.AdapterType))
                            sb.AdapterType = options.AdapterType;

                        if (string.IsNullOrEmpty(sb.SpatialPayloadJson))
                            sb.SpatialPayloadJson = "{}";

                        if (string.IsNullOrEmpty(sb.StableObjectId))
                        {
                            messages.Add(ValidationMessage.Warning(
                                $"场景绑定 '{sb.BindingKey}' (Action: {action.Id}) 未配置场景对象"));
                        }
                    }
                }
            }
        }

        // ══════════════════════════════════════
        //  Annotation 数据收集（后处理）
        // ══════════════════════════════════════

        /// <summary>
        /// 遍历所有 ActionEntry 的 SceneBindings，通过 StableObjectId（MarkerId）
        /// 在场景中查找对应的 Marker，收集其上的 MarkerAnnotation 数据。
        /// <para>
        /// 特殊处理 AreaMarker 绑定：展开其子 PointMarker，为每个子点位生成
        /// 独立的 SceneBindingEntry（含 Annotation 数据）。
        /// </para>
        /// </summary>
        private static void EnrichBindingsWithAnnotations(
            List<ActionEntry> actions,
            ActionRegistry registry,
            List<ValidationMessage> messages)
        {
            foreach (var action in actions)
            {
                if (action.SceneBindings.Length == 0) continue;

                var expandedBindings = new List<SceneBindingEntry>();

                foreach (var sb in action.SceneBindings)
                {
                    var markerId = sb.StableObjectId;
                    if (string.IsNullOrEmpty(markerId))
                    {
                        expandedBindings.Add(sb);
                        continue;
                    }

                    var marker = AnnotationExportHelper.FindMarkerById(markerId);
                    if (marker == null)
                    {
                        expandedBindings.Add(sb);
                        continue;
                    }

                    // ── AreaMarker 处理 ──
                    if (marker is AreaMarker area)
                    {
                        // 根据 ActionTypeId 决定处理策略
                        if (IsAreaLevelAnnotationAction(action.TypeId))
                        {
                            // Spawn.Wave 等：保留 AreaMarker 整体，收集区域几何 + 自身 Annotation
                            sb.SpatialPayloadJson = AnnotationExportHelper.BuildAreaSpatialPayload(area);
                            sb.Annotations = AnnotationExportHelper.CollectAnnotationsFromMarker(
                                area, action.TypeId);
                            expandedBindings.Add(sb);

                            messages.Add(ValidationMessage.Info(
                                $"AreaMarker '{area.GetDisplayLabel()}' 作为区域整体导出 " +
                                $"(Action: {action.Id}, Annotations: {sb.Annotations.Length})"));
                        }
                        else
                        {
                            // Spawn.Preset 等：展开子 PointMarker
                            var childPoints = AnnotationExportHelper.CollectChildPointMarkers(area);
                            if (childPoints.Count == 0)
                            {
                                messages.Add(ValidationMessage.Warning(
                                    $"AreaMarker '{area.GetDisplayLabel()}' (ID: {area.MarkerId}) " +
                                    $"没有子 PointMarker (Action: {action.Id})"));
                                expandedBindings.Add(sb);
                            }
                            else
                            {
                                foreach (var pm in childPoints)
                                {
                                    var pmStableId = "marker:" + pm.MarkerId;
                                    var childSb = new SceneBindingEntry
                                    {
                                        BindingKey = sb.BindingKey,
                                        BindingType = "Transform",
                                        SceneObjectId = pmStableId,
                                        StableObjectId = pmStableId,
                                        AdapterType = sb.AdapterType,
                                        SpatialPayloadJson = AnnotationExportHelper.BuildPointSpatialPayload(pm),
                                        SourceSubGraph = sb.SourceSubGraph,
                                        SourceActionTypeId = sb.SourceActionTypeId,
                                        Annotations = AnnotationExportHelper.CollectAnnotations(
                                            pm, action.TypeId)
                                    };
                                    expandedBindings.Add(childSb);
                                }

                                messages.Add(ValidationMessage.Info(
                                    $"AreaMarker '{area.GetDisplayLabel()}' 展开为 {childPoints.Count} 个子点位 " +
                                    $"(Action: {action.Id})"));
                            }
                        }
                    }
                    // ── PointMarker：直接收集 Annotation ──
                    else if (marker is PointMarker pm)
                    {
                        sb.Annotations = AnnotationExportHelper.CollectAnnotations(
                            pm, action.TypeId);
                        expandedBindings.Add(sb);
                    }
                    else
                    {
                        // 其他 Marker 类型：原样保留
                        expandedBindings.Add(sb);
                    }
                }

                action.SceneBindings = expandedBindings.ToArray();
            }
        }


        /// <summary>
        /// 判断给定 ActionTypeId 是否需要将 AreaMarker 作为整体区域导出
        /// （收集 AreaMarker 自身 Annotation），而非展开子 PointMarker。
        /// </summary>
        private static bool IsAreaLevelAnnotationAction(string actionTypeId)
        {
            return actionTypeId switch
            {
                "Spawn.Wave" => true,
                _ => false
            };
        }

        /// <summary>
        /// 为 Flow.Join 节点计算入边数并添加到属性中。
        /// <para>
        /// Flow.Join 需要知道有多少条输入连线，才能判断何时所有输入都已完成。
        /// 这个数量在导出时确定，写入 "inEdgeCount" 属性。
        /// </para>
        /// </summary>
        private static void EnrichFlowJoinWithInEdgeCount(
            List<ActionEntry> actions,
            List<TransitionEntry> transitions,
            List<ValidationMessage> messages)
        {
            // 统计每个节点的入边数
            var inEdgeCounts = new Dictionary<string, int>();
            foreach (var transition in transitions)
            {
                if (!inEdgeCounts.ContainsKey(transition.ToActionId))
                    inEdgeCounts[transition.ToActionId] = 0;
                inEdgeCounts[transition.ToActionId]++;
            }

            // 为 Flow.Join 节点添加 inEdgeCount 属性
            foreach (var action in actions)
            {
                if (action.TypeId != "Flow.Join")
                    continue;

                int count = inEdgeCounts.TryGetValue(action.Id, out var c) ? c : 0;

                // 添加 inEdgeCount 属性
                var properties = action.Properties.ToList();
                properties.Add(new PropertyValue
                {
                    Key = "inEdgeCount",
                    ValueType = "int",
                    Value = count.ToString()
                });
                action.Properties = properties.ToArray();

                // 验证：至少需要 1 条入边
                if (count == 0)
                {
                    messages.Add(ValidationMessage.Warning(
                        $"Flow.Join 节点 '{action.Id}' 没有入边，将永远不会被激活"));
                }
            }
        }

    }

    // ══════════════════════════════════════
    //  导出结果
    // ══════════════════════════════════════

    /// <summary>导出结果，包含数据和验证消息</summary>
    public class ExportResult
    {
        public SceneBlueprintData Data { get; }
        public IReadOnlyList<ValidationMessage> Messages { get; }
        public bool HasErrors => Messages.Any(m => m.Level == ValidationLevel.Error);
        public bool HasWarnings => Messages.Any(m => m.Level == ValidationLevel.Warning);

        public ExportResult(SceneBlueprintData data, List<ValidationMessage> messages)
        {
            Data = data;
            Messages = messages;
        }
    }

    /// <summary>验证消息级别</summary>
    public enum ValidationLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>验证消息</summary>
    public class ValidationMessage
    {
        public ValidationLevel Level { get; }
        public string Message { get; }

        public ValidationMessage(ValidationLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        public static ValidationMessage Info(string msg) => new(ValidationLevel.Info, msg);
        public static ValidationMessage Warning(string msg) => new(ValidationLevel.Warning, msg);
        public static ValidationMessage Error(string msg) => new(ValidationLevel.Error, msg);

        public override string ToString() => $"[{Level}] {Message}";
    }
}
