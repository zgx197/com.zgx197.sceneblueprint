#nullable enable
using System.Linq;
using UnityEngine;
using UnityEditor;
using SceneBlueprint.Core;
using SceneBlueprint.Editor.Logging;
using SceneBlueprint.Editor.Markers.Definitions;
using SceneBlueprint.Editor.SpatialModes;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprint.Editor.Markers
{
    /// <summary>
    /// Scene View 标记创建工具。
    /// <para>
    /// 职责：
    /// <list type="bullet">
    ///   <item>在 Scene View 中提供 Shift+右键菜单，创建空白标记（区域标记、点标记）</item>
    /// </list>
    /// </para>
    /// <para>
    /// 标记创建后，策划通过 AreaMarkerEditor 的位置生成工具铺设子 PointMarker，
    /// 并通过 MarkerAnnotation 组件（如 SpawnAnnotation）附加业务数据。
    /// Blueprint 节点的绑定通过 Inspector 手动拖入完成。
    /// </para>
    /// <para>
    /// 使用方式：由 <see cref="SceneBlueprintWindow"/> 在打开时启用，关闭时禁用。
    /// 通过 <see cref="SceneView.duringSceneGui"/> 回调注入 Scene View 事件处理。
    /// </para>
    /// </summary>
    public static class SceneViewMarkerTool
    {
        // ─── 状态 ───

        private static bool _enabled;
        private static bool _createInputDrivenByTool;
        private static IActionRegistry? _registry;
        private static Vector3 _lastRightClickWorldPos;
        private static IEditorSpatialModeDescriptor? _spatialMode;

        /// <summary>当前是否已启用标记创建工具。</summary>
        public static bool IsEnabled => _enabled;

        /// <summary>
        /// 设置创建输入来源。
        /// false = 使用 legacy duringSceneGui 路由；
        /// true  = 使用 MarkerSelectTool.OnToolGUI 路由（P2）。
        /// </summary>
        public static void SetCreateInputDrivenByTool(bool drivenByTool)
        {
            if (_createInputDrivenByTool == drivenByTool)
                return;

            UnityEngine.Debug.Log($"[SceneViewMarkerTool] SetCreateInputDrivenByTool: {_createInputDrivenByTool} → {drivenByTool}, enabled={_enabled}");
            _createInputDrivenByTool = drivenByTool;

            if (!_enabled)
                return;

            if (_createInputDrivenByTool)
                SceneView.duringSceneGui -= OnSceneGUI;
            else
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.duringSceneGui += OnSceneGUI;
            }
        }

        // ─── 启用/禁用 ───

        /// <summary>
        /// 启用 Scene View 标记工具。
        /// <para>由蓝图编辑器窗口在打开时调用。</para>
        /// </summary>
        /// <param name="registry">Action 注册表（用于获取 SceneRequirements）</param>
        public static void Enable(
            IActionRegistry registry,
            IEditorSpatialModeDescriptor spatialMode)
        {
            _registry = registry ?? throw new System.ArgumentNullException(nameof(registry));
            _spatialMode = spatialMode ?? throw new System.ArgumentNullException(nameof(spatialMode));
            if (_enabled)
            {
                UnityEngine.Debug.Log($"[SceneViewMarkerTool] Enable 跳过（已启用）, drivenByTool={_createInputDrivenByTool}");
                return;
            }
            _enabled = true;

            if (!_createInputDrivenByTool)
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.duringSceneGui += OnSceneGUI;
                UnityEngine.Debug.Log("[SceneViewMarkerTool] Enable → 注册 duringSceneGui 回调");
            }
            else
            {
                UnityEngine.Debug.Log("[SceneViewMarkerTool] Enable → drivenByTool=true，不注册 duringSceneGui");
            }
        }

        /// <summary>
        /// 禁用 Scene View 标记工具。
        /// <para>由蓝图编辑器窗口在关闭时调用。</para>
        /// </summary>
        public static void Disable()
        {
            if (!_enabled) return;
            _enabled = false;
            _registry = null;
            _spatialMode = null;
            SceneView.duringSceneGui -= OnSceneGUI;
            UnityEngine.Debug.Log("[SceneViewMarkerTool] Disable → 已禁用并移除回调");
        }

        /// <summary>
        /// 由 MarkerSelectTool 转发的创建输入入口（P2）。
        /// </summary>
        public static void HandleCreateFromTool(Event evt, SceneView sceneView)
        {
            if (!_createInputDrivenByTool)
                return;

            if (!_enabled || _registry == null || _spatialMode == null)
                return;

            HandleCreateEvent(evt, sceneView);
        }

        // ─── Scene View 事件处理 ───

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!_enabled || _registry == null || _spatialMode == null) return;

            if (_createInputDrivenByTool)
                return;

            HandleCreateEvent(Event.current, sceneView);
        }

        private static void HandleCreateEvent(Event evt, SceneView sceneView)
        {
            if (evt == null)
                return;

            // 右键点击（MouseUp 避免与 Unity 原生右键按下阶段冲突）
            if (evt.type == EventType.MouseUp && evt.button == 1 && (evt.modifiers & EventModifiers.Shift) != 0)
            {
                // Shift + 右键 → 标记创建菜单（避免覆盖 Unity 原生右键菜单）
                if (TryRaycastGround(evt.mousePosition, sceneView, out var worldPos))
                {
                    _lastRightClickWorldPos = worldPos;
                    evt.Use();
                    ShowCreateMenu(worldPos);
                }
            }
        }

        /// <summary>
        /// 从鼠标位置获取世界坐标。
        /// 实现已下沉到 Adapter 层，此处仅保留按运行时空间的分发。
        /// </summary>
        private static bool TryRaycastGround(Vector2 mousePos, SceneView sceneView, out Vector3 worldPos)
        {
            if (_spatialMode == null)
            {
                worldPos = Vector3.zero;
                return false;
            }

            return _spatialMode.TryGetSceneViewPlacement(mousePos, sceneView, out worldPos);
        }

        // ─── 右键菜单 ───

        private static void ShowCreateMenu(Vector3 worldPos)
        {
            if (_registry == null) return;

            var menu = new GenericMenu();

            // 基础标记类型
            var markerDefinitions = MarkerDefinitionRegistry.GetAll()
                .OrderBy(d => d.DisplayName)
                .ThenBy(d => d.TypeId)
                .ToList();

            foreach (var definition in markerDefinitions)
            {
                string displayName = string.IsNullOrEmpty(definition.DisplayName)
                    ? definition.TypeId
                    : definition.DisplayName;
                string label = $"空白 {displayName}";
                var definitionCopy = definition;
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    CreateStandaloneMarkerFromDefinition(definitionCopy, worldPos);
                });
            }

            menu.ShowAsContext();
        }

        // ─── 标记创建 ───

        private static void CreateStandaloneMarkerFromDefinition(MarkerDefinition markerDef, Vector3 position)
        {
            string displayName = string.IsNullOrEmpty(markerDef.DisplayName)
                ? markerDef.TypeId
                : markerDef.DisplayName;

            string markerName = $"新{displayName}";
            var marker = MarkerHierarchyManager.CreateMarker(
                markerDef.ComponentType,
                markerName,
                position,
                tag: "");

            markerDef.Initializer?.Invoke(marker);
            Selection.activeGameObject = marker.gameObject;
            EditorGUIUtility.PingObject(marker.gameObject);
        }

    }

}
