# SceneMarker 系统设计

> 版本：v0.4  
> 日期：2026-02-17  
> 状态：核心功能已实现（标记体系 + Gizmo 管线 + 绑定系统 + 双向联动 + 日志系统 + 标记定义注册表 + **标记标注系统**）  
> 关联：[场景蓝图系统总体设计](场景蓝图系统总体设计.md)、[标记标注系统设计](标记标注系统设计.md)  
> doc_status: active  
> last_reviewed: 2026-02-17

---

## 1. 概述

### 1.1 问题背景

关卡设计师的工作流是**空间导向**的：在白模地形上规划战斗区域、放置刷怪点、布置触发器和演出事件。而蓝图编辑器是**逻辑导向**的：节点和连线表达执行顺序和条件关系。

当前的 SceneBinding 机制要求设计师手动将场景对象拖入蓝图节点的属性字段，导致：
- **空间上下文断裂**：蓝图中看不到标记在场景中的位置
- **频繁窗口切换**：Scene View ↔ 蓝图编辑器 ↔ Inspector
- **绑定操作碎片化**：每个绑定字段都需要手动拖拽

### 1.2 设计目标

SceneMarker 系统旨在桥接 Scene View 和蓝图编辑器，实现：

1. **空间→逻辑一步到位**：在 Scene View 中右键创建标记，自动生成蓝图节点并绑定
2. **分层可视化**：不同类型的标记按图层管理，可独立切换可见性
3. **双向联动**：选中蓝图节点 ↔ 场景高亮对应标记
4. **Tag 系统集成**：标记自动携带 Tag，图层映射由 Tag 前缀驱动

---

## 2. 核心概念

### 2.1 标记与蓝图节点的关系

```
关系模型：标记独立存在，节点按需引用

  Scene View                          蓝图编辑器
  ┌──────────┐                       ┌──────────┐
  │AreaMarker│──── SceneBinding ────▶│  Spawn   │
  │  (区域)  │     (markerId)        │  Action  │
  └──────────┘                       │          │
  ┌──────────┐                       │          │
  │PointMarker│─── SceneBinding ────▶│          │
  │  (点位1) │     (markerId)        └──────────┘
  └──────────┘
  ┌──────────┐
  │PointMarker│─── SceneBinding ────▶ (同一节点或不同节点)
  │  (点位2) │
  └──────────┘

特性：
  - 一个蓝图节点可以引用多个标记（如 Spawn 引用 1 区域 + N 点位）
  - 一个标记可以被多个节点引用（如同一触发区域触发战斗 + 灯光变化）
  - 标记在 Scene View 中独立于蓝图存在，可以先放标记后绑定
```

### 2.2 标记类型

| 类型 | 空间形态 | 场景表达 | 典型用途 |
|------|---------|---------|---------|
| **PointMarker** | 单点（Transform） | 位置 + 朝向 Gizmo | 刷怪点、摄像机位、VFX 播放点、路径点 |
| **AreaMarker** | 多边形 / Box 区域 | 半透明区域 + 边框 | 触发区、刷怪区、灯光区、音频区 |
| **EntityMarker** | Prefab 实例 | 实际 Prefab 预览 | 预设怪物、可交互物体、NPC |

### 2.3 图层系统

图层由 Tag 前缀自动映射，设计师可按需切换可见性：

| 图层 | Tag 前缀 | Gizmo 颜色 | 包含内容 |
|------|---------|-----------|---------|
| 🔴 Combat | `Combat.*` | 红色 | 刷怪点、刷怪区、伏击点、巡逻路径 |
| 🔵 Trigger | `Trigger.*` | 蓝色 | 触发区域、阻挡体、进度门 |
| 🟡 Environment | `Environment.*` | 黄色 | 灯光区域、音频区域、雾效区域 |
| 🟢 Camera | `Camera.*` | 绿色 | 摄像机位、注视目标、运镜路径 |
| 🟣 Narrative | `Narrative.*` | 紫色 | 对话触发点、笔记拾取点 |

---

## 3. 组件设计

### 3.1 SceneMarker（抽象基类）

```csharp
/// <summary>
/// 场景标记基类 — 蓝图节点与场景空间的桥梁
/// </summary>
public abstract class SceneMarker : MonoBehaviour
{
    [ReadOnly] public string MarkerId;           // 唯一 ID（蓝图通过此 ID 引用）
    public string MarkerName;                     // 设计师可读名称
    public string Tag;                            // Tag 标签（如 "Combat.SpawnPoint"）
    [ReadOnly] public string SubGraphId;          // 所属子蓝图 ID（可空=顶层）

    /// <summary>标记类型 ID（字符串，对应 MarkerTypeIds 常量）</summary>
    public abstract string MarkerTypeId { get; }

    /// <summary>返回标记的代表位置（用于双向联动聚焦）</summary>
    public virtual Vector3 GetRepresentativePosition() => transform.position;
}
```

> **设计决策（v0.2）**：旧版使用 `MarkerType` 枚举（Point/Area/Entity），已替换为 `MarkerTypeIds` 字符串常量类 + `string MarkerTypeId` 属性。新增标记类型无需修改枚举，只需添加字符串常量 + Provider 文件即可。

### 3.2 PointMarker

```csharp
/// <summary>单点标记 — 表示一个位置 + 朝向</summary>
public class PointMarker : SceneMarker
{
    public override string MarkerTypeId => MarkerTypeIds.Point;
    public float GizmoRadius = 0.5f;             // Gizmo 显示半径
    public bool ShowDirection = true;             // 是否显示方向箭头
}
```

### 3.3 AreaMarker

```csharp
/// <summary>区域标记 — 表示一个多边形或 Box 区域</summary>
public class AreaMarker : SceneMarker
{
    public override string MarkerTypeId => MarkerTypeIds.Area;

    public AreaShape Shape = AreaShape.Box;        // Polygon / Box
    public List<Vector3> Vertices = new();         // 多边形顶点（相对坐标）
    public Vector3 BoxSize = new(8f, 3f, 8f);     // Box 模式的尺寸
    public float Height = 3f;                      // 区域高度（用于体积判定）

    /// <summary>返回区域中心</summary>
    public override Vector3 GetRepresentativePosition()
    {
        if (Shape == AreaShape.Box) return transform.position;
        if (Vertices.Count == 0) return transform.position;
        var center = Vector3.zero;
        foreach (var v in Vertices) center += v;
        return transform.position + center / Vertices.Count;
    }
}

public enum AreaShape { Polygon, Box }
```

### 3.4 EntityMarker

```csharp
/// <summary>实体标记 — 表示一个 Prefab 实例的放置</summary>
public class EntityMarker : SceneMarker
{
    public override string MarkerTypeId => MarkerTypeIds.Entity;

    public GameObject PrefabRef;                  // 引用的 Prefab
    public int Count = 1;                         // 数量（用于刷怪等场景）
}
```

### 3.5 MarkerRequirement（Action 场景需求声明）

```csharp
/// <summary>
/// Action 声明它需要什么类型的场景标记。
/// 放在 ActionDefinition 中，驱动：
///   - Scene View 右键菜单自动创建对应标记
///   - Inspector 自动生成绑定 UI
///   - 验证逻辑检查必需标记是否已绑定
/// </summary>
[Serializable]
public class MarkerRequirement
{
    public string BindingKey;          // 绑定键名（如 "spawnArea", "spawnPoints"）
    public string MarkerTypeId;        // 需要的标记类型 ID（如 "Point", "Area", "Entity"）
    public string DisplayName;         // 显示名称（如 "刷怪区域"）
    public bool Required;              // 是否必需
    public bool AllowMultiple;         // 是否允许绑定多个标记
    public int MinCount;               // 最少数量（AllowMultiple 时有效）
    public string DefaultTag;          // 自动创建时的默认 Tag
}
```

### 3.6 MarkerTypeIds（标记类型 ID 常量）

```csharp
/// <summary>字符串常量，取代旧版 MarkerType 枚举。开放式扩展。</summary>
public static class MarkerTypeIds
{
    public const string Point = "Point";
    public const string Area = "Area";
    public const string Entity = "Entity";
    // 新增类型只需添加常量，无需修改已有代码
}
```

### 3.7 MarkerDefinition + MarkerDefinitionRegistry（扩展性核心）

```csharp
/// <summary>标记类型定义——描述一种标记“是什么、怎么创建、创建后怎么初始化”</summary>
public class MarkerDefinition
{
    public string TypeId;                  // 全局唯一 ID（对应 MarkerTypeIds）
    public string DisplayName;             // 编辑器显示名
    public string Description;             // 描述文本
    public Type ComponentType;             // 对应的 SceneMarker 子类类型
    public float DefaultSpacing = 2f;      // 自动创建时相邻标记间距
    public Action<SceneMarker> Initializer; // 创建后的初始化回调（可选）
}

/// <summary>标记定义提供者接口（自动发现）</summary>
[MarkerDef("Point")]  // 标注 Attribute 声明类型 ID
public class PointMarkerDef : IMarkerDefinitionProvider
{
    public MarkerDefinition Define() => new MarkerDefinition
    {
        TypeId = MarkerTypeIds.Point,
        DisplayName = "点标记",
        ComponentType = typeof(PointMarker),
        DefaultSpacing = 2f,
    };
}

/// <summary>注册表，自动扫描所有 [MarkerDef] 标注的 Provider</summary>
public static class MarkerDefinitionRegistry
{
    public static void AutoDiscover();           // 反射自动发现
    public static MarkerDefinition? Get(string typeId);
    public static IReadOnlyList<MarkerDefinition> GetAll();
}
```

**新增标记类型的操作（零接触已有逻辑文件）：**

```
1. Core/MarkerType.cs 加一个 const string         (可选，1行)
2. Runtime/Markers/PathMarker.cs                     (新文件，~30行)
3. Editor/Markers/Definitions/PathMarkerDef.cs       (新文件，~25行)
4. Editor/Markers/Renderers/PathMarkerRenderer.cs    (新文件，~80行)
   ━━ 完成，AutoDiscover 自动注册，无需修改任何已有文件 ━━
```

---

## 4. Scene View 交互设计

### 4.1 右键创建菜单

菜单按设计师意图组织（而非技术标记类型），由已注册的 ActionDefinition 自动生成：

```
在此位置创建行动...
├── ⚔️ 战斗
│   ├── 刷怪（区域 + 点位）         ← SpawnDef.SceneRequirements 驱动
│   ├── 放置预设怪                  ← PlacePresetDef（EntityMarker）
│   └── 伏击点                      ← AmbushDef（PointMarker）
├── 🎯 触发
│   ├── 进入触发区                  ← TriggerZoneDef（AreaMarker）
│   └── 交互触发                    ← InteractDef（PointMarker）
├── 🎬 演出
│   ├── 摄像机行为                  ← CameraActionDef（PointMarker × 2）
│   └── 播放特效                    ← PlayVFXDef（PointMarker）
├── 💡 环境
│   ├── 灯光变化                    ← LightingDef（AreaMarker）
│   └── 音频区域                    ← AudioZoneDef（AreaMarker）
├───────────────
└── 🏷️ 仅创建标记（不创建蓝图节点）
    ├── 点位标记
    ├── 区域标记
    └── 实体标记
```

菜单项来源：
- 遍历 `ActionRegistry` 中所有定义了 `SceneRequirements` 的 Action
- 按 Action 的 `Category` 字段分组（战斗 / 触发 / 演出 / 环境）
- 没有 `SceneRequirements` 的 Action（如 Delay、Branch）不出现在菜单中

### 4.2 多步创建流程

对于需要多个标记的 Action（如 Spawn = 区域 + 多个点位）：

```
步骤 1：选择菜单项"刷怪"
  → 进入区域绘制模式（鼠标变十字准星）
  → 工具栏提示："点击放置区域顶点，双击结束绘制"
  → 绘制完成 → AreaMarker 创建

步骤 2：自动进入点位放置模式
  → 工具栏提示："点击放置刷怪点（已放置 0 个），Esc 结束"
  → 每次点击创建一个 PointMarker
  → 点位自动限制在区域范围内（可选）
  → 按 Esc 或右键结束

步骤 3：自动完成
  → 蓝图编辑器中创建 Spawn 节点
  → 自动绑定 AreaMarker + 所有 PointMarker
  → 节点自动加入当前展开的子蓝图（如有）
  → Inspector 显示新节点属性
```

创建顺序由 `SceneRequirements` 中的定义顺序决定，Required 字段控制哪些步骤不可跳过。

### 4.3 Gizmo 绘制规则

```
PointMarker：
  - 实心圆球 + 方向箭头
  - 大小根据摄像机距离自适应
  - 颜色由图层决定（Tag 前缀映射）

AreaMarker（Polygon）：
  - 半透明填充 + 实线边框
  - 顶点显示为可拖拽小方块（编辑模式下）
  - 高度范围用虚线竖线表示

AreaMarker（Box）：
  - 半透明 Cube + 线框
  - 可通过 Handle 调整尺寸

EntityMarker：
  - Prefab 的线框预览（如果有 MeshFilter）
  - 否则显示为菱形图标 + Prefab 名称标签

所有标记：
  - 显示 MarkerName 文本标签（可在设置中关闭）
  - 被蓝图节点引用时标签旁显示 🔗 图标
  - 选中蓝图节点时，关联标记 Gizmo 加粗 + 脉冲动画
```

### 4.4 图层可见性控制

```
实现方式：Scene View Overlay 工具栏（Unity 2021.2+ SceneView.AddOverlayToActiveView）

┌──────────────────────────────────┐
│ 🏷️ 标记图层                      │
│  ☑ 🔴 战斗  ☑ 🔵 触发           │
│  ☑ 🟡 环境  ☐ 🟢 摄像机         │  ← 摄像机图层已隐藏
│  ☑ 🟣 叙事                       │
│  ─────────────────────────────── │
│  [全部显示] [全部隐藏]            │
└──────────────────────────────────┘

过滤逻辑：
  - 根据 SceneMarker.Tag 的第一级前缀匹配图层
  - 图层关闭时，该图层所有标记的 Gizmo 不绘制
  - 不影响标记 GameObject 的 activeInHierarchy（只是视觉隐藏）
```

---

## 5. 双向联动

### 5.1 蓝图 → 场景

| 操作 | 场景响应 |
|------|---------|
| 选中蓝图节点 | Scene View 高亮该节点绑定的所有标记（Gizmo 加粗 + 颜色加亮） |
| 双击蓝图节点 | Scene View 聚焦到标记的代表位置（Frame Selected） |
| 选中子蓝图 | Scene View 用虚线框圈出该子蓝图下所有标记的包围盒 |
| 悬停蓝图节点 | Scene View 对应标记轻微高亮（预览效果） |

### 5.2 场景 → 蓝图

| 操作 | 蓝图响应 |
|------|---------|
| 选中场景标记 | 蓝图编辑器高亮引用该标记的节点 + 自动滚动画布 |
| 双击场景标记 | 蓝图编辑器聚焦到对应节点 |
| 框选场景区域 | 蓝图编辑器高亮该区域内所有标记对应的节点 |

### 5.3 实现机制

```
核心：事件总线 / ScriptableObject 事件

  SceneMarkerSelectionBridge（ScriptableObject 单例）
    - OnBlueprintNodeSelected(nodeId, markerIds[])
    - OnSceneMarkerSelected(markerId)
    - OnRequestFrameMarker(markerId)
    - OnRequestFrameNode(nodeId)

  蓝图编辑器订阅场景侧事件，场景 Gizmo 绘制器订阅蓝图侧事件。
  用 SO 事件避免直接引用，保持 Editor/Runtime 分离。
```

---

## 6. 场景 Hierarchy 组织

```
场景中的标记对象按子蓝图自动分组：

SceneBlueprintMarkers/                    ← 根容器（自动创建/管理）
  ├── [走廊战斗]/                         ← 子蓝图分组（名称 = SubGraphFrame.Title）
  │   ├── TriggerZone_走廊入口             ← AreaMarker
  │   ├── SpawnArea_走廊中段               ← AreaMarker
  │   ├── SpawnPoint_01                    ← PointMarker
  │   ├── SpawnPoint_02                    ← PointMarker
  │   └── SpawnPoint_03                    ← PointMarker
  ├── [大厅Boss]/
  │   ├── BossSpawn                        ← EntityMarker
  │   ├── CameraRig_Boss入场               ← PointMarker
  │   └── LightingZone_Boss               ← AreaMarker
  └── [未分组]/                            ← 顶层节点的标记
      └── TriggerZone_关卡入口             ← AreaMarker

命名规则：
  - 容器名 = "SceneBlueprintMarkers"（固定）
  - 子蓝图分组名 = SubGraphFrame.Title（蓝图中修改名称时同步更新）
  - 标记名 = MarkerType + "_" + MarkerName
```

---

## 7. 与 Tag 系统的集成

### 7.1 自动 Tag 标注

创建标记时根据 Action 的 `MarkerRequirement.DefaultTag` 自动填充：

```
Action 类型          → 标记默认 Tag
Spawn               → Combat.SpawnArea / Combat.SpawnPoint
PlacePreset         → Combat.Entity
TriggerZone         → Trigger.OnEnter
CameraAction        → Camera.Position / Camera.LookAt
PlayVFX             → Environment.VFX
LightingChange      → Environment.Lighting
```

设计师可在 Inspector 中手动修改 Tag（如 `Combat.SpawnPoint` → `Combat.SpawnPoint.Elite`），实现更精细的分类。

### 7.2 Tag 过滤

在 Tag 系统（Phase 5）完成后，图层过滤可扩展为 Tag 条件过滤：
- 不仅按图层开关，还可按 Tag 表达式过滤（如 "只显示 `Combat.*.Elite`"）
- 蓝图编辑器中也可按 Tag 过滤节点高亮

---

## 8. 绑定系统与数据持久化

### 8.1 绑定架构（v0.3 优化后）

```
核心原则：
  - BindingContext 为编辑时唯一真相源（内存中的 GameObject 引用）
  - PropertyBag 中存储 MarkerId（稳定唯一标识，不怕改名）
  - SceneBlueprintManager 为场景持久化镜像

数据流：
  编辑时（Inspector 拖拽）：
    BindingContext.Set(key, GO)               ← 内存引用
    PropertyBag.Set(key, marker.MarkerId)     ← 稳定 ID

  创建时（Shift+右键）：
    创建标记 → 创建节点 → 自动绑定（同上）   ← v0.3 新增

  保存时：
    BindingContext → SceneBlueprintManager（持久化到场景）

  加载时：
    策略1: Manager.BoundObject → BindingContext（直接引用恢复）
    策略2: PropertyBag.MarkerId → FindMarkerInScene → BindingContext（回退查找）

  联动时：
    蓝图→场景: BindingContext.Get → GO → MarkerId → 高亮
    场景→蓝图: MarkerId → PropertyBag 匹配 → 选中节点
```

> **设计决策（v0.3）**：旧版 PropertyBag 中存储 `GameObject.name`，改名即断裂。现改为存储 `MarkerId`（GUID），彻底消除改名导致的绑定丢失问题。同时实现了创建标记后自动绑定到蓝图节点。

### 8.2 标记数据存储

```
场景标记数据存储在三个地方：

1. Scene 中的 GameObject + SceneMarker 组件
   - 随场景保存（.unity 文件）
   - 包含空间数据（Transform、Vertices、PrefabRef 等）
   - 包含 MarkerId（唯一标识，GUID 格式）

2. PropertyBag（节点属性）中的 MarkerId
   - 存储在 BlueprintAsset.GraphJson 中
   - 格式：{ "spawnArea": "a3f2c1d8-..." }
   - 仅存 ID，不存空间数据（单一数据源）

3. SceneBlueprintManager（场景 MonoBehaviour）
   - 持有 GameObject 直接引用（SceneBindingSlot.BoundObject）
   - 按子蓝图 ID 分组（SubGraphBindingGroup）
   - 由编辑器"同步到场景"功能自动维护
```

### 8.3 同步与验证

```
打开蓝图编辑器时：
  1. 加载 BlueprintAsset → 反序列化 Graph
  2. 从 SceneBlueprintManager 恢复 BindingContext（策略1）
  3. 对于未恢复的绑定，用 PropertyBag 中的 MarkerId 查找场景标记（策略2）
  4. MarkerBindingValidator 检查绑定一致性：
     - 类型匹配：marker.MarkerTypeId == req.MarkerTypeId
     - 缺失标记：MarkerId 引用的标记在场景中不存在 → ⚠️ 警告
     - 必需未绑定：Required 标记未绑定 → ❌ 错误

保存时：
  - 蓝图数据 → BlueprintAsset.GraphJson
  - 标记数据 → 随场景保存
  - BindingContext → SceneBlueprintManager（自动同步）
```

---

## 9. 目录结构

```
Assets/Extensions/SceneBlueprint/
  ├── Core/
  │   ├── SceneBlueprint.Core.asmdef             ← 纯 C#，无 Unity 引用
  │   ├── MarkerType.cs                          ← MarkerTypeIds 字符串常量类
  │   ├── MarkerRequirement.cs                   ← Action 场景需求声明（使用 string MarkerTypeId）
  │   ├── ActionDefinition.cs                    ← SceneRequirements 引用 MarkerRequirement
  │   └── ...
  │
  ├── Runtime/
  │   ├── SceneBlueprint.Runtime.asmdef
  │   ├── BlueprintAsset.cs
  │   ├── SceneBlueprintManager.cs               ← 场景持久化（自动管理）
  │   ├── SceneBindingSlot.cs                    ← 单条绑定数据
  │   ├── SubGraphBindingGroup.cs                ← 按子蓝图分组的绑定
  │   ├── Markers/
  │   │   ├── SceneMarker.cs                     ← 抽象基类（MarkerTypeId 字符串属性）
  │   │   ├── PointMarker.cs
  │   │   ├── AreaMarker.cs
  │   │   ├── EntityMarker.cs
  │   │   └── Annotations/                       ← 标记标注系统（v0.4 新增）
  │   │       ├── MarkerAnnotation.cs            ← 标注抽象基类
  │   │       ├── InitialBehavior.cs             ← 怪物初始行为枚举
  │   │       ├── SpawnAnnotation.cs             ← 刷怪标注（MonsterId/Level/Behavior/GuardRadius）
  │   │       ├── CameraAnnotation.cs            ← 摄像机标注（FOV/Transition/Easing）
  │   │       └── CameraEasing.cs               ← （定义在 CameraAnnotation.cs 内）
  │   └── ...
  │
  ├── Editor/
  │   ├── SceneBlueprintWindow.cs                ← 编辑器主窗口（含双向联动、自动绑定）
  │   ├── ActionNodeInspectorDrawer.cs           ← Inspector（SceneBinding 存 MarkerId）
  │   ├── ActionContentRenderer.cs               ← 画布摘要（MarkerId 截短显示）
  │   ├── BindingContext.cs                      ← 编辑时绑定上下文（唯一真相源）
  │   │
  │   ├── Logging/                               ← 日志系统（v0.2 新增）
  │   │   ├── SBLog.cs                           ← 核心日志 API
  │   │   ├── SBLogLevel.cs / SBLogEntry.cs
  │   │   ├── SBLogTags.cs                       ← 模块标签常量
  │   │   ├── SBLogBuffer.cs                     ← 环形缓冲
  │   │   ├── SBLogSettings.cs                   ← EditorPrefs 持久化设置
  │   │   └── SBLogWindow.cs                     ← 日志查看器 EditorWindow
  │   │
  │   ├── Markers/
  │   │   ├── Definitions/                       ← 标记定义系统（v0.2 新增）
  │   │   │   ├── MarkerDefinition.cs            ← 标记类型元数据
  │   │   │   ├── IMarkerDefinitionProvider.cs   ← 接口 + [MarkerDef] 属性
  │   │   │   ├── MarkerDefinitionRegistry.cs    ← AutoDiscover 注册表
  │   │   │   ├── PointMarkerDef.cs              ← 内置 Provider
  │   │   │   ├── AreaMarkerDef.cs
  │   │   │   └── EntityMarkerDef.cs
  │   │   │
  │   │   ├── Pipeline/                          ← Gizmo 绘制管线（v0.2 新增）
  │   │   │   ├── GizmoRenderPipeline.cs         ← 管线主循环 + 阶段调度
  │   │   │   ├── GizmoDrawContext.cs            ← 绘制上下文
  │   │   │   ├── IMarkerGizmoRenderer.cs        ← 渲染器接口 + DrawPhase
  │   │   │   ├── MarkerCache.cs                 ← 标记缓存
  │   │   │   ├── GizmoStyleConstants.cs         ← 颜色/尺寸常量
  │   │   │   └── GizmoLabelUtil.cs              ← 标签绘制工具
  │   │   │
  │   │   ├── Renderers/                         ← 标记渲染器（v0.2 新增）
  │   │   │   ├── PointMarkerRenderer.cs
  │   │   │   ├── AreaMarkerRenderer.cs
  │   │   │   └── EntityMarkerRenderer.cs
  │   │   │
  │   │   ├── Annotations/                       ← 标注定义注册表（v0.4 新增）
  │   │   │   ├── AnnotationDefinition.cs        ← 标注元数据
  │   │   │   ├── IAnnotationDefinitionProvider.cs ← 接口 + [AnnotationDef] 属性
  │   │   │   ├── AnnotationDefinitionRegistry.cs ← AutoDiscover 注册表
  │   │   │   └── Definitions/
  │   │   │       ├── SpawnAnnotationDef.cs      ← 刷怪标注定义
  │   │   │       └── CameraAnnotationDef.cs     ← 摄像机标注定义
  │   │   │
  │   │   ├── Tools/                             ← 标记编辑工具
  │   │   │   ├── AreaMarkerEditor.cs            ← AreaMarker Inspector（含位置生成 + 自动标注）
  │   │   │   └── PositionGenerator.cs           ← 位置生成算法
  │   │   │
  │   │   ├── MarkerGizmoDrawer.cs               ← 遗留兼容（仅保留 GetMarkerColor）
  │   │   ├── MarkerLayerSystem.cs               ← 图层系统
  │   │   ├── MarkerLayerOverlay.cs              ← Scene View 图层面板
  │   │   ├── MarkerHierarchyManager.cs          ← Hierarchy 自动分组
  │   │   ├── MarkerBindingValidator.cs          ← 绑定验证（使用 MarkerTypeId 字符串比较）
  │   │   ├── SceneViewMarkerTool.cs             ← 右键菜单（Registry 驱动，无 switch）
  │   │   └── SceneMarkerSelectionBridge.cs      ← 双向联动事件桥
  │   │
  │   ├── Export/
  │   │   ├── BlueprintExporter.cs               ← 导出器（合并 SO + Manager + Annotation 后处理）
  │   │   ├── AnnotationExportHelper.cs          ← Annotation 导出辅助（v0.4 新增）
  │   │   └── BlueprintSerializer.cs
  │   └── ...
  │
  ├── Actions/                                   ← 各 Action 使用 MarkerTypeIds.xxx
  │   ├── Combat/SpawnActionDef.cs
  │   ├── Combat/PlacePresetActionDef.cs
  │   └── ...
  └── ...
```

---

## 10. 实施路线

### Phase 4B-2：标记体系 + Gizmo + 绑定 ✅（2026-02-14 完成）

```
已完成步骤：
  ✅ M1. SceneMarker 组件体系：基类 + PointMarker + AreaMarker + EntityMarker
  ✅ M2. MarkerRequirement + MarkerTypeIds 字符串常量 + ActionDefinition.SceneRequirements
  ✅ M3. Gizmo 绘制管线：GizmoRenderPipeline + 3 个 Renderer（分阶段、缓存、视锥裁剪）
  ✅ M4. SceneViewMarkerTool：Shift+右键菜单 → Registry 驱动创建标记 + 蓝图节点
  ✅ M5. MarkerHierarchyManager：场景 Hierarchy 自动分组管理
  ✅ M6. 图层系统：MarkerLayerSystem + MarkerLayerOverlay（Tag 前缀映射）
  ✅ M7. 双向联动：SceneMarkerSelectionBridge（选中/高亮/聚焦/双击）
  ✅ M8. AreaMarkerRenderer.DrawInteractive：Box Handle + 多边形顶点拖拽
  ✅ M9. MarkerBindingValidator：绑定一致性验证（类型匹配、缺失检测）
  ✅ M10. MarkerDefinition + IMarkerDefinitionProvider + MarkerDefinitionRegistry（自动发现）
  ✅ M11. 绑定优化：PropertyBag 存 MarkerId、自动绑定、MarkerId 恢复
  ✅ M12. SBLog 日志系统：分级日志 + 模块标签 + 环形缓冲 + 专用 EditorWindow
```

### Phase 5 已完成（Tag 深度集成）

```
已完成步骤：
  ✅ M13. Tag 条件过滤：不仅按图层开关，还可按 Tag 表达式过滤
  ✅ M14. 蓝图编辑器中按 Tag 过滤节点高亮

已废弃步骤：
  ❌ M15. 多步创建流程：区域绘制 → 点位放置（按 SceneRequirements 顺序引导）
```

### Phase 6+（遭遇模板）已废弃

```
已废弃步骤：
  ❌ M16. 遭遇模板资产：EncounterTemplate (SO) 存储标记布局 + 子蓝图模板
  ❌ M17. 模板库面板 + 拖拽实例化
  ❌ M18. 空间热力图：事件密度可视化叠加层
```

### 标记标注系统 ✅（2026-02-17 完成）

> 详细设计见 [标记标注系统设计](标记标注系统设计.md)

```
已完成步骤（Phase 1~6，共 21 步）：
  ✅ 基础框架：MarkerAnnotation 抽象基类 + SpawnAnnotation + InitialBehavior 枚举
  ✅ 注册表：AnnotationDefinitionRegistry（AutoDiscover，复用 MarkerDefinitionRegistry 模式）
  ✅ Gizmo 集成：MarkerCache 缓存 Annotation + Decoration 阶段 + 颜色覆盖优先级
  ✅ 位置生成工具：AreaMarkerEditor 自动添加标注选项（从 Registry 动态获取）
  ✅ 导出集成：SceneBindingEntry.Annotations + AnnotationExportHelper + AreaMarker 展开子点位
  ✅ 扩展验证：CameraAnnotation（FOV/过渡/缓动 + 视锥线框 Gizmo）

新增文件清单：
  Runtime/Markers/Annotations/MarkerAnnotation.cs      ← 标注抽象基类
  Runtime/Markers/Annotations/InitialBehavior.cs       ← 怪物初始行为枚举
  Runtime/Markers/Annotations/SpawnAnnotation.cs       ← 刷怪标注
  Runtime/Markers/Annotations/CameraAnnotation.cs      ← 摄像机标注
  Editor/Markers/Annotations/AnnotationDefinition.cs   ← 标注元数据
  Editor/Markers/Annotations/IAnnotationDefinitionProvider.cs ← 接口 + Attribute
  Editor/Markers/Annotations/AnnotationDefinitionRegistry.cs  ← AutoDiscover 注册表
  Editor/Markers/Annotations/Definitions/SpawnAnnotationDef.cs
  Editor/Markers/Annotations/Definitions/CameraAnnotationDef.cs
  Editor/Export/AnnotationExportHelper.cs               ← Annotation 导出辅助

修改文件清单：
  Editor/Markers/Pipeline/MarkerCache.cs               ← 新增 _annotationCache + GetAnnotations()
  Editor/Markers/Pipeline/GizmoStyleConstants.cs       ← 颜色覆盖优先级
  Editor/Markers/Pipeline/GizmoRenderPipeline.cs       ← 新增 ExecuteDecorationPhase
  Editor/Markers/Tools/AreaMarkerEditor.cs             ← 自动添加标注选项
  Editor/Export/BlueprintExporter.cs                   ← EnrichBindingsWithAnnotations 后处理
  Core/Export/SceneBlueprintData.cs                    ← AnnotationDataEntry
  Actions/Spawn/SpawnPresetDef.cs                      ← 双绑定槽
```

---

## 11. 已确认决策

| # | 问题 | 决策 |
|---|------|------|
| D1 | AreaMarker 的区域编辑方式 | **两者都支持**：Box Handle + 多边形顶点拖拽，通过 AreaShape 枚举切换 |
| D2 | 标记删除时的绑定处理 | **保留引用+标警告**：MarkerBindingValidator 检测缺失标记并报 Warning |
| D3 | 标记类型扩展机制 | **方案B**：MarkerTypeIds 字符串 + MarkerDefinition + AutoDiscover Registry |
| D4 | PropertyBag 绑定存储格式 | **存 MarkerId**（GUID），不存 GameObject.name（v0.3 优化） |
| D5 | 创建标记后是否自动绑定 | **自动绑定**：OnMarkerCreated 回调中写入 BindingContext + PropertyBag |

## 12. 开放问题

| 问题 | 状态 | 备选方案 |
|------|------|---------|
| 多个蓝图共享同一场景的标记？ | 待定 | 当前一个关卡=一张图，暂不考虑 |
| EntityMarker 运行时是否实例化 Prefab？ | 待定 | 取决于运行时 Handler 的实现方式 |
