#nullable enable
using System.Collections.Generic;
using System.Text;
using NodeGraph.Core;
using SceneBlueprint.Core;
using SceneBlueprint.Contract.Knowledge;
using SceneBlueprint.Runtime.Knowledge;
using SceneBlueprint.Editor.Analysis;
using SceneBlueprint.Editor.Session;
using SceneBlueprint.Editor.WindowServices;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Knowledge.ChatPanel
{
    /// <summary>
    /// 蓝图查询工具集——供 LLM Function Calling 调用的 6 个核心工具。
    /// 通过 KnowledgeService 获取蓝图上下文和知识库数据。
    /// </summary>
    public static class BlueprintTools
    {
        /// <summary>
        /// 将所有蓝图查询工具注册到 ToolExecutor。
        /// </summary>
        public static void RegisterAll(ToolExecutor executor)
        {
            executor.Register(Def_GetBlueprintContext(), HandleGetBlueprintContext);
            executor.Register(Def_GetNodeDetail(), HandleGetNodeDetail);
            executor.Register(Def_GetMarkers(), HandleGetMarkers);
            executor.Register(Def_ValidateBlueprint(), HandleValidateBlueprint);
            executor.Register(Def_GetActionTypes(), HandleGetActionTypes);
            executor.Register(Def_GetNodeConnections(), HandleGetNodeConnections);
        }

        // ══════════════════════════════════════
        //  1. get_blueprint_context — 获取当前蓝图概览
        // ══════════════════════════════════════

        private static ToolDefinition Def_GetBlueprintContext()
        {
            return new ToolDefinition(
                "get_blueprint_context",
                "获取当前打开的蓝图概览信息，包括蓝图名称、节点数量、节点列表、选中节点、校验问题等"
            );
        }

        private static string HandleGetBlueprintContext(string argsJson)
        {
            var svc = KnowledgeService.Instance;
            if (!svc.ContextProvider.HasActiveSession)
                return "{\"error\":\"当前没有活跃的蓝图编辑会话\"}";

            var ctx = svc.ContextProvider.GetCurrentContext();
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"blueprintName\":\"{Esc(ctx.BlueprintName)}\",");
            sb.Append($"\"nodeCount\":{ctx.NodeCount},");
            sb.Append($"\"nodeListSummary\":\"{Esc(ctx.NodeListSummary)}\",");
            sb.Append($"\"selectedNode\":\"{Esc(ctx.SelectedNodeDisplayName)}\",");
            sb.Append($"\"selectedNodeTypeId\":\"{Esc(ctx.SelectedNodeTypeId)}\",");
            sb.Append($"\"selectedNodeProperties\":\"{Esc(ctx.SelectedNodeProperties)}\",");
            sb.Append($"\"validationIssues\":\"{Esc(ctx.ValidationIssues)}\"");
            sb.Append("}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  2. get_node_detail — 获取指定节点类型的详细定义
        // ══════════════════════════════════════

        private static ToolDefinition Def_GetNodeDetail()
        {
            return new ToolDefinition(
                "get_node_detail",
                "获取指定 Action 节点类型的详细定义信息，包括属性、端口、场景需求、知识文档等",
                new List<ToolParameter>
                {
                    new ToolParameter("type_id", "string", "Action 的 TypeId，如 Combat.Spawn", required: true)
                }
            );
        }

        private static string HandleGetNodeDetail(string argsJson)
        {
            string? typeId = SimpleJsonExtract(argsJson, "type_id");
            if (string.IsNullOrEmpty(typeId))
                return "{\"error\":\"缺少参数 type_id\"}";

            // 从 Session 获取 ActionRegistry
            var session = KnowledgeService.Instance.ContextProvider.ActiveSession;
            if (session == null)
                return "{\"error\":\"当前没有活跃的蓝图编辑会话\"}";

            var registry = session.ActionRegistry;
            if (registry == null || !registry.TryGet(typeId!, out var def))
                return $"{{\"error\":\"未找到 TypeId '{Esc(typeId!)}' 的行动定义\"}}";

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"typeId\":\"{Esc(def.TypeId)}\",");
            sb.Append($"\"displayName\":\"{Esc(def.DisplayName)}\",");
            sb.Append($"\"category\":\"{Esc(def.Category)}\",");
            sb.Append($"\"description\":\"{Esc(def.Description)}\",");
            sb.Append($"\"duration\":\"{def.Duration}\",");

            // 属性列表
            sb.Append("\"properties\":[");
            for (int i = 0; i < def.Properties.Length; i++)
            {
                if (i > 0) sb.Append(",");
                var p = def.Properties[i];
                sb.Append($"{{\"key\":\"{Esc(p.Key)}\",\"displayName\":\"{Esc(p.DisplayName)}\",\"type\":\"{p.Type}\"");
                if (p.DefaultValue != null)
                    sb.Append($",\"defaultValue\":\"{Esc(p.DefaultValue.ToString()!)}\"");
                if (p.Min.HasValue)
                    sb.Append($",\"min\":{p.Min.Value}");
                if (p.Max.HasValue)
                    sb.Append($",\"max\":{p.Max.Value}");
                if (!string.IsNullOrEmpty(p.Tooltip))
                    sb.Append($",\"tooltip\":\"{Esc(p.Tooltip!)}\"");
                if (p.EnumOptions != null && p.EnumOptions.Length > 0)
                    sb.Append($",\"enumOptions\":\"{Esc(string.Join(",", p.EnumOptions))}\"");
                if (!string.IsNullOrEmpty(p.SectionKey))
                    sb.Append($",\"sectionKey\":\"{Esc(p.SectionKey)}\"");
                if (!string.IsNullOrEmpty(p.SectionTitle))
                    sb.Append($",\"sectionTitle\":\"{Esc(p.SectionTitle)}\"");
                if (p.SectionOrder != 0)
                    sb.Append($",\"sectionOrder\":{p.SectionOrder}");
                if (p.IsAdvanced)
                    sb.Append(",\"isAdvanced\":true");
                sb.Append("}");
            }
            sb.Append("],");

            // 端口列表
            sb.Append("\"ports\":[");
            for (int i = 0; i < def.Ports.Length; i++)
            {
                if (i > 0) sb.Append(",");
                var port = def.Ports[i];
                sb.Append($"{{\"id\":\"{Esc(port.Id)}\",\"displayName\":\"{Esc(port.DisplayName)}\",\"direction\":\"{port.Direction}\",\"kind\":\"{port.Kind}\"");
                if (port.GraphRole != PortGraphRole.None)
                    sb.Append($",\"graphRole\":\"{port.GraphRole}\"");
                if (!string.IsNullOrEmpty(port.SummaryLabel))
                    sb.Append($",\"summaryLabel\":\"{Esc(port.SummaryLabel)}\"");
                if (port.MinConnections > 0)
                    sb.Append($",\"minConnections\":{port.MinConnections}");
                sb.Append("}");
            }
            sb.Append("],");

            // 场景需求
            var declaredSceneBindings = SceneBindingDeclarationSupport.CollectDeclaredBindings(def, propertyValues: null, includeEmpty: true);
            sb.Append("\"sceneRequirements\":[");
            for (int i = 0; i < declaredSceneBindings.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var binding = declaredSceneBindings[i];
                var requirement = binding.Requirement;
                sb.Append($"{{\"bindingKey\":\"{Esc(binding.BindingKey)}\",\"displayName\":\"{Esc(SceneBindingDeclarationSupport.ResolveTitle(binding))}\"");
                sb.Append($",\"markerTypeId\":\"{Esc(SceneBindingDeclarationSupport.ResolveMarkerTypeId(binding))}\"");
                if (requirement != null)
                {
                    sb.Append($",\"required\":{BoolStr(requirement.Required)},\"exclusive\":{BoolStr(requirement.Exclusive)}");
                }
                sb.Append("}");
            }
            sb.Append("],");

            // 输出变量
            sb.Append("\"outputVariables\":[");
            var outputVariables = def.GetDeclaredOutputVariables();
            for (int i = 0; i < outputVariables.Length; i++)
            {
                if (i > 0) sb.Append(",");
                var ov = outputVariables[i];
                sb.Append($"{{\"name\":\"{Esc(ov.Name)}\",\"displayName\":\"{Esc(ov.DisplayName)}\",\"type\":\"{Esc(ov.Type)}\",\"scope\":\"{Esc(ov.Scope)}\"}}");
            }
            sb.Append("],");

            sb.Append("\"graphDeclarations\":[");
            var graphPorts = def.FindGraphDeclarationPorts();
            for (int i = 0; i < graphPorts.Length; i++)
            {
                if (i > 0) sb.Append(",");
                var port = graphPorts[i];
                sb.Append($"{{\"id\":\"{Esc(port.Id)}\",\"displayName\":\"{Esc(port.DisplayName)}\",\"graphRole\":\"{port.GraphRole}\"");
                if (!string.IsNullOrEmpty(port.SummaryLabel))
                    sb.Append($",\"summaryLabel\":\"{Esc(port.SummaryLabel)}\"");
                if (port.MinConnections > 0)
                    sb.Append($",\"minConnections\":{port.MinConnections}");
                sb.Append("}");
            }
            sb.Append("]");

            // 尝试获取知识文档
            var doc = KnowledgeService.Instance.Registry.FindActionDoc(typeId!);
            if (doc != null)
            {
                string? content = doc.ReadContent();
                if (!string.IsNullOrEmpty(content))
                {
                    if (content!.Length > 1500)
                        content = content.Substring(0, 1500) + "\n...(截断)";
                    sb.Append($",\"knowledgeDoc\":\"{Esc(content)}\"");
                }
            }

            sb.Append("}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  3. get_markers — 获取蓝图中使用的场景标记信息
        // ══════════════════════════════════════

        private static ToolDefinition Def_GetMarkers()
        {
            return new ToolDefinition(
                "get_markers",
                "获取当前蓝图中所有节点的场景标记（Marker）绑定信息和场景需求"
            );
        }

        private static string HandleGetMarkers(string argsJson)
        {
            var session = KnowledgeService.Instance.ContextProvider.ActiveSession;
            if (session == null)
                return "{\"error\":\"当前没有活跃的蓝图编辑会话\"}";

            var graph = session.ViewModel?.Graph;
            if (graph == null)
                return "{\"error\":\"无法获取蓝图图数据\"}";

            var sb = new StringBuilder();
            sb.Append("{\"markers\":[");

            int count = 0;
            foreach (var node in graph.Nodes)
            {
                if (node.UserData is not ActionNodeData actionData) continue;

                // 检查是否有 SceneBinding 属性
                if (actionData.Properties == null) continue;

                var registry = session.ActionRegistry;
                ActionDefinition? def = null;
                registry?.TryGet(actionData.ActionTypeId, out def);

                if (def == null)
                {
                    continue;
                }

                var declaredBindings = SceneBindingDeclarationSupport.CollectDeclaredBindings(def, actionData.Properties.All);
                for (var index = 0; index < declaredBindings.Count; index++)
                {
                    var binding = declaredBindings[index];
                    if (count > 0) sb.Append(",");
                    sb.Append($"{{\"nodeTypeId\":\"{Esc(actionData.ActionTypeId)}\",\"nodeId\":\"{node.Id}\",\"propertyKey\":\"{Esc(binding.BindingKey)}\",\"boundValue\":\"{Esc(binding.RawValue)}\"");
                    sb.Append($",\"displayName\":\"{Esc(SceneBindingDeclarationSupport.ResolveTitle(binding))}\"");
                    sb.Append($",\"markerTypeId\":\"{Esc(SceneBindingDeclarationSupport.ResolveMarkerTypeId(binding))}\"");
                    if (binding.Requirement != null)
                    {
                        var requirement = binding.Requirement;
                        sb.Append($",\"required\":{BoolStr(requirement.Required)},\"exclusive\":{BoolStr(requirement.Exclusive)}");
                    }

                    sb.Append("}");
                    count++;
                }
            }

            sb.Append("]");

            // 查询场景中实际存在的 Marker
            sb.Append(",\"sceneMarkers\":[");
            int markerCount = 0;
            var allMarkers = UnityEngine.Object.FindObjectsOfType<SceneMarker>();
            foreach (var marker in allMarkers)
            {
                if (markerCount > 0) sb.Append(",");
                sb.Append($"{{\"markerId\":\"{Esc(marker.MarkerId)}\",\"markerName\":\"{Esc(marker.GetDisplayLabel())}\",\"markerTypeId\":\"{Esc(marker.MarkerTypeId)}\",\"gameObject\":\"{Esc(marker.gameObject.name)}\"");
                var pos = marker.GetRepresentativePosition();
                sb.Append($",\"position\":\"({pos.x:F1},{pos.y:F1},{pos.z:F1})\"");
                if (!string.IsNullOrEmpty(marker.Tag))
                    sb.Append($",\"tag\":\"{Esc(marker.Tag)}\"");
                sb.Append("}");
                markerCount++;
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  4. validate_blueprint — 执行蓝图校验
        // ══════════════════════════════════════

        private static ToolDefinition Def_ValidateBlueprint()
        {
            return new ToolDefinition(
                "validate_blueprint",
                "对当前蓝图执行校验分析，返回所有诊断问题（错误、警告）"
            );
        }

        private static string HandleValidateBlueprint(string argsJson)
        {
            var session = KnowledgeService.Instance.ContextProvider.ActiveSession;
            if (session == null)
                return "{\"error\":\"当前没有活跃的蓝图编辑会话\"}";

            // 优先触发最新分析；若 AnalysisCtrl 不可用则读缓存
            AnalysisReport? report = null;
            try { report = session.AnalysisCtrl?.ForceRunNow(); }
            catch { /* 忽略异常，回退到缓存 */ }
            report ??= session.LastAnalysisReport;
            if (report == null)
                return "{\"diagnostics\":[],\"summary\":\"无校验报告（可能尚未执行分析）\"}";

            var sb = new StringBuilder();
            sb.Append("{\"diagnostics\":[");

            for (int i = 0; i < report.Diagnostics.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var d = report.Diagnostics[i];
                sb.Append($"{{\"severity\":\"{d.Severity}\",\"code\":\"{Esc(d.Code)}\",\"message\":\"{Esc(d.Message)}\"");
                if (!string.IsNullOrEmpty(d.NodeId))
                    sb.Append($",\"nodeId\":\"{Esc(d.NodeId)}\"");
                sb.Append("}");
            }

            sb.Append($"],\"totalErrors\":{report.ErrorCount},");
            sb.Append($"\"totalWarnings\":{report.WarningCount}}}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  5. get_action_types — 获取所有可用的行动类型
        // ══════════════════════════════════════

        private static ToolDefinition Def_GetActionTypes()
        {
            return new ToolDefinition(
                "get_action_types",
                "获取所有已注册的行动（Action）类型列表，按分类分组，包括 TypeId、显示名、描述",
                new List<ToolParameter>
                {
                    new ToolParameter("category", "string", "可选，按分类过滤，如 Combat、Flow、Presentation")
                }
            );
        }

        private static string HandleGetActionTypes(string argsJson)
        {
            var session = KnowledgeService.Instance.ContextProvider.ActiveSession;
            if (session == null)
                return "{\"error\":\"当前没有活跃的蓝图编辑会话\"}";

            var registry = session.ActionRegistry;
            if (registry == null)
                return "{\"error\":\"ActionRegistry 未初始化\"}";

            string? category = SimpleJsonExtract(argsJson, "category");

            IReadOnlyList<ActionDefinition> actions;
            if (!string.IsNullOrEmpty(category))
            {
                actions = registry.GetByCategory(category!);
            }
            else
            {
                actions = registry.GetAll();
            }

            var sb = new StringBuilder();
            sb.Append("{\"actions\":[");

            for (int i = 0; i < actions.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var a = actions[i];
                sb.Append($"{{\"typeId\":\"{Esc(a.TypeId)}\",\"displayName\":\"{Esc(a.DisplayName)}\",\"category\":\"{Esc(a.Category)}\",\"description\":\"{Esc(a.Description)}\",\"duration\":\"{a.Duration}\"}}");
            }

            sb.Append($"],\"totalCount\":{actions.Count}");

            // 如果未按分类过滤，附带分类列表
            if (string.IsNullOrEmpty(category))
            {
                var categories = registry.GetCategories();
                sb.Append(",\"categories\":[");
                for (int i = 0; i < categories.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append($"\"{Esc(categories[i])}\"");
                }
                sb.Append("]");
            }

            sb.Append("}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  6. get_node_connections — 获取节点连线关系
        // ══════════════════════════════════════

        private static ToolDefinition Def_GetNodeConnections()
        {
            return new ToolDefinition(
                "get_node_connections",
                "获取当前蓝图中指定节点（或全部节点）的连线关系，展示执行流和数据流",
                new List<ToolParameter>
                {
                    new ToolParameter("node_id", "string", "可选，节点 ID。不传则返回所有节点的连线概览")
                }
            );
        }

        private static string HandleGetNodeConnections(string argsJson)
        {
            var session = KnowledgeService.Instance.ContextProvider.ActiveSession;
            if (session == null)
                return "{\"error\":\"当前没有活跃的蓝图编辑会话\"}";

            var graph = session.ViewModel?.Graph;
            if (graph == null)
                return "{\"error\":\"无法获取蓝图图数据\"}";

            string? nodeId = SimpleJsonExtract(argsJson, "node_id");

            var sb = new StringBuilder();
            sb.Append("{\"connections\":[");

            int connIdx = 0;

            // 确定要查询的节点集合
            IEnumerable<Node> targetNodes;
            if (!string.IsNullOrEmpty(nodeId))
            {
                var node = graph.FindNode(nodeId!);
                if (node == null)
                    return $"{{\"error\":\"未找到节点 '{Esc(nodeId!)}'\"}}"; 
                targetNodes = new[] { node };
            }
            else
            {
                targetNodes = graph.Nodes;
            }

            foreach (var node in targetNodes)
            {
                var edges = graph.GetEdgesForNode(node.Id);
                foreach (var edge in edges)
                {
                    // 只从 source 侧记录，避免重复
                    var srcPort = graph.FindPort(edge.SourcePortId);
                    if (srcPort == null || srcPort.NodeId != node.Id) continue;

                    var tgtPort = graph.FindPort(edge.TargetPortId);
                    if (tgtPort == null) continue;

                    var srcNode = graph.FindNode(srcPort.NodeId);
                    var tgtNode = graph.FindNode(tgtPort.NodeId);

                    string srcTypeId = (srcNode?.UserData is ActionNodeData sd) ? sd.ActionTypeId : srcNode?.TypeId ?? "";
                    string tgtTypeId = (tgtNode?.UserData is ActionNodeData td) ? td.ActionTypeId : tgtNode?.TypeId ?? "";

                    if (connIdx > 0) sb.Append(",");
                    sb.Append($"{{\"srcNodeId\":\"{srcNode?.Id}\",\"srcTypeId\":\"{Esc(srcTypeId)}\",\"srcPort\":\"{Esc(srcPort.SemanticId)}\",");
                    sb.Append($"\"tgtNodeId\":\"{tgtNode?.Id}\",\"tgtTypeId\":\"{Esc(tgtTypeId)}\",\"tgtPort\":\"{Esc(tgtPort.SemanticId)}\",");
                    sb.Append($"\"portKind\":\"{srcPort.Kind}\"}}");
                    connIdx++;
                }
            }

            sb.Append($"],\"totalEdges\":{connIdx}}}");
            return sb.ToString();
        }

        // ══════════════════════════════════════
        //  工具方法
        // ══════════════════════════════════════

        /// <summary>简易 JSON 字符串值提取。</summary>
        private static string? SimpleJsonExtract(string json, string key)
        {
            string p1 = $"\"{key}\":\"";
            string p2 = $"\"{key}\": \"";
            int idx = json.IndexOf(p1, System.StringComparison.Ordinal);
            if (idx >= 0) idx += p1.Length;
            else
            {
                idx = json.IndexOf(p2, System.StringComparison.Ordinal);
                if (idx >= 0) idx += p2.Length;
            }
            if (idx < 0) return null;

            var sb = new StringBuilder();
            for (int i = idx; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length) { sb.Append(json[++i]); continue; }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string BoolStr(bool b) => b ? "true" : "false";
    }
}
