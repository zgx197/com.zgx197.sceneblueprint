# Blackboard 数据通道与 Flow.Filter 条件过滤设计

> **文档版本**：v1.0  
> **创建日期**：2026-02-19  
> **最后更新**：2026-02-19  
> **状态**：📝 设计中  
> **重要性**：🟡 子系统扩展  
> **关联**：[SceneBlueprint核心设计原则](SceneBlueprint核心设计原则.md)、[波次刷怪系统重构设计](波次刷怪系统重构设计.md)  
> **doc_status**: draft  
> **last_reviewed**: 2026-02-19

---

## 目录

- [一、问题背景](#一问题背景)
- [二、设计目标](#二设计目标)
- [三、核心设计：Blackboard 数据通道](#三核心设计blackboard-数据通道)
- [四、Flow.Filter 条件过滤节点](#四flowfilter-条件过滤节点)
- [五、自动推断来源节点机制](#五自动推断来源节点机制)
- [六、SpawnWaveSystem 改造](#六spawnwavesystem-改造)
- [七、Blueprint 组合示例](#七blueprint-组合示例)
- [八、复杂条件的组合策略](#八复杂条件的组合策略)
- [九、影响范围与实施路线](#九影响范围与实施路线)
- [十、设计决策记录](#十设计决策记录)

---

## 一、问题背景

### 1.1 现状

当前 `Spawn.Wave` 节点有一个 `onWaveStart` 事件端口，每波开始时触发。但存在两个问题：

1. **数据不传递**：`EmitWaveStartEvent` 只发射 PortEvent（控制信号），没有把 `waveIndex` 等数据写入任何可读取的位置
2. **无条件过滤**：下游节点无法判断"这是第几波"，所以无法实现"只在第 5 波触发镜头震动"

### 1.2 更一般的问题

这不仅是波次刷怪的问题。任何 ActionNode 在执行过程中触发事件端口时，都可能需要携带数据，下游需要根据数据做条件判断：

| 场景 | 事件端口 | 携带数据 | 下游条件 |
|------|---------|---------|---------|
| 波次刷怪 | onWaveStart | waveIndex, monsterFilter | waveIndex == 4 |
| 对话系统 | onChoice | choiceId | choiceId == "accept" |
| Boss 战 | onPhaseChange | phase | phase >= 2 |
| 计时器 | onTick | tickCount | tickCount % 10 == 0 |

### 1.3 业界方案参考

| 方案 | 代表框架 | 特点 |
|------|---------|------|
| 执行流 + 数据流双线 | UE Blueprint | 灵活但复杂，蓝图容易变面条 |
| Blackboard 全局变量 | 行为树（BT） | 简单直观，但数据流不可见 |
| Signal + Payload | Godot Signal | 数据跟事件走，连接配置复杂 |

**我们的选择**：Blackboard + 轻量条件节点。理由：
- 已有 `frame.Blackboard` 基础设施
- 用户是策划，不适合数据连线的复杂度
- 场景复杂度有限，不需要通用编程能力

---

## 二、设计目标

### 2.1 功能目标

1. 上游节点能在触发事件端口时发布数据到 Blackboard
2. 下游节点能从 Blackboard 读取数据并做条件判断
3. 策划能在蓝图中实现"第 5 波 Boss 登场时触发镜头震动"
4. 机制通用，不仅限于波次刷怪

### 2.2 设计原则

1. **最小侵入**：不改 PortEvent 结构，不引入数据连线
2. **策划友好**：条件配置用下拉选择 + 输入框，不写表达式
3. **原子节点**：Filter 只做单条件判断，复杂逻辑通过蓝图拓扑组合
4. **命名安全**：Blackboard 变量用前缀避免冲突

---

## 三、核心设计：Blackboard 数据通道

### 3.1 写入约定

上游节点在触发事件端口前，往 `frame.Blackboard` 写入约定变量。

**命名规范**：`{actionId}.{variableName}`

```
示例：
  actionId = "node_wave"
  
  写入：
    frame.Blackboard.Set("node_wave.waveIndex", 4);       // 当前波次索引（0-based）
    frame.Blackboard.Set("node_wave.waveCount", 5);        // 总波次数
    frame.Blackboard.Set("node_wave.monsterFilter", "Boss"); // 当前波次的怪物筛选标签
```

**为什么用 `actionId` 前缀**：
- 同一蓝图中可能有多个 `Spawn.Wave` 节点（如左路刷怪、右路刷怪）
- 前缀确保变量不冲突
- 下游 Filter 通过"来源节点 ID"定位变量

### 3.2 生命周期

- **写入时机**：事件端口触发前（如 `EmitWaveStartEvent` 之前）
- **覆盖策略**：每次触发都覆盖（同一个 key 的值会被最新值替换）
- **清理策略**：Phase 1 不主动清理，Blackboard 变量在蓝图生命周期内持续存在

### 3.3 数据类型

Blackboard 存储 `object`，但 Flow.Filter 比较时统一转为字符串或数字：

| 写入类型 | 比较方式 |
|---------|---------|
| int | 数字比较（==, !=, >, <, >=, <=） |
| float | 数字比较 |
| string | 字符串比较（==, != 有效；>, < 按字典序，但不推荐） |
| bool | 转为 "true"/"false" 字符串比较 |

---

## 四、Flow.Filter 条件过滤节点

### 4.1 节点定义

```
TypeId:       "Flow.Filter"
DisplayName:  "条件过滤"
Category:     "Flow"
Duration:     Instant（瞬时型——立即判断并路由）
ThemeColor:   (0.9, 0.7, 0.2) 黄色——与 Flow.Branch 同色系，表示"决策"

端口：
  in     — 输入（被上游事件端口激活）
  pass   — 条件满足时触发
  reject — 条件不满足时触发（可选，不连则丢弃）

属性：
  key    — 变量名（String，如 "waveIndex"）
  op     — 操作符（Enum：==, !=, >, <, >=, <=）
  value  — 目标值（String，运行时按需转为数字）
```

### 4.2 属性说明

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| key | String | "" | Blackboard 变量名（不含前缀，如 "waveIndex"） |
| op | Enum | "==" | 比较操作符 |
| value | String | "" | 目标值（如 "4"） |

**注意**：`key` 只填变量名（如 `waveIndex`），不填完整的 Blackboard key（如 `node_wave.waveIndex`）。前缀由运行时自动推断（见第五章）。

### 4.3 运行时逻辑（FlowFilterSystem）

```
FlowFilterSystem.Order = 15  （在 FlowSystem(10) 之后，业务 System 之前）

处理 Flow.Filter 节点：
  1. 检查 Phase == Running
  2. 确定来源节点 ID（自动推断，见第五章）
  3. 拼接 Blackboard key = "{sourceActionId}.{key}"
  4. 从 Blackboard 读取值
  5. 与 value 做 op 比较
  6. 满足 → 标记 Completed，TransitionSystem 走 pass 出边
     不满足 → 标记 Completed，TransitionSystem 走 reject 出边
  7. 通过 CustomInt 标记走哪个端口：
     CustomInt = 1 → pass（默认，TransitionSystem 正常传播 out 边）
     CustomInt = 2 → reject
```

### 4.4 端口路由机制

这里有一个关键设计问题：TransitionSystem 当前的逻辑是"Completed 后传播所有出边"。但 Flow.Filter 需要根据条件结果只走 `pass` 或 `reject` 其中一个。

**解决方案**：Flow.Filter 不依赖 TransitionSystem 的自动传播，而是自己在 System 中手动发射 PortEvent 到正确的端口，然后标记 `CustomInt = 1`（已传播）防止 TransitionSystem 重复处理。

```
FlowFilterSystem 伪代码：

if (conditionMet)
{
    // 手动发射 pass 端口的出边事件
    EmitPortEvents(frame, actionIndex, "pass");
}
else
{
    // 手动发射 reject 端口的出边事件
    EmitPortEvents(frame, actionIndex, "reject");
}

state.Phase = ActionPhase.Completed;
state.CustomInt = 1; // 标记已传播，TransitionSystem 不再重复处理
```

这个模式和 `SpawnWaveSystem.EmitWaveStartEvent` 一致——业务 System 自己控制哪些端口触发。

### 4.5 比较逻辑

```csharp
private static bool EvaluateCondition(object? bbValue, string op, string targetValue)
{
    if (bbValue == null) return op == "!="; // null != 任何值 为 true

    string bbStr = bbValue.ToString() ?? "";

    // 尝试数字比较
    if (double.TryParse(bbStr, out double bbNum) && double.TryParse(targetValue, out double targetNum))
    {
        return op switch
        {
            "==" => Math.Abs(bbNum - targetNum) < 0.0001,
            "!=" => Math.Abs(bbNum - targetNum) >= 0.0001,
            ">"  => bbNum > targetNum,
            "<"  => bbNum < targetNum,
            ">=" => bbNum >= targetNum,
            "<=" => bbNum <= targetNum,
            _    => false
        };
    }

    // 回退到字符串比较
    return op switch
    {
        "==" => bbStr == targetValue,
        "!=" => bbStr != targetValue,
        _    => false // 字符串不支持 >, <, >=, <=
    };
}
```

---

## 五、自动推断来源节点机制

### 5.1 问题

Flow.Filter 需要知道"从哪个节点的 Blackboard 变量中读取数据"。如果让策划手动填写来源节点 ID，体验不好。

### 5.2 方案：运行时从入边反查

Flow.Filter 被激活时，TransitionSystem 发送的 PortEvent 中包含 `FromActionIndex`。我们可以在激活时把来源节点的 ActionId 记录下来。

**实现方式**：在 TransitionSystem 激活目标节点时，把来源 ActionId 写入 Blackboard：

```
Blackboard key: "_activatedBy.{targetActionId}"
Value: sourceActionId
```

例如：
```
[Spawn.Wave(id=node_wave)] ─onWaveStart→ [Flow.Filter(id=node_filter)]

TransitionSystem 激活 node_filter 时写入：
  frame.Blackboard.Set("_activatedBy.node_filter", "node_wave");
```

FlowFilterSystem 读取时：
```
string sourceId = frame.Blackboard.Get<string>($"_activatedBy.{myActionId}");
// sourceId = "node_wave"
string bbKey = $"{sourceId}.{key}";
// bbKey = "node_wave.waveIndex"
object? value = frame.Blackboard.Get<object>(bbKey);
```

### 5.3 回退机制

如果自动推断失败（比如 `_activatedBy` 不存在），Flow.Filter 直接用 `key` 作为 Blackboard key（不加前缀）。这允许策划手动写完整的 key 作为兜底。

### 5.4 对 TransitionSystem 的改动

在 TransitionSystem 激活目标节点的代码中，增加一行 Blackboard 写入：

```csharp
// 普通节点：OR 语义，直接激活
if (targetState.Phase == ActionPhase.Idle)
{
    targetState.Phase = ActionPhase.Running;
    targetState.TicksInPhase = 0;

    // 记录激活来源（供 Flow.Filter 等节点自动推断数据来源）
    var sourceActionId = frame.Actions[evt.FromActionIndex].Id;
    var targetActionId = frame.Actions[evt.ToActionIndex].Id;
    frame.Blackboard.Set($"_activatedBy.{targetActionId}", sourceActionId);
}
```

这是一个通用机制——所有节点都能通过 `_activatedBy.{myId}` 知道自己是被谁激活的。

---

## 六、SpawnWaveSystem 改造

### 6.1 当前问题

`EmitWaveStartEvent` 只发射 PortEvent，没有写入 Blackboard 数据。

### 6.2 改造内容

在 `EmitWaveStartEvent` 调用前，写入波次相关数据：

```csharp
// 在 ProcessWaveAction 中，触发 onWaveStart 前写入 Blackboard
var actionId = frame.Actions[actionIndex].Id;
frame.Blackboard.Set($"{actionId}.waveIndex", currentWave);           // 当前波次索引（0-based）
frame.Blackboard.Set($"{actionId}.waveCount", waveEntries.Length);    // 总波次数
frame.Blackboard.Set($"{actionId}.monsterFilter", currentEntry.monsterFilter); // 当前波次筛选标签

// 触发 onWaveStart 端口事件
EmitWaveStartEvent(frame, actionIndex, currentWave);
```

### 6.3 可用变量列表

| 变量名 | 类型 | 说明 | 示例值 |
|--------|------|------|--------|
| waveIndex | int | 当前波次索引（0-based） | 0, 1, 2, 3, 4 |
| waveCount | int | 总波次数 | 5 |
| monsterFilter | string | 当前波次的怪物筛选标签 | "Normal", "Boss" |

策划在 Flow.Filter 中只需要填 `key: waveIndex`，运行时自动拼接为 `node_wave.waveIndex`。

---

## 七、Blueprint 组合示例

### 7.1 第 5 波 Boss 登场时镜头震动

```
[Flow.Start]
    ↓ out
[Trigger.EnterArea]
    ↓ out
[Spawn.Wave] ──onWaveStart──→ [Flow.Filter] ──pass──→ [VFX.CameraShake]
  (id: node_wave)               key: waveIndex            intensity: 3.0
  waves:                        op: ==                    duration: 1.0
    波次1: 5个, Normal           value: 4
    波次2: 5个, Normal
    波次3: 3个, Elite
    波次4: 5个, Normal
    波次5: 1个, Boss
    ↓ out
[Flow.End]
```

**执行流程**：
1. 玩家进入区域 → Trigger.EnterArea Completed → Spawn.Wave 激活
2. 波次 1 开始 → Blackboard 写入 `node_wave.waveIndex = 0` → onWaveStart → Filter 判断 0 == 4 → false → reject（无连接，丢弃）
3. 波次 2~4 同理，Filter 都走 reject
4. 波次 5 开始 → Blackboard 写入 `node_wave.waveIndex = 4` → onWaveStart → Filter 判断 4 == 4 → true → pass → CameraShake 激活
5. 所有波次完成 → Spawn.Wave Completed → Flow.End

### 7.2 第 3 波和第 5 波都触发效果（OR 组合）

```
                         ┌─[Filter: waveIndex == 2]─pass─┐
[Spawn.Wave]─onWaveStart─┤                                ├→ [VFX.CameraShake]
                         └─[Filter: waveIndex == 4]─pass─┘
```

两个 Filter 并联，任一满足都会激活 CameraShake（OR 语义由节点的默认激活规则保证——普通节点收到任意一个 PortEvent 就激活）。

### 7.3 波次 >= 3 且是 Boss 波时触发（AND 组合）

```
[Spawn.Wave]─onWaveStart→ [Filter: waveIndex >= 2] ─pass→ [Filter: monsterFilter == "Boss"] ─pass→ [VFX.CameraShake]
```

两个 Filter 串联，第一个 pass 连第二个 in，实现 AND 语义。

### 7.4 未来扩展：对话选择分支

```
[Dialog.Show]─onChoice→ [Filter: choiceId == "accept"] ─pass→ [Quest.Accept]
                        [Filter: choiceId == "reject"] ─pass→ [Dialog.Farewell]
```

同样的 Filter 机制，不同的数据来源。Dialog.Show 节点只需要在触发 onChoice 前写入 `{actionId}.choiceId`。

---

## 八、复杂条件的组合策略

### 8.1 设计决策：Filter 保持单条件

Flow.Filter 只做单条件判断（一个 key + 一个 op + 一个 value），复杂逻辑通过蓝图拓扑组合。

**理由**：
1. 90% 的场景是单条件（`waveIndex == 4`）
2. 保持节点原子性，策划更容易理解
3. AND/OR 通过串联/并联 Filter 实现，直观可见
4. 未来如果频繁出现多条件需求，可以升级为 StructList 多条件，不改端口和语义

### 8.2 组合规则

| 逻辑 | 蓝图拓扑 | 说明 |
|------|---------|------|
| AND | 串联 | Filter1.pass → Filter2.in → Filter2.pass → 下游 |
| OR | 并联 | Filter1.pass → 下游，Filter2.pass → 下游 |
| NOT | 用 reject | Filter.reject → 下游（条件不满足时触发） |

---

## 九、影响范围与实施路线

### 9.1 影响范围

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `Actions/Flow/FlowFilterDef.cs` | **新增** | Flow.Filter 节点定义 |
| `Runtime/Interpreter/Systems/FlowFilterSystem.cs` | **新增** | Flow.Filter 运行时系统 |
| `Runtime/Interpreter/Systems/SpawnWaveSystem.cs` | **修改** | EmitWaveStartEvent 前写入 Blackboard |
| `Runtime/Interpreter/Systems/TransitionSystem.cs` | **修改** | 激活节点时写入 `_activatedBy` |
| `Runtime/Test/BlueprintRuntimeManager.cs` | **修改** | 注册 FlowFilterSystem |
| `Editor/Interpreter/BlueprintTestWindow.cs` | **修改** | 注册 FlowFilterSystem |

### 9.2 不需要改动的部分

| 文件 | 原因 |
|------|------|
| `Blackboard.cs` | 已有 Set/Get/TryGet，无需扩展 |
| `PortEvent.cs` | 不改结构，数据走 Blackboard |
| `BlueprintFrame.cs` | 不改接口 |
| `BlueprintExporter.cs` | Flow.Filter 的属性都是基础类型，已有导出逻辑覆盖 |

### 9.3 实施路线

#### Phase 1：Blackboard 数据通道 + TransitionSystem 改造

```
目标：建立数据传递基础设施

步骤：
  1. TransitionSystem 激活节点时写入 _activatedBy.{targetActionId}
  2. SpawnWaveSystem.ProcessWaveAction 中，EmitWaveStartEvent 前写入 Blackboard 变量

验收：
  - 运行时测试中，Blackboard 中能看到 waveIndex/waveCount/monsterFilter 变量
  - _activatedBy 机制正确记录激活来源
```

#### Phase 2：Flow.Filter 节点定义 + 运行时系统

```
目标：实现条件过滤节点

步骤：
  1. 新增 FlowFilterDef.cs（节点定义：端口 + 属性）
  2. 新增 FlowFilterSystem.cs（运行时逻辑：读 Blackboard → 比较 → 路由端口）
  3. 注册 FlowFilterSystem 到 BlueprintRuntimeManager 和 BlueprintTestWindow

验收：
  - 蓝图编辑器中能创建 Flow.Filter 节点
  - 节点显示 key/op/value 三个属性
  - 有 in/pass/reject 三个端口
```

#### Phase 3：端到端测试

```
目标：验证完整链路

测试蓝图：
  Flow.Start → Trigger.EnterArea → Spawn.Wave(5波) → Flow.End
                                        │
                                        └─onWaveStart→ Flow.Filter(waveIndex == 4) ─pass→ VFX.CameraShake

验收：
  - 波次 1~4：Filter 走 reject，CameraShake 不触发
  - 波次 5：Filter 走 pass，CameraShake 触发
  - 所有波次完成后 Flow.End 正常执行
  - 日志输出清晰可读
```

---

## 十、设计决策记录

| # | 问题 | 决策 | 理由 |
|---|------|------|------|
| F1 | 数据传递用什么通道？ | Blackboard（全局字典） | 已有基础设施，不需要改 PortEvent 结构 |
| F2 | Blackboard 变量命名？ | `{actionId}.{variableName}` | 前缀避免冲突，支持多个同类型节点 |
| F3 | Filter 来源节点怎么确定？ | 自动推断（从 `_activatedBy` 读取） | 策划不需要手动填写来源节点 ID |
| F4 | Filter 支持多条件吗？ | 不支持，用蓝图拓扑组合 | 保持原子性，AND=串联，OR=并联 |
| F5 | Filter 的端口路由怎么实现？ | System 手动发射 PortEvent + CustomInt=1 | 与 SpawnWaveSystem.EmitWaveStartEvent 模式一致 |
| F6 | 比较逻辑支持哪些操作符？ | ==, !=, >, <, >=, <= | 覆盖常见场景，不引入正则或表达式 |
| F7 | 字符串和数字怎么区分？ | 运行时尝试转数字，失败则字符串比较 | 策划不需要关心类型 |
| F8 | 自动推断失败怎么办？ | 回退到直接用 key 作为 Blackboard key | 兜底机制，允许手动写完整 key |

---

## 附录

### 术语表

| 术语 | 定义 |
|------|------|
| **Blackboard** | 蓝图全局变量字典，System 之间通过 key-value 共享数据 |
| **数据通道** | 上游节点通过 Blackboard 发布数据、下游节点读取的机制 |
| **Flow.Filter** | 条件过滤节点，从 Blackboard 读取变量做条件判断，决定走 pass 或 reject |
| **自动推断** | Flow.Filter 自动从 `_activatedBy` 获取来源节点 ID，拼接 Blackboard key |
| **串联** | 多个 Filter 首尾相连，实现 AND 逻辑 |
| **并联** | 多个 Filter 的 pass 端口连到同一下游，实现 OR 逻辑 |

### 相关文档

- [SceneBlueprint核心设计原则](SceneBlueprint核心设计原则.md)
- [波次刷怪系统重构设计](波次刷怪系统重构设计.md)
- [节点激活语义与汇聚设计](节点激活语义与汇聚设计.md)

---

**版本历史**：

- **v1.0** (2026-02-19)
  - 初始版本
  - 设计 Blackboard 数据通道（命名规范、写入约定、生命周期）
  - 设计 Flow.Filter 条件过滤节点（端口、属性、运行时逻辑）
  - 设计自动推断来源节点机制（_activatedBy）
  - 定义 SpawnWaveSystem 改造方案
  - 提供 Blueprint 组合示例（单条件、AND、OR）
  - 确定复杂条件的组合策略（串联=AND、并联=OR、reject=NOT）
