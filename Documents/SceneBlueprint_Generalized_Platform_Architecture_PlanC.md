# SceneBlueprint 通用化平台架构方案 C 设计

> 版本：v0.1（详细设计稿）  
> 日期：2026-02-14  
> 状态：待实现  
> 适用范围：`Assets/Extensions/SceneBlueprint` + `Assets/Extensions/NodeGraph`
> doc_status: active  
> last_reviewed: 2026-02-15

---

## 1. 背景与目标

当前 SceneBlueprint 已具备较强的图编排能力，但“场景空间层”仍偏 Unity 3D 实现。  
我们本次直接采用**方案 C（平台化）**，目标是把 SceneBlueprint 提升为“可跨 2D / 3D / 多项目复用”的技术底座。

### 1.1 总目标

1. **编排内核通用**：NodeGraph 继续保持业务无关。
2. **领域模型通用**：Action/Property/Requirement 不依赖 Unity API。
3. **空间能力插件化**：2D/3D/其他引擎通过 Adapter 接入。
4. **编辑体验统一**：策划无论做 2D 还是 3D，都走同一套工作台流程。
5. **降低心智负担**：从“资产分散配置”转向“任务导向配置”。

### 1.2 非目标

1. 不在本阶段引入 Lua/JS 运行时脚本系统。
2. 不重写 NodeGraph 核心渲染与命令系统。
3. 不做多团队协作流程治理（当前单人使用，可先提高开发速度）。

---

## 2. 现状评估（基于代码）

## 2.1 已经具备通用性的部分

1. **NodeGraph 内核抽象完备**
   - 图模型（Graph/Node/Edge）与业务数据解耦（`UserData`）。
   - 引用：`Assets/Extensions/NodeGraph/Core/Graph.cs`、`Node.cs`、`Edge.cs`
2. **连接策略可替换**
   - 通过 `IConnectionPolicy` 实现自定义连接规则。
   - 引用：`Assets/Extensions/NodeGraph/Core/IConnectionPolicy.cs`
3. **输入/序列化已接口化**
   - `IPlatformInput`、`IGraphSerializer` 允许跨宿主平台接入。
   - 引用：`Assets/Extensions/NodeGraph/Abstraction/IPlatformInput.cs`、`IGraphSerializer.cs`
4. **SceneBlueprint 对 NodeGraph 的桥接清晰**
   - `ActionDefinition -> NodeTypeDefinition` 已标准化适配。
   - 引用：`Assets/Extensions/SceneBlueprint/Editor/ActionNodeTypeAdapter.cs`

## 2.2 当前限制通用性的部分

1. **场景放置逻辑偏 3D**
   - `Physics.Raycast + Y=0` 回退属于 3D 假设。
   - 引用：`Assets/Extensions/SceneBlueprint/Editor/Markers/SceneViewMarkerTool.cs`
2. **空间数据结构偏 3D 几何**
   - `AreaMarker` 主要使用 `Vector3/Height/BoxSize`。
   - 引用：`Assets/Extensions/SceneBlueprint/Runtime/Markers/AreaMarker.cs`
3. **Binding 作用域仍是纯 BindingKey**
   - 注释已指出后续可扩展为 `subGraphId/bindingKey`。
   - 引用：`Assets/Extensions/SceneBlueprint/Editor/BindingContext.cs`
4. **导出对象标识稳定性不足**
   - 导出链路中 `SceneObjectId` 当前实质写入对象名，存在重名风险。
   - 引用：`Assets/Extensions/SceneBlueprint/Editor/SceneBlueprintWindow.cs`、`Editor/Export/BlueprintExporter.cs`

---

## 3. 目标架构（方案 C）

## 3.1 分层架构

```text
┌─────────────────────────────────────────────────────────────┐
│                 SceneBlueprint.Editor.Workbench             │
│  (统一配置工作台 / 向导 / 关系图 / 问题中心 / 预览)         │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│               SceneBlueprint.Application                    │
│  (编译管线 / 验证管线 / 注册组合 / 导出编排 / 用例服务)     │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                  SceneBlueprint.Domain                      │
│  (Action/Property/MarkerRequirement/ExportIR 纯领域模型)   │
└───────────────────────┬─────────────────────────────────────┘
                        │ 接口协议
┌───────────────────────▼─────────────────────────────────────┐
│           SceneBlueprint.SpatialAbstraction                │
│ (Placement/BindingCodec/ObjectIdentity/ScopePolicy 等)     │
└───────────────┬───────────────────────────────┬─────────────┘
                │                               │
┌───────────────▼──────────────┐   ┌────────────▼──────────────┐
│ SceneBlueprint.Adapter.Unity3D│   │ SceneBlueprint.Adapter.Unity2D│
│ (SceneView + 3D Marker + 3D导出)│  │ (2D Grid/Tile + 2D导出)      │
└──────────────────────────────┘   └───────────────────────────┘

      NodeGraph.Core / View / Commands（保持底层通用，不依赖具体玩法）
```

## 3.2 模块职责

### A. NodeGraph（不改定位）
- 负责图模型、命令系统、交互处理链、渲染描述。
- 不承担 SceneBlueprint 业务语义。

### B. SceneBlueprint.Domain（新增纯领域层）
- 只定义“可编排语义”：Action、Property、MarkerRequirement、Transition、Validation。
- 禁止依赖 `UnityEngine.*`。

### C. SceneBlueprint.Application（新增用例层）
- 负责：模板合并、注册、验证、导出流水线。
- 只依赖 Domain + 抽象接口，不依赖 Unity 实现细节。

### D. SpatialAbstraction（新增关键层）
- 定义“场景空间能力合同”：
  - 放置策略
  - 对象标识
  - 绑定编解码
  - 作用域策略

### E. Adapter（新增插件层）
- Unity3DAdapter：沿用当前 Marker 流程并抽离到插件。
- Unity2DAdapter：实现 2D 放置、2D 空间对象语义、2D 导出策略。

---

## 4. 核心接口详细设计

## 4.1 放置策略接口

```csharp
public interface IScenePlacementPolicy
{
    bool TryGetPlacement(ScenePlacementRequest request, out ScenePlacementResult result);
}

public struct ScenePlacementRequest
{
    public string SurfaceHint;     // Ground, Grid, UI, Any
    public float SnapSize;         // 0 表示不吸附
    public string CoordinateMode;  // XZ / XY / Custom
}

public struct ScenePlacementResult
{
    public bool Success;
    public Vector3 Position;
    public Quaternion Rotation;
    public string SpaceTag;        // 例如 "2D.Grid" / "3D.Terrain"
}
```

说明：
- 3D 适配器实现 Raycast + Terrain/Plane 策略。
- 2D 适配器实现 Tilemap/Grid/Collider2D 命中策略。

## 4.2 对象身份接口（稳定 ID）

```csharp
public interface ISceneObjectIdentityService
{
    string GetOrCreateStableId(object sceneObject);
    bool TryResolve(string stableId, out object sceneObject);
}
```

说明：
- 取代当前按 `GameObject.name` 的导出路径。
- 解决重名、改名导致绑定漂移的问题。

## 4.3 绑定编解码接口

```csharp
public interface ISpatialBindingCodec
{
    SceneBindingPayload Encode(object sceneObject, BindingType bindingType);
    bool TryDecode(SceneBindingPayload payload, out object sceneObject);
}

public struct SceneBindingPayload
{
    public string StableObjectId;
    public string BindingType;
    public string SerializedSpatialData; // JSON
    public string AdapterType;           // Unity3D / Unity2D / Custom
}
```

说明：
- `BindingType` 仍保留 Domain 语义：Transform/Area/Path/Collider。
- 具体几何数据格式由 Adapter 负责。

## 4.4 绑定作用域接口

```csharp
public interface IBindingScopePolicy
{
    string BuildScopedKey(string subGraphId, string bindingKey, string nodeId);
}
```

默认建议：
- `ScopedKey = subGraphId + "/" + bindingKey`
- 需要更细粒度时扩展为 `subGraphId/bindingKey/nodeId`

---

## 5. 数据模型升级设计

## 5.1 SceneBindingEntry v2（导出）

当前模型：
- `BindingKey`
- `BindingType`
- `SceneObjectId`（当前实际上是对象名）

升级后建议：

```csharp
public class SceneBindingEntryV2
{
    public string ScopedBindingKey;      // subGraphId/bindingKey
    public string BindingType;           // Transform/Area/Path/Collider
    public string StableObjectId;        // IdentityService 生成
    public string AdapterType;           // Unity3D / Unity2D
    public string SpatialPayloadJson;    // 由 BindingCodec 生成
    public string SourceSubGraph;
    public string SourceActionTypeId;
}
```

## 5.2 MarkerRequirement 兼容策略

- 继续保留 `PresetId` 主路径。
- 向后兼容字段保留，但在 v2 文档中标记为 Legacy。
- 编译阶段输出统一的 `CompiledMarkerRequirement`，供运行与导出共用。

---

## 6. 包结构与目录规划

> 单人项目可直接在当前仓库按目录落地，不必先拆独立 Git 仓库。

```text
Assets/Extensions/SceneBlueprint/
  Domain/
    Actions/
    Markers/
    Export/
  Application/
    Compile/
    Validate/
    Export/
  SpatialAbstraction/
    IScenePlacementPolicy.cs
    ISceneObjectIdentityService.cs
    ISpatialBindingCodec.cs
    IBindingScopePolicy.cs
  Adapters/
    Unity3D/
    Unity2D/
  Editor/
    Workbench/
```

迁移原则：
1. 先新增目录和接口，不马上删旧实现。
2. 通过 Facade 方式逐步替换旧入口。
3. 每一步都保留可回退路径。

---

## 7. Editor UI/UX（工作台）重构设计

目标：降低策划心智负担，把“资产编辑”改成“任务流程”。

## 7.1 工作台信息架构

三栏布局：
1. **左栏：任务导航**
   - 新建玩法流程
   - Action 类型库
   - Marker 预设库
   - 验证规则
2. **中栏：主流程面板**
   - Step1 选择玩法模板
   - Step2 配置 Action
   - Step3 配置场景绑定
   - Step4 验证并导出
3. **右栏：上下文与问题中心**
   - 依赖关系图（Action ↔ Preset）
   - 实时错误/警告
   - 一键修复建议

## 7.2 关键 UX 机制

1. **模式切换（2D/3D）**
   - 顶部全局切换：`Runtime Space: 3D / 2D`
   - 切换后自动使用对应 Adapter 与预设模板
2. **向导优先**
   - 创建 Action 时先选“意图模板”（刷怪、触发、镜头等）
   - 自动生成最小可用配置骨架
3. **关系可视化**
   - 选中 Preset 时立即显示受影响 Action/节点数量
   - 删除前展示影响清单
4. **错误集中处理**
   - 不让错误分散在各 Tab
   - 提供“定位并修复”按钮

## 7.3 心智减负规则

1. 不让策划手写关键 ID（自动建议 + 唯一性校验）。
2. 先配置“是什么”（意图、需求），再配置“细节值”。
3. 同屏显示“当前选择的配置会影响什么”。
4. 任何 destructive 操作前先给影响面板。

---

## 8. 实施计划（方案 C 直推，阶段完成即可立刻验证）

### 8.1 执行节奏与门禁原则

1. **不跨阶段欠账**：每完成一个阶段，必须在同一天完成验证。
2. **每阶段必须有三类产出**：
   - 功能改动（代码/配置）
   - 验证记录（操作步骤 + 结果 + 关键截图或日志）
   - 可回滚点（建议打本地 tag 或记录提交号）
3. **验证固定三层**：
   - L1 编译层：Unity 无编译报错
   - L2 编辑层：SceneBlueprintWindow 核心流程可操作
   - L3 数据层：导出 JSON 结构符合预期

### 8.2 阶段 C1：骨架搭建（1~2 天）

实现项：
- 建立 Domain/Application/SpatialAbstraction/Adapters 目录。
- 增加核心接口与最小实现占位。

即时验证：
1. Unity 编译通过（0 Error）。
2. 新目录与接口文件均可在 Project 视图定位。
3. `SceneBlueprintWindow` 可正常打开、右键创建节点、保存/加载蓝图不回归。

通过标准：
- 新增骨架不影响现有编辑流程。

失败回滚：
- 保留目录结构，回退接口接入点到旧逻辑。

### 8.3 阶段 C2：导出链路稳定化（2~3 天）

实现项：
- 引入 StableObjectId。
- `SceneBindingEntry` 升级为 V2（保留 V1 输出开关）。

即时验证：
1. 场景中创建两个同名对象并分别绑定到不同节点。
2. 导出 JSON，确认每条绑定都包含稳定 ID（而非对象名）。
3. 修改对象名称后再次导出，稳定 ID 保持不变。
4. 切换 V1 输出开关后，旧运行时读取链路仍可使用。

通过标准：
- 绑定可抗重命名，且对旧格式保持兼容。

失败回滚：
- 临时回退到 V1 导出路径，仅保留 StableId 生成逻辑。

### 8.4 阶段 C3：3D 适配器抽离（2~3 天）

实现项：
- 将 `SceneViewMarkerTool` 的放置与编码能力抽到 Unity3DAdapter。
- 保持现有行为一致（回归测试）。

即时验证：
1. 在 SceneView 右键创建含 SceneRequirements 的 Action，标记仍可自动生成并绑定。
2. MarkerPreset 的默认 Tag / 颜色 / 尺寸仍按既有优先级生效。
3. 导出结果与改造前对比，业务字段一致（新增 adapter 字段除外）。

通过标准：
- 用户侧操作不变，3D 项目无感迁移。

失败回滚：
- 保留适配器代码，入口切回旧 `SceneViewMarkerTool` 直连实现。

### 8.5 阶段 C4：2D 适配器最小可用（3~5 天）

实现项：
- 支持 XY 平面放置。
- 支持 Transform / Area 基础导出。

即时验证：
1. 切换 Runtime Space=2D 后，可在 XY 平面放置 Point/Area 标记。
2. 2D 模式下导出 JSON，`AdapterType=Unity2D` 且绑定可回读。
3. 切回 3D 模式后，原 3D 放置流程不受影响。

通过标准：
- 同一套 Action 模板在 2D/3D 均可跑通最小流程。

失败回滚：
- 暂停 2D 入口，只保留 3D adapter 默认路径。

### 8.6 阶段 C5：BindingScope 升级（2 天）

实现项：
- `bindingKey` -> `scopedBindingKey`。
- 完成恢复、同步、导出链路联调。

即时验证：
1. 在两个子图中创建同名 `bindingKey`，确保互不覆盖。
2. 执行“同步到场景”后，Manager 中可看到作用域化键。
3. 关闭并重开蓝图，绑定恢复正确。
4. 导出 JSON，绑定键为 scoped 形式。

通过标准：
- 子图作用域内同名键不冲突，恢复链路稳定。

失败回滚：
- 保留 scoped 生成工具，恢复导出/恢复链路为旧 key。

### 8.7 阶段 C6：工作台 UI 改造（4~6 天）

实现项：
- 三栏布局 + 向导 + 问题中心 + 关系面板。

即时验证：
1. 从“新建玩法”到“导出”可在单窗口闭环完成。
2. 问题中心可定位至少 3 类错误（缺失绑定、缺失必填属性、引用失效）。
3. 关系面板可展示 Preset 被哪些 Action 引用。

通过标准：
- 新用户按向导流程可在 10 分钟内完成最小蓝图配置。

失败回滚：
- 保留工作台入口，必要时仅降级关闭问题中心/关系面板增强能力。

### 8.8 阶段 C7：验收与清理（2~3 天）

实现项：
- 统一文档、删除过时入口。
- 以工作台为唯一入口，移除旧配置入口与对应菜单项。
- 新增验收记录文档：`Assets/Extensions/SceneBlueprint/_archive/C7_验收记录.md`。

即时验证：
1. 完整执行 C1~C6 验证清单，形成最终验收记录。
2. 检查旧入口已不可见且不可调用。
3. 文档与代码目录一致，避免“文档漂移”。

通过标准：
- 主路径稳定可用，入口策略唯一且清晰。

失败回滚：
- 暂缓物理删除，仅保留代码层占位并冻结文档。

---

## 9. 迁移映射（现有类 -> 新职责）

| 现有类 | 新位置/新职责 | 说明 |
|---|---|---|
| `SceneBlueprintProfile` | `Application/ProfileComposer` | 负责注册组合，不直接耦合 Unity 空间细节 |
| `ActionNodeTypeAdapter` | `Application/NodeTypeProjectionBuilder` | 领域定义到 NodeType 投影 |
| `SceneViewMarkerTool` | `Adapters/Unity3D/PlacementTool` | 保留 UI 入口，空间计算下沉到策略接口 |
| `BindingContext` | `Application/BindingSession` | 支持 scoped key 与 adapter 元数据 |
| `BlueprintExporter` | `Application/ExportPipeline` | 统一调用 Identity + BindingCodec |
| `SceneBlueprintManager` | `Adapters/Unity*/PersistenceBridge` | 作为场景宿主存储桥接 |

---

## 10. 质量门禁与验收标准

### 10.1 阶段门禁矩阵（每阶段结束必须勾选）

| 阶段 | L1 编译层 | L2 编辑层 | L3 数据层 |
|---|---|---|---|
| C1 | 无编译错误 | 打开窗口 + 创建节点 + 保存加载 | 导出基础 JSON 成功 |
| C2 | 无编译错误 | 绑定编辑流程正常 | StableObjectId 生效（V2 统一语义） |
| C3 | 无编译错误 | 3D 标记创建/绑定不回归 | 导出结构对齐（新增 adapter 字段除外） |
| C4 | 无编译错误 | 2D/3D 模式切换都可操作 | `AdapterType` 正确输出并可回读 |
| C5 | 无编译错误 | 多子图同名键不冲突 | scoped key 导出正确 |
| C6 | 无编译错误 | 单窗口闭环可完成 | 错误定位与关系信息可导出 |
| C7 | 无编译错误 | 主入口唯一、旧入口移除 | 最终验收记录完整 |

### 10.2 功能验收

1. 同一套 ActionTemplate 可在 3D 模式与 2D 模式下创建并导出。
2. 导出的绑定对象使用稳定 ID，不受重命名影响。
3. 子图作用域下同名 `bindingKey` 不再冲突。
4. 工作台可在单入口完成“配置-验证-导出”。

### 10.3 回归验收

1. 导出结果统一采用 V2 语义（旧数据不再兼容）。
2. MarkerPreset 的 D 阶段能力不回退（PresetId、颜色优先级、热刷新）。
3. 子蓝图边界端口与展平导出逻辑保持正确。

---

## 11. 风险与应对

1. **风险：一次性改动面过大**  
   应对：先完成入口切换验证，再做旧入口清理，必要时保留最小占位。
2. **风险：导出格式升级影响运行时读取**  
   应对：统一 V2 语义并同步消费端协议，停止保留旧语义分支。
3. **风险：2D 语义与 3D 语义混用导致配置混乱**  
   应对：工作台加入 Runtime Space 模式锁，模板按模式过滤。

---

## 12. 本文档对应的首批落地任务（建议）

1. 新增 `SpatialAbstraction` 四个接口文件。
2. 在导出链路增加 `StableObjectId`（先不改 UI）。
3. 将 `BindingContext` 升级为 scoped key。
4. 把 `SceneViewMarkerTool.TryRaycastGround` 改为策略调用。
5. 补一组 2D 最小可用演示（XY 平面 + Point/Area）。

---

> 结论：方案 C 可直接执行。  
> 你当前是单人迭代，这个窗口期非常适合先把底层“平台化接口”打稳，再做功能扩张。
