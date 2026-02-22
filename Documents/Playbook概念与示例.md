# Playbook 概念与示例

> doc_status: active  
> last_reviewed: 2026-02-19

> 本文档定义 SceneBlueprint 中的 **Playbook（编排手册）** 概念。  
> 目标是将 NodeGraph 的图状规则转换成“关卡设计师可读、可评审、可验证”的规则文本层。

---

## 1. 什么是 Playbook

**Playbook 不是运行时系统**，而是 SceneBlueprint 的“可读解释层”。

- NodeGraph/SceneBlueprint 负责表达图状规则（分支、并行、汇合、回环）
- Playbook 负责把图翻译成规则手册，便于策划讨论、评审和排错

可以把它理解为：

- **Graph** = 机器可执行的结构化规则图
- **Playbook** = 人类可读的规则说明书

---

## 2. 为什么需要 Playbook

在复杂关卡里，规则通常是非线性的：

- 战斗：刷怪、分波、Join 汇合、Boss 演出
- 非战斗：摄像机移动、屏幕闪烁、UI 引导、机关开关
- 混合：达成条件后同时触发战斗和表现层事件

仅看图会遇到两个问题：

1. 图能连得出来，但很难快速确认“整体意图是否正确”
2. 讨论需求时，大家常用自然语言，难以直接映射到图

Playbook 的价值就是：

- 降低沟通成本（把图翻译成人话）
- 降低评审成本（按规则条目逐条检查）
- 降低回归成本（变更后可做规则 diff）

---

## 3. 与 SceneBlueprint 的关系

Playbook 不是替代图，而是由图派生。

建议采用“同源双视图”原则：

- **编辑视图**：NodeGraph（拖拽编排）
- **阅读视图**：Playbook（规则列表/时序摘要）

二者基于同一份数据，不允许手工双写。

---

## 4. 基础语法（建议）

每条规则至少包含以下字段：

- `When`：触发条件（事件、状态、变量）
- `If`：附加判定（可选）
- `Do`：执行动作（可多个）
- `InParallel`：是否并行
- `Wait`：等待条件（如 Delay、Join）
- `Then`：后续动作
- `Notes`：设计意图说明（可选）

示意：

```text
Rule R-001
When 玩家进入警戒区(Zone_A)
If  当前阶段 == Idle
Do  摄像机推进到 Boss 焦点, 屏幕闪烁一次
Then 激活战斗子图 Encounter_A
```

---

## 5. 节点到 Playbook 的映射示例

| SceneBlueprint 节点语义 | Playbook 语句语义 |
|---|---|
| `Flow.Start` | `When 蓝图启动` |
| `Flow.Branch` | `If / Else` |
| `Flow.Delay` | `Wait N 秒` |
| `Flow.Join(requiredCount=2)` | `Wait until 任意2条前置路径完成` |
| `Combat.Spawn` | `Do 刷怪（按模板/节奏/区域）` |
| `Combat.PlacePreset` | `Do 在预设点放置怪物` |
| （未来）`Presentation.CameraMove` | `Do 摄像机移动` |
| （未来）`Presentation.ScreenFlash` | `Do 屏幕闪烁` |

---

## 6. 示例 A：战斗编排（伏击 + Join）

### 图意图
- 玩家进入区域触发伏击
- 左右两路敌人并行出现
- 两路都结束后触发 Boss 出场

### Playbook 表达

```text
Rule A-01
When 玩家进入 Zone_A
Do 并行触发 LeftWave 与 RightWave

Rule A-02
When LeftWave 完成
Do 标记 LeftDone = true

Rule A-03
When RightWave 完成
Do 标记 RightDone = true

Rule A-04
When Join(requiredCount=2) 满足
Do 触发 Boss 出场镜头
Then 刷新 Boss（PresetPoint_Boss）
```

---

## 7. 示例 B：非战斗编排（达成条件后的表现控制）

### 图意图
- 玩家收集 3 个机关核心后
- 摄像机移动到遗迹核心
- 屏幕闪烁 + 震动 + UI 提示

### Playbook 表达

```text
Rule B-01
When CoreCollectedCount >= 3
Do Camera.MoveTo(RelicCore, blend=1.2s)
Do Screen.Flash(color=white, duration=0.25s)
Do Camera.Shake(intensity=0.3, duration=0.6s)
Do UI.ShowHint("遗迹核心已激活")
```

---

## 8. 示例 C：混合编排（战斗与非战斗并发）

### 图意图
- 玩家进入 Boss 区后
- 一边开始怪潮，一边播放演出
- 演出结束后才允许 Boss 正式参战

### Playbook 表达

```text
Rule C-01
When 玩家进入 BossZone
Do 并行：
   1) 激活怪潮子图 WaveLoop_A
   2) 播放 Boss 入场演出（镜头/音效/屏效）

Rule C-02
When 演出子图完成
If  当前场上存活怪 <= 8
Do 允许 Boss 进入战斗状态
Else Wait 2s 后重试
```

---

## 9. 关卡设计师 UX 建议

为了让 Playbook 真正可用，建议编辑器支持：

1. **一键查看 Playbook**：从当前图自动生成规则清单
2. **双向定位**：点规则高亮节点；点节点定位规则
3. **规则分组**：按子图/阶段/域（Combat、Presentation、World）分组
4. **规则校验**：显示“永不触发、死路、冲突动作、无限循环”
5. **变更对比**：展示版本间规则差异（新增/删除/条件变化）

---

## 10. 最小落地建议（不涉及运行时）

第一阶段只做 Authoring 层：

- 定义 Playbook 数据结构（只读派生）
- 提供 Graph -> Playbook 的转换器
- 在 SceneBlueprintWindow 增加 Playbook 面板（只读）

建议输出结构：

```json
{
  "blueprintId": "...",
  "rules": [
    {
      "id": "R-001",
      "when": "PlayerEnter(Zone_A)",
      "if": "Phase == Idle",
      "do": ["Camera.MoveTo(BossFocus)", "Screen.Flash(White)"]
    }
  ]
}
```

---

## 11. 结论

Playbook 的定位是：

- 保留 SceneBlueprint 的图状表达能力（非线性、可并发、可汇合）
- 同时提供策划可读的规则手册层
- 让“能编排”升级为“可沟通、可评审、可维护”

它是 SceneBlueprint 通用化（战斗 + 非战斗）的关键支撑能力。

---

## 12. 方向演进讨论（2026-02-19）

### 12.1 对当前定位的反思

第 1–10 节将 Playbook 定位为“Graph 的单向阅读层”，解决的是“沟通成本”问题。
但策划本身就在图里工作，再生成一份文档反而多一层。**更关键的问题**：Playbook 能否成为 Graph 的“等价替代输入”？

### 12.2 方向 A：Playbook 作为 AI 辅助建图的中间语言（推荐优先落地）

```
策划用自然语言描述意图
    ↓
AI 生成结构化 Playbook 规则
    ↓
工具自动转换成 NodeGraph
    ↓
策划在图里微调细节
```

Playbook 的真正价值不是“把图翻译给人看”，而是作为**人和图之间的桥梁**。

**最小落地路径**：
1. 编辑器新增 Playbook 面板（只读）：自动将当前图翻译成结构化规则列表
2. 支持双向定位：点击规则 → 高亮对应节点
3. 规则格式作为未来 AI 辅助生成蓝图的输入接口

### 12.3 方向 B：Playbook 作为可执行脚本（替代简单图）

策划直接写 YAML / DSL，工具解析为运行时格式，跳过图编辑器：

```yaml
on: Start
parallel:
  - spawn.preset: guard_area
  - spawn.wave:
      area: main_arena
      waves: [5 Normal, 5 Elite, 1 Boss]
join: all
then: End
```

| 维度 | 方向 A | 方向 B |
|---|---|---|
| **核心价值** | 降低策划建图门槛 | 替代简单图，提升可维护性 |
| **与图的关系** | 双向转换（草稿→图，图→复查）| 并存，图处理复杂逻辑 |
| **实现难度** | 中（需要 Playbook→Graph 转换器）| 中（需要新的解析器）|
| **最大受益者** | 不熏悉图编辑器的策划 | 需要维护大量关卡数据的团队 |

### 12.4 相关文档

- [缺失功能盘点](缺失功能盘点.md)：第三章有更完整的 Playbook 方向比较和落地路径
