#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using NodeGraph.Core;
using NodeGraph.Unity;
using SceneBlueprint.Core;
using SceneBlueprint.Contract;
using SceneBlueprint.Editor.Drawers;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor
{
    /// <summary>
    /// Action 节点的 Inspector 面板绘制器。
    /// 根据 ActionDefinition 的 PropertyDefinition[] 自动生成 EditorGUILayout 控件。
    /// 支持条件可见性（VisibleWhen）、多种属性类型、范围约束等。
    ///
    /// 与 ActionContentRenderer 的职责区分：
    /// - ActionContentRenderer → 画布摘要（4行关键属性文本）
    /// - ActionNodeInspectorDrawer → Inspector 完整编辑（所有属性、可交互控件）
    /// </summary>
    public class ActionNodeInspectorDrawer : INodeInspectorDrawer
    {
        private Graph? _currentGraph;
        private IActionRegistry? _actionRegistry;
        private BindingContext? _bindingContext;
        private ITagDimensionRegistry? _tagDimensionRegistry;
        private string[]? _signalTags;
        private VariableDeclaration[] _variables = System.Array.Empty<VariableDeclaration>();
        private string[] _variableDisplayNames = new[] { "(未选择)" };
        private int[] _variableIndices = new[] { -1 };
        private readonly List<IActionInspectorSection> _inspectorSections;
        private readonly List<IActionInspectorOverride> _inspectorOverrides;
        private readonly Dictionary<string, bool> _advancedSectionFoldouts = new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>属性修改回调（nodeId, nodeData）</summary>
        public System.Action<string, ActionNodeData>? OnPropertyChanged;

        public ActionNodeInspectorDrawer(ActionRegistry actionRegistry)
        {
            _actionRegistry = actionRegistry;
            _inspectorSections = DiscoverInspectorSections();
            _inspectorOverrides = DiscoverInspectorOverrides();
        }

        /// <summary>设置场景绑定上下文（由编辑器窗口管理生命周期）</summary>
        public void SetBindingContext(BindingContext? context)
        {
            _bindingContext = context;
        }

        /// <summary>设置 Blackboard 变量声明列表（由编辑器窗口在资产变更时更新）</summary>
        public void SetVariableDeclarations(VariableDeclaration[]? variables)
        {
            _variables = variables ?? System.Array.Empty<VariableDeclaration>();
            // 构建缓存：下拉选项文本 + 对应 index 值
            if (_variables.Length == 0)
            {
                _variableDisplayNames = new[] { "(无变量，请先在变量面板添加)" };
                _variableIndices = new[] { -1 };
            }
            else
            {
                _variableDisplayNames = new string[_variables.Length + 1];
                _variableIndices      = new int[_variables.Length + 1];
                _variableDisplayNames[0] = "(未选择)";
                _variableIndices[0]      = -1;
                for (int i = 0; i < _variables.Length; i++)
                {
                    var v = _variables[i];
                    _variableDisplayNames[i + 1] = $"[{v.Index}] {v.Name}  ({v.Type}, {v.Scope})";
                    _variableIndices[i + 1]      = v.Index;
                }
            }
        }

        /// <summary>设置 Tag 维度注册表（由编辑器窗口在初始化时注入）</summary>
        public void SetTagDimensionRegistry(ITagDimensionRegistry? registry)
        {
            _tagDimensionRegistry = registry;
        }

        /// <summary>设置可选的信号标签列表（由编辑器窗口从 codegen 产物收集后注入）</summary>
        public void SetSignalTags(string[]? signalTags)
        {
            _signalTags = signalTags;
        }

        /// <summary>设置当前 Graph 引用（用于子蓝图 Inspector 和关卡总览）</summary>
        public void SetGraph(Graph? graph)
        {
            _currentGraph = graph;
        }

        public bool CanInspect(Node node)
        {
            // Action 节点 或 子蓝图代表节点 都可以 Inspect
            return node.UserData is ActionNodeData
                || node.TypeId == SubGraphConstants.BoundaryNodeTypeId;
        }

        // ─────────────────────────────────────
        //  DataIn 端口连接来源显示
        // ─────────────────────────────────────

        /// <summary>
        /// 展示当前节点所有 DataIn 端口的连接状态。
        /// 已连线时显示 "来自 [source]"；未连线时不占任何空间。
        /// </summary>
        private void DrawDataInConnections(Node node, ActionDefinition def)
        {
            if (_currentGraph == null) return;

            bool hasAny = false;
            foreach (var portDef in def.Ports)
            {
                if (portDef.Kind != PortKind.Data || portDef.Direction != PortDirection.Input)
                    continue;

                string portDisplayName = string.IsNullOrEmpty(portDef.DisplayName) ? portDef.Id : portDef.DisplayName;
                string? sourceLabel = GetDataInSourceLabel(node, (SceneBlueprint.Core.PortDefinition)portDef);

                if (!hasAny)
                {
                    EditorGUILayout.Space(2);
                    hasAny = true;
                }

                if (sourceLabel != null)
                {
                    // 已连线：显示来源信息（蓝绿色）
                    var oldColor = GUI.color;
                    GUI.color = new Color(0.4f, 0.85f, 1f, 1f);
                    EditorGUILayout.LabelField(portDisplayName, $"← {sourceLabel}");
                    GUI.color = oldColor;
                }
                else
                {
                    // 未连线：显示提示（灰色）
                    var oldColor = GUI.color;
                    GUI.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                    EditorGUILayout.LabelField(portDisplayName, "(未连线)");
                    GUI.color = oldColor;
                }
            }

            if (hasAny)
                EditorGUILayout.Space(4);
        }

        /// <summary>
        /// 返回 DataIn 端口的连接来源标签，格式为「源端口显示名 · 源节点类型名」。
        /// 该 DataIn 端口未连线时返回 null。
        /// </summary>
        private string? GetDataInSourceLabel(Node node, SceneBlueprint.Core.PortDefinition portDef)
        {
            if (_currentGraph == null) return null;

            // 找到对应的 Node.Port（通过显示名和方向匹配）
            string portDisplayName = string.IsNullOrEmpty(portDef.DisplayName) ? portDef.Id : portDef.DisplayName;
            NodeGraph.Core.Port? nodePort = null;
            foreach (var p in node.Ports)
            {
                if (p.Direction == PortDirection.Input && p.Kind == PortKind.Data && p.Name == portDisplayName)
                {
                    nodePort = p;
                    break;
                }
            }
            if (nodePort == null) return null;

            // 查找连入该端口的边
            NodeGraph.Core.Edge? inEdge = null;
            foreach (var edge in _currentGraph.Edges)
            {
                if (edge.TargetPortId == nodePort.Id)
                {
                    inEdge = edge;
                    break;
                }
            }
            if (inEdge == null) return null;

            // 找到源端口和源节点
            var sourcePort = _currentGraph.FindPort(inEdge.SourcePortId);
            if (sourcePort == null) return null;
            var sourceNode = _currentGraph.FindNode(sourcePort.NodeId);
            if (sourceNode == null) return null;

            // 读取源节点类型显示名
            string sourceNodeDisplayName = sourceNode.TypeId;
            string sourcePortDisplayName = sourcePort.Name;

            if (sourceNode.UserData is ActionNodeData sourceData
                && _actionRegistry != null
                && _actionRegistry.TryGet(sourceData.ActionTypeId, out var sourceDef))
            {
                sourceNodeDisplayName = sourceDef.DisplayName;

                // 在源 ActionDef.Ports 中反查语义 ID 对应的显示名
                foreach (var sp in sourceDef.Ports)
                {
                    string spDisplayName = string.IsNullOrEmpty(sp.DisplayName) ? sp.Id : sp.DisplayName;
                    if (spDisplayName == sourcePort.Name && sp.Direction == PortDirection.Output && sp.Kind == PortKind.Data)
                    {
                        sourcePortDisplayName = sp.DisplayName ?? sp.Id;
                        break;
                    }
                }
            }

            return $"{sourcePortDisplayName} · {sourceNodeDisplayName}";
        }

        public string GetTitle(Node node)
        {
            // 子蓝图代表节点：显示所属子蓝图的标题
            if (node.TypeId == SubGraphConstants.BoundaryNodeTypeId)
            {
                return "\U0001F4E6 子蓝图";
            }

            var data = node.UserData as ActionNodeData;
            if (data == null) return node.TypeId;

            if (_actionRegistry.TryGet(data.ActionTypeId, out var def))
                return def.DisplayName;

            return data.ActionTypeId;
        }

        public bool DrawInspector(Node node)
        {
            // 子蓝图代表节点：绘制子蓝图摘要
            if (node.TypeId == SubGraphConstants.BoundaryNodeTypeId)
            {
                return DrawSubGraphInspector(node);
            }

            var data = node.UserData as ActionNodeData;
            if (data == null)
            {
                EditorGUILayout.HelpBox("节点数据为空", MessageType.Warning);
                return false;
            }

            if (!_actionRegistry.TryGet(data.ActionTypeId, out var def))
            {
                EditorGUILayout.HelpBox($"未知类型: {data.ActionTypeId}", MessageType.Error);
                return false;
            }

            bool changed = false;
            IActionInspectorOverride? inspectorOverride = null;
            ActionInspectorOverrideContext? overrideContext = null;
            if (_actionRegistry != null)
            {
                overrideContext = new ActionInspectorOverrideContext(
                    node,
                    _currentGraph,
                    def,
                    data,
                    _actionRegistry,
                    _bindingContext,
                    _variables);
                inspectorOverride = ResolveInspectorOverride(overrideContext.Value);
            }

            // ── 节点信息头 ──
            EditorGUILayout.LabelField("类型", def.DisplayName);
            if (!string.IsNullOrEmpty(def.Category))
                EditorGUILayout.LabelField("分类", def.Category);
            EditorGUILayout.Space(4);

            // ── DataIn 端口连接信息（如果存在 DataIn 端口）──
            DrawDataInConnections(node, def);

            var connectedPortSemanticIds = ResolveConnectedPortSemanticIds(node);
            if (DrawDefinitionDrivenPropertyLayout(
                    node,
                    def,
                    data,
                    inspectorOverride,
                    connectedPortSemanticIds))
            {
                changed = true;
            }

            if (_actionRegistry != null)
            {
                var sectionContext = new ActionInspectorSectionContext(
                    node,
                    _currentGraph,
                    def,
                    data,
                    _actionRegistry,
                    _bindingContext,
                    ResolveSuppressDefinitionMetadataSection(inspectorOverride, overrideContext),
                    ResolveSuppressDefinitionValidationSection(inspectorOverride, overrideContext),
                    ResolveSuppressDefaultCompilationSection(inspectorOverride, overrideContext),
                    _variables);
                if (DrawInspectorSections(sectionContext))
                {
                    changed = true;
                    OnPropertyChanged?.Invoke(node.Id, data);
                }
            }

            return changed;
        }

        private bool DrawInspectorSections(ActionInspectorSectionContext context)
        {
            if (_inspectorSections.Count == 0)
            {
                return false;
            }

            var changed = false;
            for (var index = 0; index < _inspectorSections.Count; index++)
            {
                var section = _inspectorSections[index];
                try
                {
                    if (!section.Supports(context))
                    {
                        continue;
                    }

                    if (section.Draw(context))
                    {
                        changed = true;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[ActionNodeInspectorDrawer] Inspector section 执行失败: {section.GetType().FullName} - {ex.Message}");
                }
            }

            return changed;
        }

        private static bool ResolveSuppressDefaultCompilationSection(
            IActionInspectorOverride? inspectorOverride,
            ActionInspectorOverrideContext? overrideContext)
        {
            if (inspectorOverride == null)
            {
                return false;
            }

            if (overrideContext.HasValue
                && inspectorOverride is IActionInspectorOverrideMetadataResolver metadataResolver)
            {
                return metadataResolver.SuppressDefaultCompilationSection(overrideContext.Value);
            }

            return inspectorOverride is IActionInspectorOverrideMetadata metadata
                && metadata.SuppressDefaultCompilationSection;
        }

        private static bool ResolveSuppressDefinitionMetadataSection(
            IActionInspectorOverride? inspectorOverride,
            ActionInspectorOverrideContext? overrideContext)
        {
            if (inspectorOverride == null)
            {
                return false;
            }

            if (overrideContext.HasValue
                && inspectorOverride is IActionInspectorOverrideMetadataResolver metadataResolver)
            {
                return metadataResolver.SuppressDefinitionMetadataSection(overrideContext.Value);
            }

            return inspectorOverride is IActionInspectorOverrideMetadata metadata
                && metadata.SuppressDefinitionMetadataSection;
        }

        private static bool ResolveSuppressDefinitionValidationSection(
            IActionInspectorOverride? inspectorOverride,
            ActionInspectorOverrideContext? overrideContext)
        {
            if (inspectorOverride == null)
            {
                return false;
            }

            if (overrideContext.HasValue
                && inspectorOverride is IActionInspectorOverrideMetadataResolver metadataResolver)
            {
                return metadataResolver.SuppressDefinitionValidationSection(overrideContext.Value);
            }

            return inspectorOverride is IActionInspectorOverrideMetadata metadata
                && metadata.SuppressDefinitionValidationSection;
        }

        private static List<IActionInspectorSection> DiscoverInspectorSections()
        {
            var sections = new List<(IActionInspectorSection instance, int order)>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IActionInspectorSection>())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                try
                {
                    var instance = (IActionInspectorSection)Activator.CreateInstance(type)!;
                    var attr = (ActionInspectorSectionAttribute?)Attribute.GetCustomAttribute(
                        type,
                        typeof(ActionInspectorSectionAttribute));
                    sections.Add((instance, attr?.Order ?? 0));
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[ActionNodeInspectorDrawer] Inspector section 实例化失败: {type.FullName} - {ex.Message}");
                }
            }

            sections.Sort(static (left, right) =>
            {
                var orderCompare = left.order.CompareTo(right.order);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                return string.CompareOrdinal(
                    left.instance.GetType().FullName,
                    right.instance.GetType().FullName);
            });

            var result = new List<IActionInspectorSection>(sections.Count);
            for (var index = 0; index < sections.Count; index++)
            {
                result.Add(sections[index].instance);
            }

            return result;
        }

        private static List<IActionInspectorOverride> DiscoverInspectorOverrides()
        {
            var overrides = new List<(IActionInspectorOverride instance, int order)>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IActionInspectorOverride>())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                try
                {
                    var instance = (IActionInspectorOverride)Activator.CreateInstance(type)!;
                    var attr = (ActionInspectorOverrideAttribute?)Attribute.GetCustomAttribute(
                        type,
                        typeof(ActionInspectorOverrideAttribute));
                    overrides.Add((instance, attr?.Order ?? 0));
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[ActionNodeInspectorDrawer] Inspector override 实例化失败: {type.FullName} - {ex.Message}");
                }
            }

            overrides.Sort(static (left, right) =>
            {
                var orderCompare = left.order.CompareTo(right.order);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                return string.CompareOrdinal(
                    left.instance.GetType().FullName,
                    right.instance.GetType().FullName);
            });

            var result = new List<IActionInspectorOverride>(overrides.Count);
            for (var index = 0; index < overrides.Count; index++)
            {
                result.Add(overrides[index].instance);
            }

            return result;
        }

        private IActionInspectorOverride? ResolveInspectorOverride(ActionInspectorOverrideContext context)
        {
            for (var index = 0; index < _inspectorOverrides.Count; index++)
            {
                var inspectorOverride = _inspectorOverrides[index];
                try
                {
                    if (inspectorOverride.Supports(context))
                    {
                        return inspectorOverride;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[ActionNodeInspectorDrawer] Inspector override Supports 失败: {inspectorOverride.GetType().FullName} - {ex.Message}");
                }
            }

            return null;
        }

        private IReadOnlyCollection<string> ResolveConnectedPortSemanticIds(Node node)
        {
            if (_currentGraph == null)
            {
                return Array.Empty<string>();
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in _currentGraph.Edges)
            {
                var sourcePort = _currentGraph.FindPort(edge.SourcePortId);
                var targetPort = _currentGraph.FindPort(edge.TargetPortId);
                if (sourcePort != null && string.Equals(sourcePort.NodeId, node.Id, StringComparison.Ordinal))
                {
                    result.Add(sourcePort.SemanticId);
                }

                if (targetPort != null && string.Equals(targetPort.NodeId, node.Id, StringComparison.Ordinal))
                {
                    result.Add(targetPort.SemanticId);
                }
            }

            return result;
        }

        private bool DrawDefinitionDrivenPropertyLayout(
            Node ownerNode,
            ActionDefinition definition,
            ActionNodeData data,
            IActionInspectorOverride? inspectorOverride,
            IReadOnlyCollection<string> connectedPortSemanticIds)
        {
            var sections = ActionDefinitionSectionLayoutBuilder.BuildVisibleSections(definition, data.Properties);
            if (sections.Count == 0)
            {
                return false;
            }

            var changed = false;
            for (var index = 0; index < sections.Count; index++)
            {
                var section = sections[index];
                if (ShouldDrawSectionHeader(section, sections.Count))
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField(section.Title, EditorStyles.boldLabel);
                }

                if (DrawPropertyEntries(
                        section.NormalProperties,
                        data.Properties,
                        ownerNode.Id,
                        inspectorOverride,
                        ownerNode,
                        definition,
                        data,
                        connectedPortSemanticIds))
                {
                    changed = true;
                }

                if (section.AdvancedProperties.Count > 0)
                {
                    var foldoutKey = $"{ownerNode.Id}:{section.Key}:advanced";
                    _advancedSectionFoldouts.TryGetValue(foldoutKey, out var expanded);
                    var nextExpanded = EditorGUILayout.Foldout(expanded, "高级 Advanced", true);
                    _advancedSectionFoldouts[foldoutKey] = nextExpanded;
                    if (nextExpanded)
                    {
                        EditorGUI.indentLevel++;
                        if (DrawPropertyEntries(
                                section.AdvancedProperties,
                                data.Properties,
                                ownerNode.Id,
                                inspectorOverride,
                                ownerNode,
                                definition,
                                data,
                                connectedPortSemanticIds))
                        {
                            changed = true;
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            return changed;
        }

        private bool DrawPropertyEntries(
            List<PropertyDefinition> properties,
            PropertyBag bag,
            string ownerNodeId,
            IActionInspectorOverride? inspectorOverride,
            Node ownerNode,
            ActionDefinition definition,
            ActionNodeData data,
            IReadOnlyCollection<string> connectedPortSemanticIds)
        {
            var changed = false;
            for (var index = 0; index < properties.Count; index++)
            {
                var prop = properties[index];
                if (!DrawPropertyField(prop, bag, ownerNodeId, inspectorOverride, ownerNode, definition, data))
                {
                    continue;
                }

                ActionDefinitionAuthoringSupport.TryApplyPropertyNormalization(
                    ownerNodeId,
                    definition,
                    prop,
                    bag,
                    connectedPortSemanticIds,
                    _variables);
                changed = true;
                OnPropertyChanged?.Invoke(ownerNodeId, data);
            }

            return changed;
        }

        private static bool ShouldDrawSectionHeader(ActionDefinitionPropertySectionLayout section, int totalSectionCount)
        {
            return totalSectionCount > 1 || !section.IsImplicitDefault;
        }

        // ── 属性控件绘制（使用 EditorGUILayout 自动布局）──

        private bool DrawPropertyField(
            PropertyDefinition prop,
            PropertyBag bag,
            string ownerNodeId,
            IActionInspectorOverride? inspectorOverride,
            Node ownerNode,
            ActionDefinition definition,
            ActionNodeData data)
        {
            if (_actionRegistry != null && inspectorOverride != null)
            {
                var overrideContext = new ActionInspectorPropertyContext(
                    new ActionInspectorOverrideContext(
                        ownerNode,
                        _currentGraph,
                        definition,
                        data,
                        _actionRegistry,
                        _bindingContext,
                        _variables),
                    prop,
                    bag,
                    ownerNodeId);
                if (inspectorOverride.TryDrawProperty(overrideContext, out var overrideChanged))
                {
                    return overrideChanged;
                }
            }

            bool changed = false;

            switch (prop.Type)
            {
                case PropertyType.Float:
                    changed = DrawFloatField(prop, bag);
                    break;

                case PropertyType.Int:
                    changed = DrawIntField(prop, bag);
                    break;

                case PropertyType.Bool:
                    changed = DrawBoolField(prop, bag);
                    break;

                case PropertyType.String:
                    changed = DrawStringField(prop, bag);
                    break;

                case PropertyType.Enum:
                    changed = DrawEnumField(prop, bag);
                    break;

                case PropertyType.AssetRef:
                    changed = DrawStringField(prop, bag);
                    break;

                case PropertyType.SceneBinding:
                    changed = DrawSceneBindingField(prop, bag, ownerNodeId);
                    break;

                case PropertyType.Tag:
                    changed = DrawTagField(prop, bag);
                    break;

                case PropertyType.StructList:
                    changed = DrawStructListField(prop, bag, ownerNodeId);
                    break;

                case PropertyType.VariableSelector:
                    changed = DrawVariableSelectorField(prop, bag);
                    break;

                case PropertyType.SignalTagSelector:
                    changed = DrawSignalTagSelectorField(prop, bag);
                    break;

                case PropertyType.EntityRefSelector:
                    changed = DrawEntityRefSelectorField(prop, bag);
                    break;

                case PropertyType.ConditionParams:
                    changed = DrawConditionParamsField(prop, bag);
                    break;

                default:
                    EditorGUILayout.LabelField(prop.DisplayName, $"(不支持的类型 {prop.Type})");
                    break;
            }

            return changed;
        }

        private bool DrawFloatField(PropertyDefinition prop, PropertyBag bag)
        {
            float current = bag.Get<float>(prop.Key);
            float result;
            var label = BuildLabel(prop);

            if (prop.Min.HasValue && prop.Max.HasValue)
                result = EditorGUILayout.Slider(label, current, prop.Min.Value, prop.Max.Value);
            else
                result = EditorGUILayout.FloatField(label, current);

            if (!result.Equals(current))
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawIntField(PropertyDefinition prop, PropertyBag bag)
        {
            int current = bag.Get<int>(prop.Key);
            int result;
            var label = BuildLabel(prop);

            if (prop.Min.HasValue && prop.Max.HasValue)
                result = EditorGUILayout.IntSlider(label, current,
                    (int)prop.Min.Value, (int)prop.Max.Value);
            else
                result = EditorGUILayout.IntField(label, current);

            if (result != current)
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawVariableSelectorField(PropertyDefinition prop, PropertyBag bag)
        {
            int currentIndex = bag.Get<int>(prop.Key);

            // 在 _variableIndices 数组中找到当前值对应的下拉位置
            int popupPos = 0;
            for (int i = 0; i < _variableIndices.Length; i++)
            {
                if (_variableIndices[i] == currentIndex)
                {
                    popupPos = i;
                    break;
                }
            }

            int newPopupPos = EditorGUILayout.Popup(BuildLabel(prop), popupPos, _variableDisplayNames);

            if (newPopupPos != popupPos)
            {
                int newIndex = _variableIndices[newPopupPos];
                bag.Set(prop.Key, newIndex);
                return true;
            }
            return false;
        }

        private bool DrawBoolField(PropertyDefinition prop, PropertyBag bag)
        {
            bool current = bag.Get<bool>(prop.Key);
            bool result = EditorGUILayout.Toggle(BuildLabel(prop), current);

            if (result != current)
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawTagField(PropertyDefinition prop, PropertyBag bag)
        {
            string current = bag.Get<string>(prop.Key) ?? "";
            string result = TagSelectorDrawer.Draw(prop.DisplayName, current, _tagDimensionRegistry);

            if (!string.Equals(result, current, StringComparison.Ordinal))
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawSignalTagSelectorField(PropertyDefinition prop, PropertyBag bag)
        {
            string current = bag.Get<string>(prop.Key) ?? "";
            string result = TagSelectorDrawer.DrawSignalTagSelector(prop.DisplayName, current, _signalTags);

            if (!string.Equals(result, current, StringComparison.Ordinal))
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawEntityRefSelectorField(PropertyDefinition prop, PropertyBag bag)
        {
            string current = bag.Get<string>(prop.Key) ?? "";
            var sceneCandidates = BuildSceneEntityRefCandidates();
            string result = EntityRefDrawer.Draw(prop.DisplayName, current, sceneCandidates);

            if (!string.Equals(result, current, StringComparison.Ordinal))
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private IReadOnlyList<EntityRefSceneCandidate> BuildSceneEntityRefCandidates()
        {
            return EntityRefSceneCandidateProvider.BuildCandidates(_currentGraph);
        }

        private bool DrawConditionParamsField(PropertyDefinition prop, PropertyBag bag)
        {
            string current = bag.Get<string>(prop.Key) ?? "";
            string result = ConditionParamsDrawer.Draw(prop.DisplayName, current);

            if (!string.Equals(result, current, StringComparison.Ordinal))
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        private bool DrawStringField(PropertyDefinition prop, PropertyBag bag)
        {
            // 若设置了 TypeSourceKey，根据关联变量的类型动态切换控件
            if (!string.IsNullOrEmpty(prop.TypeSourceKey))
                return DrawTypedStringField(prop, bag);

            string current = bag.Get<string>(prop.Key) ?? "";
            string result = EditorGUILayout.TextField(BuildLabel(prop), current);

            if (result != current)
            {
                bag.Set(prop.Key, result);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 根据 TypeSourceKey 指向的 VariableSelector 属性所选变量的类型，
        /// 动态切换本字段的 UI 控件（IntField / FloatField / Toggle / TextField）。
        /// 值始终以字符串形式存入 PropertyBag。
        /// </summary>
        private bool DrawTypedStringField(PropertyDefinition prop, PropertyBag bag)
        {
            // 查找关联变量的类型字符串
            string varType = "String";
            int varIndex = bag.Get<int>(prop.TypeSourceKey!);
            if (varIndex >= 0 && _variables != null)
            {
                foreach (var v in _variables)
                {
                    if (v.Index == varIndex)
                    {
                        varType = v.Type ?? "String";
                        break;
                    }
                }
            }

            string current = bag.Get<string>(prop.Key) ?? "";
            string newValue;

            switch (varType)
            {
                case "Int":
                {
                    int parsed = int.TryParse(current, out var n) ? n : 0;
                    int edited = EditorGUILayout.IntField(BuildLabel(prop), parsed);
                    newValue = edited.ToString();
                    break;
                }
                case "Float":
                {
                    float parsed = float.TryParse(current,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var f) ? f : 0f;
                    float edited = EditorGUILayout.FloatField(BuildLabel(prop), parsed);
                    newValue = edited.ToString(CultureInfo.InvariantCulture);
                    break;
                }
                case "Bool":
                {
                    bool parsed = current.Equals("true", StringComparison.OrdinalIgnoreCase)
                                  || current == "1";
                    bool edited = EditorGUILayout.Toggle(BuildLabel(prop), parsed);
                    newValue = edited ? "true" : "false";
                    break;
                }
                default:
                    newValue = EditorGUILayout.TextField(BuildLabel(prop), current);
                    break;
            }

            if (newValue != current)
            {
                bag.Set(prop.Key, newValue);
                return true;
            }
            return false;
        }

        private bool DrawSceneBindingField(PropertyDefinition prop, PropertyBag bag, string ownerNodeId)
        {
            string bindingTypeStr = prop.SceneBindingType?.ToString() ?? "Unknown";
            string scopedBindingKey = BindingScopeUtility.BuildScopedKey(ownerNodeId, prop.Key);
            var req = prop.BindingRequirement;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 标题行：显示名称 + 绑定类型 + 约束标记
            var titleParts = $"\U0001F517 {prop.DisplayName} ({bindingTypeStr})";
            if (req != null)
            {
                if (req.Required)  titleParts += "  \u26A0\uFE0F required";
                if (req.Exclusive) titleParts += "  \U0001F512 exclusive";
            }
            EditorGUILayout.LabelField(titleParts, EditorStyles.miniLabel);

            if (_bindingContext != null)
            {
                // 从 BindingContext 读取当前绑定
                var current = _bindingContext.Get(scopedBindingKey);
                var currentMarker = current != null ? current.GetComponent<SceneMarker>() : null;
                string currentMarkerId = currentMarker != null ? currentMarker.MarkerId : "";
                string displayText = currentMarker != null
                    ? currentMarker.GetDisplayLabel()
                    : "(未绑定)";

                // 自定义绑定控件：[显示名称] [选择按钮🔍] [清除按钮×]
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("场景标记");

                // 显示当前绑定的标记名（可点击聚焦到场景对象）
                var labelStyle = currentMarker != null ? EditorStyles.label : EditorStyles.miniLabel;
                if (GUILayout.Button(displayText, labelStyle))
                {
                    if (currentMarker != null)
                    {
                        // 点击名称 → 在 Scene View 中选中并聚焦该 Marker
                        Selection.activeGameObject = current;
                        SceneView.FrameLastActiveSceneView();
                    }
                }

                // 选择按钮：弹出 SceneMarkerSelectorWindow
                if (GUILayout.Button("选择\u25B8", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    var filter = new SceneMarkerSelectorWindow.FilterConfig
                    {
                        MarkerTypeId = req?.MarkerTypeId,
                        RequiredAnnotations = req?.RequiredAnnotations,
                    };

                    // 捕获闭包变量
                    var capturedBag = bag;
                    var capturedPropKey = prop.Key;
                    var capturedNodeId = ownerNodeId;
                    var capturedScopedKey = scopedBindingKey;
                    var capturedContext = _bindingContext;
                    var capturedGraph = _currentGraph;

                    SceneMarkerSelectorWindow.Show(filter, currentMarkerId, selected =>
                    {
                        if (capturedContext == null) return;

                        if (selected != null)
                        {
                            capturedContext.Set(capturedScopedKey, selected.gameObject);
                            capturedBag.Set(capturedPropKey, selected.MarkerId);

                            SBLog.Info(SBLogTags.Binding,
                                "SceneBinding修改(Selector): node={0}, prop={1}, marker={2}, markerId='{3}'",
                                capturedNodeId, capturedPropKey,
                                selected.GetDisplayLabel(), selected.MarkerId);
                        }
                        else
                        {
                            capturedContext.Set(capturedScopedKey, null);
                            capturedBag.Set(capturedPropKey, "");

                            SBLog.Info(SBLogTags.Binding,
                                "SceneBinding清除(Selector): node={0}, prop={1}",
                                capturedNodeId, capturedPropKey);
                        }

                        // 通过 Graph 查找节点数据传递给回调
                        var nodeData = capturedGraph?.FindNode(capturedNodeId)?.UserData as ActionNodeData;
                        if (nodeData != null)
                            OnPropertyChanged?.Invoke(capturedNodeId, nodeData);
                    });
                }

                // 清除按钮
                using (new EditorGUI.DisabledScope(currentMarker == null))
                {
                    if (GUILayout.Button("\u2715", GUILayout.Width(20)))
                    {
                        _bindingContext.Set(scopedBindingKey, null);
                        bag.Set(prop.Key, "");

                        SBLog.Info(SBLogTags.Binding,
                            "SceneBinding清除: node={0}, prop={1}",
                            ownerNodeId, prop.Key);

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        return true;
                    }
                }

                EditorGUILayout.EndHorizontal();

                // 状态提示
                if (current == null)
                {
                    var msgType = (req != null && req.Required) ? MessageType.Error : MessageType.Warning;
                    var msgText = (req != null && req.Required) ? "必需绑定，未绑定场景对象" : "未绑定场景对象";
                    EditorGUILayout.HelpBox(msgText, msgType);
                }
                else
                {
                    DrawSceneBindingAuthoringSummary(
                        EntityRefSceneIdentityConventions.BuildSceneBindingAuthoringSummary(
                            currentMarkerId,
                            currentMarker?.GetDisplayLabel(),
                            currentMarker?.MarkerTypeId ?? bindingTypeStr));

                    // Exclusive 冲突检测
                    if (req is { Exclusive: true })
                    {
                        var conflictMsg = CheckExclusiveConflict(current, ownerNodeId, prop.Key);
                        if (conflictMsg != null)
                        {
                            EditorGUILayout.HelpBox(conflictMsg, MessageType.Warning);
                        }
                    }
                }
            }
            else
            {
                // 无 BindingContext 时降级为只读显示 MarkerId
                string storedId = bag.Get<string>(prop.Key) ?? "";
                if (string.IsNullOrEmpty(storedId))
                {
                    EditorGUILayout.LabelField("场景对象", "(未绑定)");
                }
                else
                {
                    // 尝试通过 MarkerId 查找标记名称用于显示
                    var marker = SceneMarkerSelectionBridge.FindMarkerInScene(storedId);
                    string displayText = marker != null ? marker.GetDisplayLabel() : $"(ID: {storedId[..System.Math.Min(8, storedId.Length)]})";
                    EditorGUILayout.LabelField("场景对象", displayText);

                    DrawSceneBindingAuthoringSummary(
                        EntityRefSceneIdentityConventions.BuildSceneBindingAuthoringSummary(
                            storedId,
                            marker?.GetDisplayLabel(),
                            marker?.MarkerTypeId ?? bindingTypeStr));
                }
                EditorGUILayout.HelpBox("请先保存蓝图以启用场景绑定", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            return false;
        }

        private static void DrawSceneBindingAuthoringSummary(EntityRefAuthoringSummary summary)
        {
            if (!summary.HasSummary && !summary.HasRuntimeIdentity && !summary.HasHelpText)
            {
                return;
            }

            EditorGUILayout.Space(2);
            if (summary.HasSummary)
            {
                EditorGUILayout.LabelField("", $"{summary.ModeLabel}：{summary.SummaryText}", EditorStyles.miniLabel);
            }

            if (summary.HasRuntimeIdentity)
            {
                EditorGUILayout.LabelField("", $"稳定身份：{summary.RuntimeIdentityText}", EditorStyles.miniLabel);
            }

            if (summary.HasHelpText)
            {
                EditorGUILayout.HelpBox(summary.HelpText, MessageType.Info);
            }
        }

        /// <summary>检测 Exclusive 冲突：是否有其他节点绑定了同一个 Marker</summary>
        private string? CheckExclusiveConflict(GameObject boundObj, string ownerNodeId, string propKey)
        {
            if (_currentGraph == null || _bindingContext == null) return null;

            var marker = boundObj.GetComponent<SceneMarker>();
            if (marker == null || string.IsNullOrEmpty(marker.MarkerId)) return null;

            foreach (var kvp in _bindingContext.All)
            {
                if (kvp.Value == null || kvp.Value != boundObj) continue;

                // 跳过自身
                var otherNodeId = BindingScopeUtility.ExtractNodeId(kvp.Key);
                var otherBindingKey = BindingScopeUtility.ExtractRawBindingKey(kvp.Key);
                if (otherNodeId == ownerNodeId && otherBindingKey == propKey) continue;

                // 同一个 GameObject 被其他节点绑定了
                return $"Exclusive 冲突：Marker \"{boundObj.name}\" 已被节点 {otherNodeId} 绑定";
            }

            return null;
        }

        // ── 子蓝图 Inspector ──

        private bool DrawSubGraphInspector(Node repNode)
        {
            if (_currentGraph == null) return false;

            // 找到此代表节点所属的 SubGraphFrame
            var sgf = _currentGraph.FindContainerSubGraphFrame(repNode.Id);
            if (sgf == null)
            {
                EditorGUILayout.HelpBox("未找到子蓝图框", MessageType.Warning);
                return false;
            }

            // ── 子蓝图信息 ──
            EditorGUILayout.LabelField("名称", sgf.Title);
            EditorGUILayout.LabelField("内部节点", sgf.ContainedNodeIds.Count.ToString());

            // 统计内部连线数
            int internalEdgeCount = 0;
            var containedSet = new HashSet<string>(sgf.ContainedNodeIds);
            foreach (var edge in _currentGraph.Edges)
            {
                var sp = _currentGraph.FindPort(edge.SourcePortId);
                var tp = _currentGraph.FindPort(edge.TargetPortId);
                if (sp != null && tp != null && containedSet.Contains(sp.NodeId) && containedSet.Contains(tp.NodeId))
                    internalEdgeCount++;
            }
            EditorGUILayout.LabelField("内部连线", internalEdgeCount.ToString());

            // ── 边界端口 ──
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("── 边界端口 ──", EditorStyles.boldLabel);

            foreach (var port in repNode.Ports)
            {
                string arrow = port.Direction == NodeGraph.Core.PortDirection.Input ? "● " : "";
                string suffix = port.Direction == NodeGraph.Core.PortDirection.Output ? " ●" : "";
                EditorGUILayout.LabelField($"  {arrow}{port.Name}{suffix}",
                    $"({port.Direction}, {port.Kind})");
            }

            // ── 场景绑定汇总 ──
            var bindings = CollectSubGraphBindings(sgf);
            if (bindings.Count > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("── 场景绑定汇总 ──", EditorStyles.boldLabel);

                bool changed = false;
                foreach (var (prop, nodeData, nodeId) in bindings)
                {
                    if (DrawSceneBindingField(prop, nodeData.Properties, nodeId))
                        changed = true;
                }
                return changed;
            }

            return false;
        }

        /// <summary>收集子蓝图内所有 SceneBinding 属性</summary>
        private List<(PropertyDefinition prop, ActionNodeData data, string nodeId)> CollectSubGraphBindings(SubGraphFrame sgf)
        {
            var result = new List<(PropertyDefinition, ActionNodeData, string)>();
            if (_currentGraph == null) return result;

            foreach (var nodeId in sgf.ContainedNodeIds)
            {
                var node = _currentGraph.FindNode(nodeId);
                if (node?.UserData is not ActionNodeData data) continue;
                if (!_actionRegistry.TryGet(data.ActionTypeId, out var def)) continue;

                var properties = def.FindSceneBindingProperties();
                for (var index = 0; index < properties.Length; index++)
                {
                    result.Add((properties[index], data, node.Id));
                }
            }
            return result;
        }

        // ── 关卡总览（无选中时）──

        public bool DrawBlueprintInspector(Graph graph)
        {
            _currentGraph = graph;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("\U0001F5FA\uFE0F 关卡总览", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── 蓝图信息 ──
            int subGraphCount = graph.SubGraphFrames.Count;
            int totalNodes = graph.Nodes.Count;
            int totalEdges = graph.Edges.Count;

            EditorGUILayout.LabelField("子蓝图", subGraphCount.ToString());
            EditorGUILayout.LabelField("总节点", totalNodes.ToString());
            EditorGUILayout.LabelField("总连线", totalEdges.ToString());

            // ── 子蓝图列表 ──
            if (subGraphCount > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("── 子蓝图列表 ──", EditorStyles.boldLabel);

                foreach (var sgf in graph.SubGraphFrames)
                {
                    var bindings = CollectSubGraphBindings(sgf);
                    int bound = 0;
                    int total = bindings.Count;

                    if (_bindingContext != null)
                    {
                        foreach (var (prop, _, nodeId) in bindings)
                        {
                            string scopedBindingKey = BindingScopeUtility.BuildScopedKey(nodeId, prop.Key);
                            if (_bindingContext.Get(scopedBindingKey) != null) bound++;
                        }
                    }

                    string status = total == 0 ? ""
                        : bound == total ? $"  绑定: {bound}/{total} \u2705"
                        : $"  绑定: {bound}/{total} \u26A0\uFE0F";

                    string icon = sgf.IsCollapsed ? "\U0001F4E6" : "\U0001F4C2";
                    EditorGUILayout.LabelField($"  {icon} {sgf.Title}{status}");
                }
            }

            // ── 全部未绑定项 ──
            if (_bindingContext != null)
            {
                var allUnbound = new List<(string subGraphTitle, PropertyDefinition prop, string nodeId)>();

                foreach (var sgf in graph.SubGraphFrames)
                {
                    var bindings = CollectSubGraphBindings(sgf);
                    foreach (var (prop, _, nodeId) in bindings)
                    {
                        string scopedBindingKey = BindingScopeUtility.BuildScopedKey(nodeId, prop.Key);
                        if (_bindingContext.Get(scopedBindingKey) == null)
                            allUnbound.Add((sgf.Title, prop, nodeId));
                    }
                }

                if (allUnbound.Count > 0)
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("── 未绑定项 ──", EditorStyles.boldLabel);

                    foreach (var (title, prop, nodeId) in allUnbound)
                    {
                        string bindingType = prop.SceneBindingType?.ToString() ?? "?";
                        string scopedBindingKey = BindingScopeUtility.BuildScopedKey(nodeId, prop.Key);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"⚠️ {title} / {prop.DisplayName} ({bindingType})",
                            EditorStyles.miniLabel);

                        var current = _bindingContext.Get(scopedBindingKey);
                        var result = (GameObject?)EditorGUILayout.ObjectField(
                            "", current, typeof(GameObject), true);
                        if (result != current)
                        {
                            _bindingContext.Set(scopedBindingKey, result);
                        }
                        EditorGUILayout.EndVertical();
                    }

                    return true; // 有可编辑内容
                }
            }

            return true; // 显示了总览信息
        }

        // ── 属性控件 ──

        private bool DrawEnumField(PropertyDefinition prop, PropertyBag bag)
        {
            if (prop.EnumOptions == null || prop.EnumOptions.Length == 0)
            {
                EditorGUILayout.LabelField(prop.DisplayName, "(无枚举选项)");
                return false;
            }

            string current = bag.Get<string>(prop.Key) ?? prop.EnumOptions[0];
            int selectedIndex = System.Array.IndexOf(prop.EnumOptions, current);
            if (selectedIndex < 0) selectedIndex = 0;
            var displayOptions = BuildEnumDisplayOptions(prop.EnumOptions, prop.EnumDisplayOptions);

            int newIndex = EditorGUILayout.Popup(BuildLabel(prop), selectedIndex, displayOptions);
            if (newIndex != selectedIndex && newIndex >= 0 && newIndex < prop.EnumOptions.Length)
            {
                bag.Set(prop.Key, prop.EnumOptions[newIndex]);
                return true;
            }
            return false;
        }

        // ── StructList 绘制 ──

        /// <summary>
        /// 绘制结构化列表属性（StructList）。
        /// 使用 EditorGUILayout 绘制可增删的列表，每个元素根据 StructFields 定义绘制子字段。
        /// 数据以 JSON 字符串形式存储在 PropertyBag 中。
        /// </summary>
        private bool DrawStructListField(PropertyDefinition prop, PropertyBag bag, string ownerNodeId)
        {
            if (prop.StructFields == null || prop.StructFields.Length == 0)
            {
                EditorGUILayout.LabelField(prop.DisplayName, "(无子字段定义)");
                return false;
            }

            // 从 PropertyBag 读取 JSON 字符串，解析为列表
            string json = bag.Get<string>(prop.Key)
                ?? PropertyDefinitionValueUtility.CreateDefaultBagValue(prop)?.ToString()
                ?? "[]";
            var items = StructListJsonHelper.Deserialize(json, prop.StructFields);

            bool changed = false;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"── {prop.DisplayName} ({items.Count} 项) ──", EditorStyles.boldLabel);

            // 绘制每个列表元素
            int removeIndex = -1;
            int moveUpIndex = -1;
            int moveDownIndex = -1;

            for (int i = 0; i < items.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 标题行：序号 + 操作按钮
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"#{i + 1}", EditorStyles.miniLabel, GUILayout.Width(24));
                GUILayout.FlexibleSpace();

                // 上移按钮
                EditorGUI.BeginDisabledGroup(i == 0);
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)))
                    moveUpIndex = i;
                EditorGUI.EndDisabledGroup();

                // 下移按钮
                EditorGUI.BeginDisabledGroup(i == items.Count - 1);
                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)))
                    moveDownIndex = i;
                EditorGUI.EndDisabledGroup();

                // 删除按钮
                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();

                // 绘制子字段
                var item = items[i];
                foreach (var field in prop.StructFields)
                {
                    if (DrawStructItemField(field, item, ownerNodeId, prop.Key, parentStructKey: null))
                        changed = true;
                }

                EditorGUILayout.EndVertical();
            }

            // 添加按钮
            if (GUILayout.Button($"+ 添加{prop.DisplayName.TrimEnd('配', '置', '列', '表')}", GUILayout.Height(22)))
            {
                var newItem = StructListJsonHelper.CreateDefaultItem(prop.StructFields);
                items.Add(newItem);
                changed = true;
            }

            // 处理排序和删除操作
            if (removeIndex >= 0 && removeIndex < items.Count)
            {
                items.RemoveAt(removeIndex);
                changed = true;
            }
            if (moveUpIndex > 0 && moveUpIndex < items.Count)
            {
                var temp = items[moveUpIndex];
                items[moveUpIndex] = items[moveUpIndex - 1];
                items[moveUpIndex - 1] = temp;
                changed = true;
            }
            if (moveDownIndex >= 0 && moveDownIndex < items.Count - 1)
            {
                var temp = items[moveDownIndex];
                items[moveDownIndex] = items[moveDownIndex + 1];
                items[moveDownIndex + 1] = temp;
                changed = true;
            }

            // 写回 PropertyBag
            if (changed)
            {
                string newJson = StructListJsonHelper.Serialize(items, prop.StructFields);
                bag.Set(prop.Key, newJson);
            }

            return changed;
        }

        /// <summary>绘制 StructList 中单个元素的单个子字段</summary>
        private bool DrawStructItemField(
            PropertyDefinition field,
            Dictionary<string, object> item,
            string ownerNodeId,
            string rootPropertyKey,
            string? parentStructKey)
        {
            bool changed = false;

            switch (field.Type)
            {
                case PropertyType.Int:
                {
                    int current = item.TryGetValue(field.Key, out var v)
                        ? System.Convert.ToInt32(v)
                        : System.Convert.ToInt32(PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field));
                    int result;
                    if (field.Min.HasValue && field.Max.HasValue)
                        result = EditorGUILayout.IntSlider(BuildLabel(field), current,
                            (int)field.Min.Value, (int)field.Max.Value);
                    else
                        result = EditorGUILayout.IntField(BuildLabel(field), current);
                    if (result != current) { item[field.Key] = result; changed = true; }
                    break;
                }
                case PropertyType.Float:
                {
                    float current = item.TryGetValue(field.Key, out var v)
                        ? System.Convert.ToSingle(v)
                        : System.Convert.ToSingle(PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field));
                    float result;
                    if (field.Min.HasValue && field.Max.HasValue)
                        result = EditorGUILayout.Slider(BuildLabel(field), current,
                            field.Min.Value, field.Max.Value);
                    else
                        result = EditorGUILayout.FloatField(BuildLabel(field), current);
                    if (!result.Equals(current)) { item[field.Key] = result; changed = true; }
                    break;
                }
                case PropertyType.Bool:
                {
                    bool current = item.TryGetValue(field.Key, out var v)
                        ? System.Convert.ToBoolean(v)
                        : System.Convert.ToBoolean(PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field));
                    bool result = EditorGUILayout.Toggle(BuildLabel(field), current);
                    if (result != current) { item[field.Key] = result; changed = true; }
                    break;
                }
                case PropertyType.String:
                {
                    string current = item.TryGetValue(field.Key, out var v)
                        ? v?.ToString() ?? ""
                        : PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field)?.ToString() ?? "";
                    string result = EditorGUILayout.TextField(BuildLabel(field), current);
                    if (result != current) { item[field.Key] = result; changed = true; }
                    break;
                }
                case PropertyType.Enum:
                {
                    if (field.EnumOptions != null && field.EnumOptions.Length > 0)
                    {
                        string current = item.TryGetValue(field.Key, out var v)
                            ? v?.ToString() ?? field.EnumOptions[0]
                            : PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field)?.ToString() ?? field.EnumOptions[0];
                        int selectedIndex = System.Array.IndexOf(field.EnumOptions, current);
                        if (selectedIndex < 0) selectedIndex = 0;
                        int newIndex = EditorGUILayout.Popup(
                            BuildLabel(field),
                            selectedIndex,
                            BuildEnumDisplayOptions(field.EnumOptions, field.EnumDisplayOptions));
                        if (newIndex != selectedIndex && newIndex >= 0 && newIndex < field.EnumOptions.Length)
                        {
                            item[field.Key] = field.EnumOptions[newIndex];
                            changed = true;
                        }
                    }
                    break;
                }
                case PropertyType.EntityRefSelector:
                {
                    string current = item.TryGetValue(field.Key, out var v)
                        ? v?.ToString() ?? ""
                        : PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field)?.ToString() ?? "";
                    string result = EntityRefDrawer.Draw(field.DisplayName, current, BuildSceneEntityRefCandidates());
                    if (!string.Equals(result, current, StringComparison.Ordinal))
                    {
                        item[field.Key] = result;
                        changed = true;
                    }
                    break;
                }
                case PropertyType.StructList:
                {
                    if (field.StructFields == null || field.StructFields.Length == 0)
                    {
                        EditorGUILayout.LabelField(field.DisplayName, "(无子字段定义)");
                        break;
                    }

                    if (!item.TryGetValue(field.Key, out var value)
                        || value is not List<Dictionary<string, object>> nestedItems)
                    {
                        nestedItems = PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field)
                            as List<Dictionary<string, object>>
                            ?? new List<Dictionary<string, object>>();
                        item[field.Key] = nestedItems;
                    }

                    if (DrawNestedStructListField(field, nestedItems, ownerNodeId, rootPropertyKey))
                    {
                        changed = true;
                    }

                    break;
                }
                default:
                {
                    string current = item.TryGetValue(field.Key, out var v)
                        ? v?.ToString() ?? ""
                        : PropertyDefinitionValueUtility.CreateDefaultStructFieldValue(field)?.ToString() ?? "";
                    string result = EditorGUILayout.TextField(BuildLabel(field), current);
                    if (result != current) { item[field.Key] = result; changed = true; }
                    break;
                }
            }

            return changed;
        }

        private bool DrawNestedStructListField(
            PropertyDefinition field,
            List<Dictionary<string, object>> items,
            string ownerNodeId,
            string rootPropertyKey)
        {
            bool changed = false;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"─ {field.DisplayName} ({items.Count} 项) ─", EditorStyles.boldLabel);

            int removeIndex = -1;
            int moveUpIndex = -1;
            int moveDownIndex = -1;

            for (int i = 0; i < items.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"#{i + 1}", EditorStyles.miniLabel, GUILayout.Width(24));
                GUILayout.FlexibleSpace();

                EditorGUI.BeginDisabledGroup(i == 0);
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24)))
                    moveUpIndex = i;
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(i == items.Count - 1);
                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24)))
                    moveDownIndex = i;
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();

                var item = items[i];
                foreach (var nestedField in field.StructFields!)
                {
                    if (DrawStructItemField(nestedField, item, ownerNodeId, rootPropertyKey, field.Key))
                        changed = true;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button($"+ 添加{field.DisplayName.TrimEnd('配', '置', '列', '表')}", GUILayout.Height(20)))
            {
                items.Add(StructListJsonHelper.CreateDefaultItem(field.StructFields!));
                changed = true;
            }

            if (removeIndex >= 0 && removeIndex < items.Count)
            {
                items.RemoveAt(removeIndex);
                changed = true;
            }

            if (moveUpIndex > 0 && moveUpIndex < items.Count)
            {
                var temp = items[moveUpIndex];
                items[moveUpIndex] = items[moveUpIndex - 1];
                items[moveUpIndex - 1] = temp;
                changed = true;
            }

            if (moveDownIndex >= 0 && moveDownIndex < items.Count - 1)
            {
                var temp = items[moveDownIndex];
                items[moveDownIndex] = items[moveDownIndex + 1];
                items[moveDownIndex + 1] = temp;
                changed = true;
            }

            return changed;
        }

        private static GUIContent BuildLabel(PropertyDefinition prop)
        {
            return new GUIContent(prop.DisplayName, prop.Tooltip ?? string.Empty);
        }

        private static string[] BuildEnumDisplayOptions(string[] options, string[]? displayOptions = null)
        {
            if (displayOptions != null && displayOptions.Length == options.Length)
            {
                return displayOptions;
            }

            var resolvedDisplayOptions = new string[options.Length];
            for (var index = 0; index < options.Length; index++)
            {
                resolvedDisplayOptions[index] = ToBilingualEnumLabel(options[index]);
            }

            return resolvedDisplayOptions;
        }

        private static string ToBilingualEnumLabel(string value)
        {
            return value switch
            {
                "All" => "全部 All",
                "Normal" => "普通 Normal",
                "Elite" => "精英 Elite",
                "Boss" => "首领 Boss",
                "Minion" => "小兵 Minion",
                "Special" => "特殊 Special",
                "Alive" => "存活 Alive",
                "Static" => "静态摆件 Static",
                "Triggered" => "休眠(等待激活) Triggered",
                "Idle" => "待机 Idle",
                "Patrol" => "巡逻 Patrol",
                "Guard" => "守卫 Guard",
                "Ambush" => "埋伏 Ambush",
                "Inherit" => "继承区域设置 Inherit",
                "InArea" => "区域内随机 InArea",
                "OnMoving" => "行进间刷怪 OnMoving",
                "None" => "不指定 None",
                _ => value
            };
        }

    }
}
