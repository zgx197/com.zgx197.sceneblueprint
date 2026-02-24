#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.Markers.Pipeline.Interaction;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprint.Runtime.Markers.Annotations;

namespace SceneBlueprint.Editor.Markers.Pipeline
{
    /// <summary>
    /// Gizmo 绘制管线——统一调度所有标记的 Scene View 绘制。
    /// <para>
    /// 通过 <see cref="SceneView.duringSceneGui"/> 注册单一回调，
    /// 按 <see cref="DrawPhase"/> 顺序遍历所有可见标记，调用对应
    /// <see cref="IMarkerGizmoRenderer"/> 的 Phase 方法。
    /// </para>
    /// <para>
    /// 特性：
    /// - 严格的绘制顺序（Fill → Wireframe → Icon → Interactive → Highlight → Label → Pick）
    /// - 视锥裁剪（仅绘制摄像机可见的标记）
    /// - 标记缓存（通过 <see cref="MarkerCache"/>，不每帧 FindObjectsOfType）
    /// - 自动发现 Renderer（反射扫描所有 IMarkerGizmoRenderer 实现）
    /// - Interactive Phase 接管机制（选中时 Renderer 可替代 Fill/Wireframe）
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class GizmoRenderPipeline
    {
        /// <summary>
        /// SceneView 标记交互模式。
        /// Edit：不接管鼠标事件，完全让位给 Unity 原生变换工具。
        /// Pick：启用自定义拾取逻辑（点击 Gizmo 选中标记）。
        /// </summary>
        public enum MarkerInteractionMode
        {
            Edit = 0,
            Pick = 1
        }

        // ─── 渲染器注册表 ───
        private static readonly Dictionary<Type, IMarkerGizmoRenderer> _renderers = new();

        // ─── 每帧复用的列表（避免 GC）───
        private static readonly List<GizmoDrawContext> _drawList = new();

        // ─── 交互服务（M2 拆分）───
        private static readonly IMarkerHitTestService _hitTestService = new DefaultMarkerHitTestService();
        private static readonly IMarkerSelectionController _selectionController = new DefaultMarkerSelectionController();
        private static bool _selectionInputDrivenByTool;

        private static MarkerInteractionMode _interactionMode = MarkerInteractionMode.Edit;

        /// <summary>当前标记交互模式。</summary>
        public static MarkerInteractionMode InteractionMode => _interactionMode;

        /// <summary>
        /// 设置选中输入来源。
        /// false = 由 GizmoRenderPipeline(duringSceneGui) 处理；
        /// true  = 由 MarkerSelectTool(EditorTool) 处理。
        /// </summary>
        internal static void SetSelectionInputDrivenByTool(bool drivenByTool)
        {
            if (_selectionInputDrivenByTool == drivenByTool)
                return;

            _selectionInputDrivenByTool = drivenByTool;
            SBLog.Info(SBLogTags.Selection,
                $"Selection input route => {(drivenByTool ? "Tool" : "duringSceneGui")}, interactionMode={_interactionMode}, toolActive={MarkerSelectTool.IsActive}");
            Trace($"SetSelectionInputDrivenByTool route={(drivenByTool ? "Tool" : "duringSceneGui")}, mode={_interactionMode}, toolActive={MarkerSelectTool.IsActive}");
            _selectionController.ResetState();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 提供给 MarkerSelectTool 的选中输入入口。
        /// </summary>
        internal static void HandlePickingFromTool(Event evt)
        {
            if (!_selectionInputDrivenByTool)
                return;

            if (evt != null && IsTraceEvent(evt.type))
            {
                SBLog.Debug(SBLogTags.Selection,
                    $"HandlePickingFromTool evt={evt.type}, button={evt.button}, mods={evt.modifiers}, drawCount={_drawList.Count}, mode={_interactionMode}, toolActive={MarkerSelectTool.IsActive}");
                Trace($"HandlePickingFromTool evt={evt.type}, button={evt.button}, mods={evt.modifiers}, drawCount={_drawList.Count}, mode={_interactionMode}, toolActive={MarkerSelectTool.IsActive}");
            }

            if (_drawList.Count == 0)
            {
                SBLog.Debug(SBLogTags.Selection, "HandlePickingFromTool drawList=0 => reset state");
                Trace("HandlePickingFromTool drawList=0 => reset state");
                _selectionController.ResetState();
                return;
            }

            _selectionController.Handle(
                evt,
                _interactionMode,
                _hitTestService,
                _drawList,
                _renderers);
        }

        static GizmoRenderPipeline()
        {
            AutoDiscoverRenderers();
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        // ─── 注册 ───

        /// <summary>手动注册 Renderer（供第三方扩展或测试）</summary>
        public static void RegisterRenderer(IMarkerGizmoRenderer renderer)
        {
            _renderers[renderer.TargetType] = renderer;
        }

        /// <summary>获取已注册的 Renderer 数量（调试用）</summary>
        public static int RendererCount => _renderers.Count;

        /// <summary>
        /// 设置标记交互模式。
        /// </summary>
        public static void SetInteractionMode(MarkerInteractionMode mode)
        {
            if (_interactionMode == mode)
                return;

            _interactionMode = mode;
            _selectionController.ResetState();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 反射自动发现并注册所有已加载程序集中的 IMarkerGizmoRenderer 实现。
        /// <para>扫描范围：所有已加载程序集（含用户业务代码），跳过系统/Unity 内置程序集。</para>
        /// </summary>
        private static void AutoDiscoverRenderers()
        {
            _renderers.Clear();

            var interfaceType = typeof(IMarkerGizmoRenderer);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 跳过系统/Unity 内置程序集，避免无意义扫描
                var asmName = assembly.GetName().Name ?? "";
                if (asmName.StartsWith("System",          StringComparison.Ordinal) ||
                    asmName.StartsWith("mscorlib",         StringComparison.Ordinal) ||
                    asmName.StartsWith("Microsoft.",       StringComparison.Ordinal) ||
                    asmName.StartsWith("UnityEngine",      StringComparison.Ordinal) ||
                    asmName.StartsWith("UnityEditor",      StringComparison.Ordinal) ||
                    asmName.StartsWith("Unity.",           StringComparison.Ordinal) ||
                    asmName.StartsWith("com.unity.",       StringComparison.Ordinal) ||
                    asmName.StartsWith("nunit.",           StringComparison.Ordinal))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // 部分类型加载失败时，仍处理已成功加载的类型
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface) continue;
                    if (!interfaceType.IsAssignableFrom(type)) continue;

                    try
                    {
                        var renderer = (IMarkerGizmoRenderer)Activator.CreateInstance(type);
                        _renderers[renderer.TargetType] = renderer;
                    }
                    catch (Exception ex)
                    {
                        SBLog.Warn(SBLogTags.Pipeline, $"无法实例化 Renderer {type.Name}: {ex.Message}");
                    }
                }
            }

            if (_renderers.Count > 0)
            {
                var names = string.Join(", ", _renderers.Values.Select(r => r.GetType().Name));
                SBLog.Info(SBLogTags.Pipeline, $"已注册 {_renderers.Count} 个 Renderer: {names}");
            }
        }

        // ─── 主循环 ───

        private static void OnSceneGUI(SceneView sceneView)
        {
            _ = sceneView;

            // 获取缓存的标记列表
            var allMarkers = MarkerCache.GetAll();
            if (allMarkers.Count == 0)
            {
                _selectionController.ResetState();
                return;
            }
            if (_renderers.Count == 0)
            {
                _selectionController.ResetState();
                return;
            }

            // 预计算公共时间和脉冲值
            float time = (float)EditorApplication.timeSinceStartup;
            float pulseScale = GizmoStyleConstants.CalcPulseScale(time);
            float pulseAlpha = GizmoStyleConstants.CalcPulseAlpha(time);

            // 视锥裁剪 planes
            var camera = sceneView.camera;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);

            // ── 构建绘制列表（过滤图层 + 视锥裁剪）───
            _drawList.Clear();
            foreach (var marker in allMarkers)
            {
                if (marker == null) continue;

                // 图层可见性过滤
                if (!MarkerLayerSystem.IsMarkerVisible(marker.GetLayerPrefix(), marker.Tag)) continue;

                // 视锥裁剪
                var bounds = GetMarkerBounds(marker);
                if (!GeometryUtility.TestPlanesAABB(planes, bounds)) continue;

                // 构建绘制上下文
                _drawList.Add(BuildContext(marker, pulseScale, pulseAlpha));
            }

            if (_drawList.Count == 0)
            {
                _selectionController.ResetState();
                return;
            }

            // ── 按 Phase 顺序绘制 ───
            ExecutePhase(DrawPhase.Fill);
            ExecutePhase(DrawPhase.Wireframe);
            ExecutePhase(DrawPhase.Icon);
            ExecuteInteractivePhase();
            ExecutePhase(DrawPhase.Highlight);
            ExecutePhase(DrawPhase.Label);

            // ── Decoration Phase（Annotation 装饰层）───
            ExecuteDecorationPhase();

            // ── 拾取处理 ───
            // 收口策略：
            // - Tool 路由开启且 Tool 处于激活态时，仅走 Tool 入口，避免双通道重复处理；
            // - Tool 路由开启但 Tool 未激活时，保留 duringSceneGui 兜底（Unity 2021 焦点切换场景）。
            bool shouldUseDuringFallback = !_selectionInputDrivenByTool || !MarkerSelectTool.IsActive;
            if (shouldUseDuringFallback)
                HandlePicking();
        }

        // ─── 上下文构建 ───

        private static GizmoDrawContext BuildContext(SceneMarker marker, float pulseScale, float pulseAlpha)
        {
            var transform = marker.transform;
            var pos = transform.position;

            bool isSelected = Selection.activeGameObject == marker.gameObject;
            bool isHighlighted = SceneMarkerSelectionBridge.IsMarkerHighlighted(marker.MarkerId);

            var baseColor = GizmoStyleConstants.GetLayerColor(marker);
            var effectiveColor = isHighlighted
                ? GizmoStyleConstants.GetHighlightColor(baseColor)
                : baseColor;

            return new GizmoDrawContext
            {
                Marker = marker,
                Transform = transform,
                IsSelected = isSelected,
                IsHighlighted = isHighlighted,
                BaseColor = baseColor,
                EffectiveColor = effectiveColor,
                FillColor = GizmoStyleConstants.GetFillColor(baseColor, isSelected),
                PulseScale = isHighlighted ? pulseScale : 1f,
                PulseAlpha = isHighlighted ? pulseAlpha : 1f,
                HandleSize = HandleUtility.GetHandleSize(pos),
            };
        }

        // ─── 阶段执行 ───

        private static void ExecuteInteractivePhase()
        {
            foreach (var ctx in _drawList)
            {
                if (!ctx.IsSelected) continue;
                
                var renderer = GetRendererForMarker(ctx.Marker);
                if (renderer == null) continue;

                renderer.DrawInteractive(in ctx);
            }
        }

        /// <summary>
        /// Decoration 阶段——遍历 Marker 上的 MarkerAnnotation，调用 DrawGizmoDecoration。
        /// <para>在 Label 阶段之后、拾取处理之前执行。</para>
        /// </summary>
        private static void ExecuteDecorationPhase()
        {
            foreach (var ctx in _drawList)
            {
                var annotations = MarkerCache.GetAnnotations(ctx.Marker);
                for (int i = 0; i < annotations.Length; i++)
                {
                    if (annotations[i].HasGizmoDecoration)
                    {
                        annotations[i].DrawGizmoDecoration(ctx.IsSelected);
                    }
                }
            }
        }

        private static void ExecutePhase(DrawPhase phase)
        {
            foreach (var ctx in _drawList)
            {
                var renderer = GetRendererForMarker(ctx.Marker);
                if (renderer == null)
                    continue;

                switch (phase)
                {
                    case DrawPhase.Fill:
                        renderer.DrawFill(in ctx);
                        break;
                    case DrawPhase.Wireframe:
                        renderer.DrawWireframe(in ctx);
                        break;
                    case DrawPhase.Icon:
                        renderer.DrawIcon(in ctx);
                        break;
                    case DrawPhase.Highlight:
                        if (ctx.IsHighlighted) renderer.DrawHighlight(in ctx);
                        break;
                    case DrawPhase.Label:
                        renderer.DrawLabel(in ctx);
                        break;
                }
            }
        }

        /// <summary>
        /// 获取 Marker 的 Renderer（支持继承链查找）
        /// </summary>
        private static IMarkerGizmoRenderer? GetRendererForMarker(SceneMarker marker)
        {
            var markerType = marker.GetType();
            
            // 1. 精确匹配
            if (_renderers.TryGetValue(markerType, out var renderer))
                return renderer;
            
            // 2. 继承链查找（从子类向基类查找）
            var currentType = markerType.BaseType;
            while (currentType != null && typeof(SceneMarker).IsAssignableFrom(currentType))
            {
                if (_renderers.TryGetValue(currentType, out renderer))
                    return renderer;
                currentType = currentType.BaseType;
            }
            
            return null;
        }

        // ─── 拾取 ───

        private static void HandlePicking()
        {
            var evt = Event.current;
            if (evt != null && IsTraceEvent(evt.type))
            {
                SBLog.Debug(SBLogTags.Selection,
                    $"HandlePicking(duringSceneGui) evt={evt.type}, button={evt.button}, mods={evt.modifiers}, drawCount={_drawList.Count}, mode={_interactionMode}, routeByTool={_selectionInputDrivenByTool}");
                Trace($"HandlePicking(duringSceneGui) evt={evt.type}, button={evt.button}, mods={evt.modifiers}, drawCount={_drawList.Count}, mode={_interactionMode}, routeByTool={_selectionInputDrivenByTool}");
            }

            _selectionController.Handle(
                evt,
                _interactionMode,
                _hitTestService,
                _drawList,
                _renderers);
        }

        private static bool IsTraceEvent(EventType type)
        {
            return type == EventType.MouseDown
                || type == EventType.MouseUp
                || type == EventType.MouseDrag
                || type == EventType.Used
                || type == EventType.Ignore;
        }

        private static void Trace(string message)
        {
            // Debug.Log($"[SB.Selection.Trace][GizmoRenderPipeline] {message}");
        }

        // ─── Bounds 计算 ───

        /// <summary>
        /// 计算标记的 AABB 包围盒，用于视锥裁剪。
        /// </summary>
        private static Bounds GetMarkerBounds(SceneMarker marker)
        {
            var pos = marker.transform.position;

            // 使用 is 运算符支持继承（switch 模式匹配在某些情况下可能不匹配子类）
            if (marker is PointMarker pm)
            {
                return new Bounds(pos, Vector3.one * pm.GizmoRadius * 2f);
            }
            
            if (marker is AreaMarker am)
            {
                if (am.Shape == AreaShape.Box)
                {
                    var size = am.BoxSize;
                    // 考虑旋转后的包围盒
                    var rotatedSize = am.transform.rotation * size;
                    return new Bounds(pos, new Vector3(
                        Mathf.Abs(rotatedSize.x),
                        Mathf.Abs(rotatedSize.y),
                        Mathf.Abs(rotatedSize.z)));
                }
                else
                {
                    // Polygon：遍历世界坐标顶点计算包围盒
                    var verts = am.GetWorldVertices();
                    if (verts.Count == 0)
                        return new Bounds(pos, Vector3.one * 2f);

                    var bounds = new Bounds(verts[0], Vector3.zero);
                    for (int i = 1; i < verts.Count; i++)
                        bounds.Encapsulate(verts[i]);
                    // 扩展高度
                    bounds.Encapsulate(bounds.center + Vector3.up * am.Height);
                    return bounds;
                }
            }

            return new Bounds(pos, Vector3.one * 2f);
        }
    }
}
