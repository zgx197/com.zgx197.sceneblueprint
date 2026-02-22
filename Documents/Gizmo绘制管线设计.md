# Gizmo 绘制管线设计文档

> 版本：v1.0  
> 日期：2026-02-14  
> 状态：核心管线已实现  
> doc_status: frozen  
> last_reviewed: 2026-02-15

## 1. 背景与动机

当前 `MarkerGizmoDrawer` 使用 `[DrawGizmo]` + 散落的 `SceneView.duringSceneGui` 实现标记绘制，存在以下问题：

| 问题 | 现状 | 影响 |
|------|------|------|
| 绘制顺序不可控 | `[DrawGizmo]` 按类型触发，无法指定先后 | 半透明填充遮挡线框、标签被覆盖 |
| 重复遍历 | `FindObjectsOfType` 在 DrawGizmo/Pick 各调一次 | 标记多时性能下降 |
| 状态分散 | 颜色、高亮、脉冲逻辑散落各方法 | 新增类型需到处修改 |
| API 混用 | Gizmos（立即模式）+ Handles 混合 | 无法统一管理矩阵和颜色栈 |
| 扩展困难 | 硬编码的 switch/if，无接口抽象 | 新增标记类型改动大 |

**目标**：建立统一的 **分阶段 Handles 绘制管线**，实现：
- ✅ 严格可控的绘制顺序
- ✅ 单次遍历 + 标记缓存
- ✅ 接口化，新增类型零侵入
- ✅ 内置拾取支持
- ✅ 与图层系统、双向联动无缝集成

---

## 2. 核心架构

```
┌─────────────────────────────────────────────────────────┐
│                  GizmoRenderPipeline                     │
│              (SceneView.duringSceneGui)                   │
│                                                          │
│  ┌──────────┐   ┌──────────────────────────────────┐    │
│  │MarkerCache│──▶│  foreach marker:                  │    │
│  │(缓存+脏标记)│   │    ctx = BuildDrawContext(marker) │    │
│  └──────────┘   │    renderer = GetRenderer(marker)  │    │
│                  │                                    │    │
│                  │  Phase 0: Fill      (半透明填充)    │    │
│                  │  Phase 1: Wireframe (线框/边框)     │    │
│                  │  Phase 2: Icon      (球体/菱形)     │    │
│                  │  Phase 3: Highlight (脉冲/光晕)     │    │
│                  │  Phase 4: Label     (文字标签)      │    │
│                  │  Phase 5: Pick      (拾取碰撞)      │    │
│                  └──────────────────────────────────┘    │
│                                                          │
│  ┌──────────────────┐  ┌──────────────────┐             │
│  │PointMarkerRenderer│  │AreaMarkerRenderer │  ...       │
│  │(implements IGizmo) │  │(implements IGizmo)│             │
│  └──────────────────┘  └──────────────────┘             │
└─────────────────────────────────────────────────────────┘
```

### 2.1 绘制阶段（DrawPhase）

```csharp
public enum DrawPhase
{
    Fill,        // Phase 0: 半透明填充面（最先绘制，不遮挡后续内容）
    Wireframe,   // Phase 1: 线框、边框、方向箭头
    Icon,        // Phase 2: 实心球体、菱形等图标图形
    Interactive, // Phase 3: 选中时的编辑 Handle（拖拽顶点、Box Handle 等）
    Highlight,   // Phase 4: 高亮脉冲、外扩光晕（叠加层）
    Label,       // Phase 5: 文字标签（始终最上层）
    Pick,        // Phase 6: 不可见拾取区域（不绘制，只检测）
}
```

**顺序保证**：管线在单个 `duringSceneGui` 回调中按 Phase 枚举顺序执行，每个 Phase 遍历所有可见标记，确保同一 Phase 的所有标记在同一批次完成。

### 2.2 绘制上下文（GizmoDrawContext）

```csharp
/// <summary>
/// 单个标记的绘制上下文——每帧为每个标记预计算一次，
/// 传递给 IMarkerGizmoRenderer 的各 Phase 方法。
/// 避免 Renderer 内部重复计算颜色、状态。
/// </summary>
public struct GizmoDrawContext
{
    // ─── 标记引用 ───
    public SceneMarker Marker;
    public Transform Transform;

    // ─── 状态 ───
    public bool IsSelected;       // Unity Selection 中被选中
    public bool IsHighlighted;    // 蓝图联动高亮
    public bool IsLayerVisible;   // 图层可见（冗余，不可见的已被过滤）

    // ─── 预计算样式 ───
    public Color BaseColor;       // 图层颜色
    public Color EffectiveColor;  // 最终颜色（含高亮加亮）
    public Color FillColor;       // 半透明填充色
    public float PulseScale;      // 脉冲缩放因子（1.0 = 无脉冲）
    public float PulseAlpha;      // 脉冲透明度因子（0~1）

    // ─── 工具 ───
    public float HandleSize;      // HandleUtility.GetHandleSize 缓存
}
```

### 2.3 标记渲染器接口（IMarkerGizmoRenderer）

```csharp
/// <summary>
/// 标记 Gizmo 渲染器接口。
/// 每种标记类型（Point/Area/Entity/自定义）实现一个 Renderer，
/// 在管线中按 Phase 顺序被调用。
/// </summary>
public interface IMarkerGizmoRenderer
{
    /// <summary>支持的标记 Component 类型</summary>
    System.Type TargetType { get; }

    /// <summary>Phase 0: 绘制半透明填充面</summary>
    void DrawFill(GizmoDrawContext ctx) { }

    /// <summary>Phase 1: 绘制线框和边框</summary>
    void DrawWireframe(GizmoDrawContext ctx) { }

    /// <summary>Phase 2: 绘制图标图形（球体、菱形等）</summary>
    void DrawIcon(GizmoDrawContext ctx) { }

    /// <summary>Phase 3: 绘制高亮效果（仅 IsHighlighted 时调用）</summary>
    void DrawHighlight(GizmoDrawContext ctx) { }

    /// <summary>Phase 4: 绘制文字标签</summary>
    void DrawLabel(GizmoDrawContext ctx) { }

    /// <summary>
    /// Phase 3: 选中时绘制交互编辑 Handle（拖拽顶点、Box Handle 等）。
    /// 仅在 ctx.IsSelected 时由管线调用。
    /// 返回 true 表示本 Renderer 接管了 Fill/Wireframe 的绘制，
    /// 管线将跳过该标记的 Phase 0/1。
    /// </summary>
    bool DrawInteractive(GizmoDrawContext ctx) => false;

    /// <summary>Phase 6: 返回拾取区域信息（中心点 + 半径）</summary>
    PickBounds GetPickBounds(GizmoDrawContext ctx);
}

public struct PickBounds
{
    public Vector3 Center;
    public float Radius;
}
```

**默认实现**：接口方法使用 C# 8 默认接口方法（空实现），Renderer 只需覆写需要的 Phase。

---

## 3. 标记缓存（MarkerCache）

```csharp
/// <summary>
/// 场景标记缓存——避免每帧 FindObjectsOfType。
/// 监听 EditorApplication.hierarchyChanged 自动刷新。
/// </summary>
public static class MarkerCache
{
    private static List<SceneMarker> _all = new();
    private static bool _dirty = true;

    // 按 Component 类型分桶（避免 is/as 检查）
    private static Dictionary<Type, List<SceneMarker>> _byType = new();

    static MarkerCache()
    {
        EditorApplication.hierarchyChanged += () => _dirty = true;
    }

    /// <summary>获取所有可见标记（自动刷新缓存）</summary>
    public static IReadOnlyList<SceneMarker> GetAll()
    {
        EnsureFresh();
        return _all;
    }

    /// <summary>获取指定类型的标记列表</summary>
    public static List<SceneMarker> GetByType<T>() where T : SceneMarker
    {
        EnsureFresh();
        _byType.TryGetValue(typeof(T), out var list);
        return list ?? new List<SceneMarker>();
    }

    /// <summary>手动标记缓存过期</summary>
    public static void SetDirty() => _dirty = true;

    private static void EnsureFresh()
    {
        if (!_dirty) return;
        _dirty = false;

        _all.Clear();
        _byType.Clear();

        var markers = Object.FindObjectsOfType<SceneMarker>();
        foreach (var m in markers)
        {
            _all.Add(m);
            var type = m.GetType();
            if (!_byType.TryGetValue(type, out var list))
            {
                list = new List<SceneMarker>();
                _byType[type] = list;
            }
            list.Add(m);
        }
    }
}
```

**刷新时机**：
- `EditorApplication.hierarchyChanged`（创建/删除/Undo）
- `SceneView.duringSceneGui` 前检查 `_dirty`
- 手动 `SetDirty()`（标记属性变化时）

---

## 4. 管线主循环（GizmoRenderPipeline）

```csharp
[InitializeOnLoad]
public static class GizmoRenderPipeline
{
    // ─── 渲染器注册表 ───
    private static readonly Dictionary<Type, IMarkerGizmoRenderer> _renderers = new();

    static GizmoRenderPipeline()
    {
        // 自动发现所有 IMarkerGizmoRenderer 实现
        AutoDiscoverRenderers();

        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// <summary>反射自动发现并注册所有 Renderer</summary>
    private static void AutoDiscoverRenderers() { ... }

    /// <summary>手动注册 Renderer（供第三方扩展）</summary>
    public static void RegisterRenderer(IMarkerGizmoRenderer renderer)
    {
        _renderers[renderer.TargetType] = renderer;
    }

    // ─── 主循环 ───

    private static void OnSceneGUI(SceneView sceneView)
    {
        var allMarkers = MarkerCache.GetAll();
        if (allMarkers.Count == 0) return;

        // 预计算公共脉冲值（所有标记共享同一时间基准）
        float time = (float)EditorApplication.timeSinceStartup;
        float pulseScale = 1f + 0.3f * (0.5f + 0.5f * Mathf.Sin(time * 5f));
        float pulseAlpha = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(time * 5f));

        // ── 构建绘制列表（过滤不可见图层）───
        var drawList = new List<GizmoDrawContext>();
        foreach (var marker in allMarkers)
        {
            if (marker == null) continue;
            var prefix = marker.GetLayerPrefix();
            if (!MarkerLayerSystem.IsMarkerVisible(prefix)) continue;

            var ctx = BuildContext(marker, pulseScale, pulseAlpha);
            drawList.Add(ctx);
        }

        if (drawList.Count == 0) return;

        // ── 视锥裁剪 ───
        var planes = GeometryUtility.CalculateFrustumPlanes(sceneView.camera);
        // ... 构建 drawList 时用 TestPlanesAABB 过滤 ...

        // ── Phase 3 Interactive 先行（记录接管标记）───
        var interactiveSet = ExecuteInteractivePhase(drawList);

        // ── 按 Phase 顺序绘制 ───
        ExecutePhase(drawList, DrawPhase.Fill, interactiveSet);
        ExecutePhase(drawList, DrawPhase.Wireframe, interactiveSet);
        ExecutePhase(drawList, DrawPhase.Icon);
        ExecutePhase(drawList, DrawPhase.Highlight);
        ExecutePhase(drawList, DrawPhase.Label);
        HandlePicking(drawList);
    }

    /// <summary>先执行 Interactive Phase，返回被接管的标记集合</summary>
    private static HashSet<SceneMarker> ExecuteInteractivePhase(List<GizmoDrawContext> list)
    {
        var taken = new HashSet<SceneMarker>();
        foreach (var ctx in list)
        {
            if (!ctx.IsSelected) continue;
            if (!_renderers.TryGetValue(ctx.Marker.GetType(), out var renderer)) continue;
            if (renderer.DrawInteractive(ctx))
                taken.Add(ctx.Marker);
        }
        return taken;
    }

    private static void ExecutePhase(List<GizmoDrawContext> list, DrawPhase phase,
        HashSet<SceneMarker>? interactiveSet = null)
    {
        foreach (var ctx in list)
        {
            if (!_renderers.TryGetValue(ctx.Marker.GetType(), out var renderer))
                continue;

            // Interactive 接管的标记跳过 Fill/Wireframe
            if (interactiveSet != null && interactiveSet.Contains(ctx.Marker)
                && (phase == DrawPhase.Fill || phase == DrawPhase.Wireframe))
                continue;

            switch (phase)
            {
                case DrawPhase.Fill:      renderer.DrawFill(ctx);      break;
                case DrawPhase.Wireframe: renderer.DrawWireframe(ctx); break;
                case DrawPhase.Icon:      renderer.DrawIcon(ctx);      break;
                case DrawPhase.Highlight:
                    if (ctx.IsHighlighted) renderer.DrawHighlight(ctx);
                    break;
                case DrawPhase.Label:     renderer.DrawLabel(ctx);     break;
            }
        }
    }

    // ── 拾取处理 ───
    private static void HandlePicking(List<GizmoDrawContext> list) { ... }
}
```

---

## 5. 内置 Renderer 实现示例

### 5.1 PointMarkerRenderer

```csharp
public class PointMarkerRenderer : IMarkerGizmoRenderer
{
    public Type TargetType => typeof(PointMarker);

    public void DrawIcon(GizmoDrawContext ctx)
    {
        var pm = (PointMarker)ctx.Marker;
        var pos = ctx.Transform.position;
        float r = pm.GizmoRadius * ctx.PulseScale;

        // 实心球
        Handles.color = ctx.EffectiveColor;
        Handles.SphereHandleCap(0, pos, Quaternion.identity, r * 2, EventType.Repaint);

        // 方向箭头
        if (pm.ShowDirection)
        {
            Handles.color = ctx.EffectiveColor;
            Handles.ArrowHandleCap(0, pos, ctx.Transform.rotation,
                r * 3f, EventType.Repaint);
        }
    }

    public void DrawHighlight(GizmoDrawContext ctx)
    {
        var pos = ctx.Transform.position;
        float r = ((PointMarker)ctx.Marker).GizmoRadius;

        // 外圈脉冲光晕
        Handles.color = new Color(
            ctx.EffectiveColor.r, ctx.EffectiveColor.g,
            ctx.EffectiveColor.b, ctx.PulseAlpha * 0.3f);
        Handles.SphereHandleCap(0, pos, Quaternion.identity,
            r * 3.6f, EventType.Repaint);
    }

    public void DrawLabel(GizmoDrawContext ctx)
    {
        var pm = (PointMarker)ctx.Marker;
        var pos = ctx.Transform.position + Vector3.up * (pm.GizmoRadius + 0.5f);
        GizmoLabelUtil.DrawStandardLabel(ctx.Marker, pos, ctx.EffectiveColor);
    }

    public PickBounds GetPickBounds(GizmoDrawContext ctx)
    {
        var pm = (PointMarker)ctx.Marker;
        return new PickBounds
        {
            Center = ctx.Transform.position,
            Radius = pm.GizmoRadius * 2f
        };
    }
}
```

### 5.2 AreaMarkerRenderer（Box）

```csharp
public class AreaMarkerRenderer : IMarkerGizmoRenderer
{
    public Type TargetType => typeof(AreaMarker);

    public void DrawFill(GizmoDrawContext ctx)
    {
        var am = (AreaMarker)ctx.Marker;
        if (am.Shape == AreaShape.Box)
        {
            var matrix = Handles.matrix;
            Handles.matrix = ctx.Transform.localToWorldMatrix;
            Handles.color = ctx.IsHighlighted
                ? new Color(ctx.EffectiveColor.r, ctx.EffectiveColor.g,
                    ctx.EffectiveColor.b, ctx.PulseAlpha * 0.4f)
                : ctx.FillColor;
            Handles.DrawSolidDisc(Vector3.zero, Vector3.up, ...); // 或用 Mesh 绘制
            Handles.matrix = matrix;
        }
        // Polygon 模式类似...
    }

    public void DrawWireframe(GizmoDrawContext ctx)
    {
        // 线框绘制（Box: DrawWireCube 等效，Polygon: DrawLine 循环）
    }

    public void DrawHighlight(GizmoDrawContext ctx)
    {
        // 高亮外扩框 + 中心光晕
    }

    // ...
}
```

---

## 6. 拾取系统集成

```
HandlePicking(drawList):
    ├── 只在 MouseDown(button=0) 时执行
    ├── 遍历 drawList，调用 renderer.GetPickBounds(ctx)
    ├── 用 HandleUtility.DistanceToCircle 计算距离
    ├── 取距离最近的标记
    ├── GUIUtility.hotControl 抢占 + MouseUp 释放
    └── Selection.activeGameObject = picked.gameObject
```

与当前实现的区别：
- **不再单独遍历** `FindObjectsOfType`，复用 `drawList`
- **Pick 半径由 Renderer 定义**，而非硬编码
- **拾取结果可扩展**（如右键弹出标记菜单）

---

## 7. 与现有系统的集成

### 7.1 图层系统（MarkerLayerSystem）
- 管线主循环在构建 drawList 时过滤不可见图层
- `MarkerLayerOverlay` 面板不变，切换时调用 `SceneView.RepaintAll()`

### 7.2 双向联动（SceneMarkerSelectionBridge）
- `GizmoDrawContext.IsHighlighted` 从 `SceneMarkerSelectionBridge.IsMarkerHighlighted` 读取
- 高亮时管线自动启动持续重绘（复用现有 `EditorApplication.update` 机制）

### 7.3 AreaMarker 编辑（已迁入管线）
- `AreaMarkerEditor` 已删除，编辑逻辑迁入 `AreaMarkerRenderer.DrawInteractive`
- 选中 AreaMarker 时，`DrawInteractive` 返回 true → 管线跳过该标记的 Fill/Wireframe
- Box 模式：`BoxBoundsHandle` + 完整 6 面填充 + 12 边线框
- Polygon 模式：顶点拖拽 Handle + 右键添加/删除顶点

---

## 8. 迁移计划

### Step 1: 基础框架 ✅
- 创建 `GizmoRenderPipeline`、`MarkerCache`、`GizmoDrawContext`、`IMarkerGizmoRenderer`
- 注册 `SceneView.duringSceneGui`
- 自动发现所有 `IMarkerGizmoRenderer` 实现

### Step 2: 迁移 Renderer ✅
- 实现 `PointMarkerRenderer`、`AreaMarkerRenderer`、`EntityMarkerRenderer`
- 已删除对应的 `[DrawGizmo]` 方法
- Gizmo 外观与旧版一致

### Step 3: 拾取迁移 + Interactive Phase ✅
- 拾取逻辑已移入管线 `HandlePicking`，复用 drawList
- `AreaMarkerRenderer.DrawInteractive` 已实现（Box Handle 6面填充 + 12边线框 + 多边形顶点拖拽）
- `AreaMarkerEditor.cs` 已删除
- `MarkerGizmoDrawer.OnSceneGUI` + `PickMarkerAtMouse` 已删除

### Step 4: 清理 ✅
- `MarkerGizmoDrawer` 已精简为遗留兼容文件（仅保留 `GetMarkerColor`）
- `GizmoStyleConstants` 集中管理所有颜色、透明度、脉冲参数
- `SceneMarkerSelectionBridge` 持续重绘逻辑已更新

> **状态：全部迁移完成（2026-02-14）**

---

## 9. 文件结构

```
Editor/Markers/
  ├── Pipeline/
  │   ├── GizmoRenderPipeline.cs       ← 管线主循环 + 阶段调度
  │   ├── GizmoDrawContext.cs          ← 绘制上下文结构体
  │   ├── IMarkerGizmoRenderer.cs      ← 渲染器接口 + DrawPhase 枚举
  │   ├── MarkerCache.cs               ← 标记缓存 + 脏刷新
  │   ├── GizmoStyleConstants.cs       ← 颜色、尺寸等常量
  │   └── GizmoLabelUtil.cs            ← 通用标签绘制工具
  │
  ├── Renderers/
  │   ├── PointMarkerRenderer.cs       ← Point 标记渲染器
  │   ├── AreaMarkerRenderer.cs        ← Area 标记渲染器
  │   └── EntityMarkerRenderer.cs      ← Entity 标记渲染器
  │
  ├── MarkerLayerSystem.cs             ← 图层系统（不变）
  ├── MarkerLayerOverlay.cs            ← 图层面板（不变）
  ├── SceneMarkerSelectionBridge.cs    ← 双向联动（微调）
  ├── AreaMarkerEditor.cs              ← 区域编辑器（Step 3 后删除，迁移到 AreaMarkerRenderer.DrawInteractive）
  └── MarkerBindingValidator.cs        ← 验证系统（不变）
```

---

## 10. 性能预期

| 指标 | 旧方案 | 新方案 |
|------|--------|--------|
| FindObjectsOfType 次数/帧 | 3~6 次（DrawGizmo×3 + Pick×3） | **1 次**（缓存+脏刷新） |
| 标记遍历次数/帧 | 标记数 × 6（每类型每 DrawGizmo 1 次） | **标记数 × 6 Phase**（但单次遍历） |
| 绘制顺序 | 不可控 | **严格 Phase 顺序** |
| 新增标记类型 | 改 DrawGizmo + Pick + 颜色等 | **只需实现 IMarkerGizmoRenderer** |
| 内存分配 | 每帧 new List（FindObjectsOfType 返回值） | **缓存复用，脏时才刷新** |

---

## 11. 已确认决策

| # | 问题 | 决策 | 状态 |
|---|------|------|------|
| Q1 | Area Fill API 选择 | Box: `DrawSolidRectangleWithOutline`（6 面 × 4 顶点），Polygon: `DrawAAConvexPolygon` | ✅ 已实现 |
| Q2 | 自定义 Phase 扩展点 | 7 Phase 足够，如需扩展在 `DrawPhase` 枚举中加值 | ✅ 确认 |
| Q3 | 视锥裁剪 | `GeometryUtility.CalculateFrustumPlanes` + `TestPlanesAABB` | ✅ 已实现 |
| Q4 | 编辑 Handle 协调 | `AreaMarkerEditor` 已删除，迁入 `DrawInteractive`，返回 true 跳过 Fill/Wireframe | ✅ 已实现 |
| Q5 | Renderer 自动发现 | 反射扫描 `IMarkerGizmoRenderer` 实现，新增 Renderer 零侥入 | ✅ 已实现 |
| Q6 | Box 线框绘制 | 手动 `Handles.DrawLine` 12 条边，替代不可靠的 `Handles.DrawWireCube` | ✅ 已实现 |
| Q7 | Box 6 面填充 | `DrawAllBoxFaces` 绘制完整 6 面（底、顶、前、后、左、右） | ✅ 已实现 |
