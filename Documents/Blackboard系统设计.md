# Blackboard 系统设计

> **doc_status**: active  
> **created**: 2026-02-19  
> **last_reviewed**: 2026-02-19  
> **关联文档**: [SceneBlueprint核心设计原则](SceneBlueprint核心设计原则.md)、[缺失功能盘点](缺失功能盘点.md)、[节点激活语义与汇聚设计](节点激活语义与汇聚设计.md)

---

## 一、背景与问题

### 1.1 现状分析

`Blackboard.cs` 是挂在 `BlueprintFrame` 上的 `Dictionary<string, object>`，当前只作为**框架内部隐式数据管道**使用，策划在蓝图图层完全感知不到：

| 写入方 | 写入 Key 规范 | 内容 |
|---|---|---|
| `SpawnWaveSystem` | `{actionId}.waveIndex` | 当前波次序号 |
| `SpawnWaveSystem` | `{actionId}.waveCount` | 总波次数 |
| `SpawnWaveSystem` | `{actionId}.monsterFilter` | 当前波次筛选条件 |
| `TransitionSystem` | `_activatedBy.{targetId}` | 上游激活来源节点 ID |
| `FlowFilterSystem` | 读取上述数据，做 pass/reject 判断 | — |

### 1.2 问题清单

- **不透明**：策划看不到数据在哪里流动，图里的逻辑难以理解
- **易出错**：Key 是裸字符串，拼错了运行时才报错，没有编辑期校验
- **无类型约束**：存的是 `object`，读取时类型靠"约定"而非声明
- **无法扩展**：策划无法在图里自己写入或读取黑板变量，不能表达"有状态"逻辑（记录阶段、触发次数等）

---

## 二、设计目标

1. **策划可见**：蓝图中的所有变量在编辑器变量面板里一目了然
2. **声明强类型**：变量在声明时确定类型，节点读写时类型匹配
3. **Key 不拼写**：节点配置选变量引用（整型索引），而非手动输入字符串
4. **Local / Global 分层**：变量有明确的生命周期语义
5. **框架内部隐式数据与策划变量完全分层**：`_` 前缀内部元数据走独立字符串路径，与策划声明变量互不干扰

---

## 三、参考依据

| 框架 | 关键设计 | 吸收点 |
|---|---|---|
| **Unreal Engine** | 独立 BlackboardAsset；Key 用整型 Handle（索引）而非字符串；有 Observer 回调 | 索引 Key，消灭字符串拼写错误 |
| **BehaviorDesigner** | `SharedInt`/`SharedFloat` 泛型包装；节点字段可切换"固定值"或"黑板引用" | 泛型 SharedVariable 概念，编辑器里下拉选变量 |
| **Unity Visual Scripting** | Variables 分五层（Graph/Object/Scene/Application/Saved）| 多层作用域思路，简化为 Local + Global |

---

## 四、核心概念：变量声明

### 4.1 VariableDeclaration 数据结构

当前 `VariableEntry`（只有 Key/ValueType/InitialValue）扩展为 `VariableDeclaration`：

```csharp
[Serializable]
public class VariableDeclaration
{
    public int Index = -1;           // 唯一整型索引，运行时 O(1) 查找
    public string Name = "";         // 策划可读名称，如 "currentWave"
    public VariableType Type;        // 枚举：Int / Float / Bool / String
    public VariableScope Scope;      // 枚举：Local / Global
    public string InitialValue = ""; // 初始值（字符串，运行时 Parse）
}

public enum VariableType  { Int, Float, Bool, String }
public enum VariableScope { Local, Global }
```

### 4.2 蓝图导出格式变更

`SceneBlueprintData` 中 `BlackboardInit` 更名为 `Variables`：

```json
"Variables": [
  { "Index": 0, "Name": "currentWave",  "Type": "Int",   "Scope": "Local",  "InitialValue": "0" },
  { "Index": 1, "Name": "hasTriggered", "Type": "Bool",  "Scope": "Local",  "InitialValue": "false" },
  { "Index": 2, "Name": "difficulty",   "Type": "Float", "Scope": "Global", "InitialValue": "1.0" }
]
```

> `BlackboardInit` 字段直接废弃，统一使用 `Variables`。

---

## 五、作用域设计：Local 与 Global

### 5.1 Local（蓝图实例级）

- 存储位置：`BlueprintFrame.Blackboard`（现有）
- 生命周期：蓝图实例从 `Start` 到 `End`，结束时随 Frame 一起销毁
- 典型变量：当前波次、本局战斗计数、阶段状态

### 5.2 Global（游戏会话级）

- 存储位置：`GlobalBlackboard`（新增静态类）
- 生命周期：游戏会话期间持久存在，场景卸载时**不**自动清空
- 清空时机：由调用方在合适节点（如游戏返回主菜单）主动调用 `GlobalBlackboard.Clear()`
- 典型变量：全局难度系数、玩家解锁状态、跨关卡进度标记

> **当前阶段不做持久化（存档）**，GlobalBlackboard 是内存级的，应用退出后不保存。

### 5.3 初始化流程

```
BlueprintLoader.BuildFrame()
    ↓ 遍历 data.Variables
    ├── scope = Local  → frame.Blackboard.Set(index, parsedValue)
    └── scope = Global → GlobalBlackboard.SetIfAbsent(index, parsedValue)
                                        ↑ 如果已有值则跳过（全局变量不重复初始化）
```

`SetIfAbsent` 语义很重要：多个蓝图实例共享同一个 Global 变量时，只有第一次加载时写入初始值，后续加载不覆盖。

---

## 六、运行时架构

### 6.1 Blackboard.cs 扩展（Local）

**重写** `Blackboard.cs`，两条路径职责明确、互不混淆：

```csharp
public class Blackboard
{
    // 策划声明变量（整型索引，O(1) 访问，类型安全）
    private readonly Dictionary<int, object>    _declared = new();

    // 框架内部元数据（字符串 Key，_前缀约定，策划不可见）
    private readonly Dictionary<string, object> _internal = new();

    // 策划变量 API
    public void   Set<T>(int index, T value)       => _declared[index] = value;
    public T?     Get<T>(int index)                { ... }
    public bool   TryGet<T>(int index, out T? val) { ... }
    public bool   Has(int index)                   => _declared.ContainsKey(index);

    // 内部元数据 API（仅 System 内部调用，key 必须以 _ 开头）
    internal void   SetInternal(string key, object value) => _internal[key] = value;
    internal T?     GetInternal<T>(string key)            { ... }
    internal bool   TryGetInternal<T>(string key, out T? val) { ... }
}
```

### 6.2 GlobalBlackboard.cs（新增）

```csharp
/// <summary>
/// 游戏会话级全局黑板。
/// 生命周期：应用运行期间，不持久化到磁盘。
/// 策划变量按 VariableDeclaration.Index 访问；
/// 跨蓝图通信时 key 由调用方约定。
/// </summary>
public static class GlobalBlackboard
{
    private static readonly Dictionary<int, object>    _byIndex = new();
    private static readonly Dictionary<string, object> _byKey   = new();

    public static void   Set<T>(int index, T value)        => _byIndex[index] = value;
    public static T?     Get<T>(int index)                 { ... }
    public static bool   Has(int index)                    => _byIndex.ContainsKey(index);

    /// <summary>仅在 Key 不存在时写入（用于 Global 变量初始化）</summary>
    public static void   SetIfAbsent<T>(int index, T value)
    {
        if (!_byIndex.ContainsKey(index)) _byIndex[index] = value;
    }

    /// <summary>游戏返回主菜单/会话结束时调用</summary>
    public static void   Clear() { _byIndex.Clear(); _byKey.Clear(); }
}
```

### 6.3 内部元数据命名约定

框架内部（System 之间传递的隐式数据）继续使用字符串 Key，但**强制加 `_` 前缀**，与策划声明变量完全隔离：

| 规范 | 示例 | 读写方 |
|---|---|---|
| `_activatedBy.{nodeId}` | `_activatedBy.node_001` | TransitionSystem 写，FlowFilterSystem 读 |
| `_waveState.{nodeId}` | `_waveState.node_002` | SpawnWaveSystem 内部 |

策划变量面板**不显示**任何内部元数据，调用 `internal` 方法在编译期就限制了访问范围。

---

## 七、节点设计

### 7.1 Blackboard.Set

```
属性：
  变量（Variable）: [下拉，从当前蓝图声明变量中选] → 自动显示作用域和类型
  值（Value）:      [根据变量类型显示对应输入控件] 或 [从上游数据端口连线]

端口：
  in  → Flow（控制流输入）
  out → Flow（控制流输出，Set 完成后触发）
```

**写入逻辑**（运行时）：
- 根据声明的 `Scope` 决定写入 `frame.Blackboard`（Local）还是 `GlobalBlackboard`（Global）
- 根据声明的 `Index` 用整型 Key 写入，O(1) 访问

### 7.2 Blackboard.Get

```
属性：
  变量（Variable）: [下拉，从当前蓝图声明变量中选]

端口：
  in    → Flow（控制流输入）
  out   → Flow（控制流输出）
  value → Data（数据输出端口，类型由声明的 VariableType 决定）
```

> **数据端口类型问题**：当前数据端口系统用字符串标识类型（`DataTypes.Int` 等），`Blackboard.Get` 的输出端口类型在声明变量时确定，`ActionDefinition` 构建时动态生成对应类型的数据端口。

### 7.3 FlowFilter 的升级

`Flow.Filter` 现有的 `key` 属性是手动输入字符串（隐式查找 `{sourceId}.{key}`），未来支持两种模式：

| 模式 | key 来源 | 适用场景 |
|---|---|---|
| **隐式模式**（现有）| 字符串，自动推断上游节点前缀 | 读取 SpawnWave 输出的内置数据 |
| **显式模式**（新增）| 下拉选择声明变量 | 读取策划自定义的 Blackboard 变量 |

新设计中 `Flow.Filter` 统一使用**显式模式**（下拉选声明变量）。读取 SpawnWave 内置数据的场景，改为 SpawnWave 节点直接通过数据端口输出，不再走隐式黑板路径。

---

## 八、编辑器变量面板

### 8.1 位置

挂在蓝图图编辑器的侧边栏或底部，独立于节点 Inspector 存在（参考 UE 的 Blackboard 面板）。

### 8.2 UI 布局

```
┌──────────────────────────────────────────────┐
│  蓝图变量（Variables）                  [+添加] │
├────┬───────────────┬───────┬──────────┬──────┤
│ #  │ 变量名         │ 类型  │ 作用域    │ 初始值│
├────┼───────────────┼───────┼──────────┼──────┤
│ 0  │ currentWave   │ int   │ 🟡 Local │ 0    │
│ 1  │ hasTriggered  │ bool  │ 🟡 Local │false │
│ 2  │ difficulty    │ float │ 🔵 Global│ 1.0  │
└────┴───────────────┴───────┴──────────┴──────┘
```

- **#（Index）**：自动分配，只读，编辑器内部用于关联节点引用
- **删除变量**：检查图中是否有节点引用该变量，有则弹出确认提示
- **重命名变量**：所有引用该变量的节点自动更新（按 Index 关联，不是字符串）

---

## 九、改动范围与实施步骤

### Phase 1：数据层（无编辑器 UI）

| 步骤 | 文件 | 改动描述 |
|---|---|---|
| 1 | `SceneBlueprintData.cs` | `VariableEntry` 新增 `Index`、`Scope` 字段；`BlackboardInit` 重命名为 `Variables` |
| 2 | `GlobalBlackboard.cs` | 新增静态类 |
| 3 | `Blackboard.cs` | 新增整型索引访问路径（保留字符串路径兼容） |
| 4 | `BlueprintLoader.cs` | 初始化时按 Scope 分流写入 Local / Global |

### Phase 2：节点层

| 步骤 | 文件 | 改动描述 |
|---|---|---|
| 5 | `BlackboardSetDef.cs` | 新增节点定义（暂用字符串属性，等编辑器变量面板完成后升级为下拉）|
| 6 | `BlackboardGetDef.cs` | 新增节点定义（同上）|
| 7 | `BlackboardSetSystem.cs` | 运行时 Set 逻辑（按 Scope 路由）|
| 8 | `BlackboardGetSystem.cs` | 运行时 Get 逻辑（按 Scope 路由）|

### Phase 3：编辑器层

| 步骤 | 文件 | 改动描述 |
|---|---|---|
| 9 | 变量面板 UI | 蓝图编辑器侧边栏新增变量声明列表（Inspector 复用 SerializedObject）|
| 10 | 节点属性升级 | `Blackboard.Set/Get` 的变量属性从字符串输入改为下拉选择 |
| 11 | `FlowFilter` 升级 | key 属性新增 Explicit 模式，支持下拉选变量 |

> **当前建议先实现 Phase 1 + Phase 2**，Phase 3 的编辑器 UI 可以在功能验证后再做。

---

## 十、典型使用示例

### 示例 1：记录并判断阶段

```
声明变量：phase (int, Local, 0)

[Flow.Start]
    → [Blackboard.Set: phase = 1]
    → [Spawn.Wave: area_01]
        onComplete → [Blackboard.Set: phase = 2]
                  → [Flow.Filter: phase >= 2]
                        pass → [Spawn.Wave: area_boss]
```

### 示例 2：全局难度影响刷怪

```
声明变量：difficulty (float, Global, 1.0)
（由游戏主菜单在开始游戏时写入 GlobalBlackboard）

[Flow.Start]
    → [Blackboard.Get: difficulty] ── float ──→ [Spawn.Wave: area_01]
                                                    （波次节点读取难度系数动态调整怪物数量）
```

### 示例 3：触发次数计数（依赖 Flow.Loop，Phase 2+）

```
声明变量：triggerCount (int, Local, 0)

[Trigger.EnterArea] → [Blackboard.Set: triggerCount = triggerCount + 1]
                   → [Flow.Filter: triggerCount >= 3]
                         pass → [Spawn.Wave: boss_wave]
```

---

## 十一、未解决问题（待定）

| 问题 | 描述 | 建议 |
|---|---|---|
| **Blackboard.Get 的数据端口类型** | 当前 ActionDefinition.Port() 是静态构建，Get 节点的输出端口类型依赖运行时声明的变量 | 考虑在编辑器序列化时将变量类型写入节点属性，运行时按属性值动态构建端口 |
| **GlobalBlackboard 的 Clear 时机** | 场景卸载是否自动清空？由谁负责调用？ | 建议由 `BlueprintRuntimeManager` 在销毁时判断是否清空（可加配置项）|
| **变量表达式计算** | `triggerCount + 1` 这类算术需要 `Math.Add` 节点或内置表达式支持 | Phase 3+ 实现 `Math.*` 节点组 |
