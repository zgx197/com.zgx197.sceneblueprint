# SceneBlueprint DSL 设计文档 — `.sbdef`

> 版本：v0.3（已完成）  
> 状态：v0.1 ✅ v0.2 ✅ v0.3 ✅，v0.4 待实施

---

## 1. 背景与目标

### 1.1 现有痛点

当前新增一个 ActionNode 需要手动维护多处，存在严重的**冗余定义**：

| 步骤 | 文件 | 内容 |
|---|---|---|
| 1 | `UAT.*.g.cs` | 添加类型 ID 字符串常量（v0.1 已由 .sbdef 生成 ✅）|
| 2 | `ActionPortIds.cs` | 添加 `PropertyKey<T>` 端口键（仍手写）|
| 3 | `IActionDefinitionProvider` 实现 | 注册端口名、标签、min/max、颜色（仍手写）|
| 4 | System `.cs` | 手写业务逻辑（合理，永远不生成）|

步骤 2-3 的端口名称、类型、默认值在两处分别定义，极易漂移产生不一致 bug。v0.2 目标是将步骤 2-3 也纳入 `.sbdef` 生成。

### 1.2 目标

引入 `.sbdef` 文件作为 Action/Marker **声明的单一数据源**：

- **写一次，生成多处**：保存 `.sbdef` 自动重新生成对应的 `.g.cs` 常量文件
- **类型安全**：System 引用生成的常量，拼写错误在编译期暴露
- **低侵入**：不改变 System 的手写方式，不强制修改现有架构
- **可增量迁移**：新 Action 用 DSL，旧 Action 可保持现状共存

---

## 2. DSL 语法设计

### 2.1 v0.1 语法（已实现）

```
// vfx.sbdef — 仅类型 ID + 端口声明
action VFX.CameraShake {
    port float Duration  = 0.5
    port float Intensity = 1.0
    port float Frequency = 20.0
}
```

生成：`UAT.Vfx.g.cs`（类型 ID 常量）

### 2.2 v0.2 语法扩展（✅ 已实现）

在 action 块内新增元数据关键字，port 声明新增 `label`/`min`/`max` 选项：

```
// vfx.sbdef — 完整 v0.2 写法
action VFX.CameraShake {
    displayName "摄像机震动"
    category    "VFX"
    description "产生屏幕震动效果"
    themeColor  0.2 0.6 1.0
    duration    instant

    port float Intensity = 1.0  label "强度"      min 0.0  max 3.0
    port float Duration  = 0.5  label "时长(秒)"  min 0.1  max 5.0
    port float Frequency = 20.0 label "频率"
}

action VFX.ShowWarning {
    displayName "屏幕警告"
    category    "VFX"
    themeColor  0.9 0.3 0.3
    duration    duration

    port string Text     = ""   label "警告文字"
    port float  Duration = 2.0  label "持续时长"  min 0.5  max 10.0
    port int    FontSize = 36   label "字号"      min 12   max 72
}
```

> **flow 端口**：`in` / `out` 默认为所有 action 自动生成，无需声明。
> 若需额外流控端口（如 `Spawn.Wave` 的 `onWaveStart`），使用 `flow` 关键字（v0.4 规划）。

### 2.3 v0.3 语法扩展（✅ 已实现）

`marker` 块内支持 `label` 和 `gizmo` 关键字：

```
// markers.sbdef
marker SpawnPoint {
    label "刷怪点"
    gizmo sphere(0.4)
}

marker TriggerZone {
    label "触发区域"
    gizmo wire_box
}
```

### 2.4 完整语法规则（EBNF）

```ebnf
file          ::= statement*
statement     ::= action_decl | marker_decl | line_comment

action_decl   ::= 'action' type_id '{' action_member* '}'
type_id       ::= IDENT ('.' IDENT)*

action_member ::= action_meta | port_decl
action_meta   ::= 'displayName' STRING
                | 'category'    STRING
                | 'description' STRING
                | 'themeColor'  NUMBER NUMBER NUMBER
                | 'duration'    ('instant' | 'duration' | 'passive')

port_decl     ::= 'port' type_name IDENT ('=' literal)? port_opts?
port_opts     ::= ('label' STRING)? ('min' NUMBER)? ('max' NUMBER)?
type_name     ::= 'float' | 'int' | 'bool' | 'string' | 'color' | IDENT

marker_decl   ::= 'marker' IDENT '{' marker_prop* '}'
marker_prop   ::= 'label' STRING | 'gizmo' gizmo_shape
gizmo_shape   ::= 'sphere' ('(' NUMBER ')')? | 'wire_sphere' | 'box' | 'wire_box'

literal       ::= NUMBER | STRING | 'true' | 'false'
line_comment  ::= '//' ~'\n'* '\n'
```

### 2.5 类型映射

| DSL 类型 | C# 类型 | 生成的 PropertyKey<T> |
|---|---|---|
| `float` | `float` | `PropertyKey<float>` |
| `int` | `int` | `PropertyKey<int>` |
| `bool` | `bool` | `PropertyKey<bool>` |
| `string` | `string` | `PropertyKey<string>` |
| `color` | `UnityEngine.Color` | `PropertyKey<Color>`（v0.4 规划）|
| 其他 IDENT | 透传为 C# 类型名 | `PropertyKey<T>` |

---

## 3. 生成产物

### 3.1 v0.1 产物：`UAT.*.g.cs` ✅

类型 ID 字符串常量，已实现。

```csharp
// Generated/UAT.Vfx.g.cs
namespace SceneBlueprintUser.Generated
{
    public static partial class UAT
    {
        public static class Vfx
        {
            public const string CameraShake = "VFX.CameraShake";
            public const string ScreenFlash = "VFX.ScreenFlash";
            public const string ShowWarning = "VFX.ShowWarning";
        }
    }
}
```

### 3.2 v0.2 产物：`UActionPortIds.*.g.cs`

每个 `port` 声明生成 `PropertyKey<T>`；`in`/`out` 自动生成 `const string`。

```csharp
// Generated/UActionPortIds.Vfx.g.cs
#nullable enable
using SceneBlueprint.Core;

namespace SceneBlueprintUser.Generated
{
    public static partial class UActionPortIds
    {
        public static class VFXCameraShake
        {
            public const string In  = "in";
            public const string Out = "out";
            public static readonly PropertyKey<float> Intensity = new("intensity");
            public static readonly PropertyKey<float> Duration  = new("duration");
            public static readonly PropertyKey<float> Frequency = new("frequency");
        }

        public static class VFXShowWarning
        {
            public const string In  = "in";
            public const string Out = "out";
            public static readonly PropertyKey<string> Text     = new("text");
            public static readonly PropertyKey<float>  Duration = new("duration");
            public static readonly PropertyKey<int>    FontSize = new("fontSize");
        }
    }
}
```

### 3.3 v0.2 产物：`ActionDefs.*.g.cs` ✅

完整 `IActionDefinitionProvider` 实现，替代手写 Def 文件。类名规则：`TypeId` 去点后缀 `Def`（如 `VFXCameraShakeDef`）。

```csharp
// Generated/ActionDefs.Vfx.g.cs
// <auto-generated>
#nullable enable
using NodeGraph.Math;
using SceneBlueprint.Core;

namespace SceneBlueprintUser.Generated
{
    [ActionType(UAT.Vfx.CameraShake)]
    public class VFXCameraShakeDef : IActionDefinitionProvider
    {
        public ActionDefinition Define() => new ActionDefinition
        {
            TypeId      = UAT.Vfx.CameraShake,
            DisplayName = "摄像机震动",
            Category    = "VFX",
            Description = "产生屏幕震动效果",
            ThemeColor  = new Color4(0.8f, 0.4f, 0.9f),
            Duration    = ActionDuration.Duration,
            Ports = new[]
            {
                Port.In(UActionPortIds.VFXCameraShake.In,  "输入"),
                Port.Out(UActionPortIds.VFXCameraShake.Out, "输出"),
            },
            Properties = new[]
            {
                Prop.Float(UActionPortIds.VFXCameraShake.Duration,  "时长(秒)", defaultValue: 0.5f, min: 0.1f, max: 5f,   order: 0),
                Prop.Float(UActionPortIds.VFXCameraShake.Intensity, "强度",     defaultValue: 1f,   min: 0.1f, max: 10f,  order: 1),
                Prop.Float(UActionPortIds.VFXCameraShake.Frequency, "频率",     defaultValue: 20f,  min: 1f,   max: 100f, order: 2),
            },
            SceneRequirements = System.Array.Empty<MarkerRequirement>()
        };
    }
    // ... 同文件内包含同一 .sbdef 的其他 action
}
```

### 3.4 v0.3 产物：Marker 三件套 ✅

每个含 `marker` 声明的 `.sbdef` 文件生成三个文件，分属 Runtime 和 Editor 两个程序集。

**`UMarkerTypeIds.{Name}.g.cs`**（Runtime）—— 类型 ID 常量，`partial class` 支持多文件合并：

```csharp
// Generated/UMarkerTypeIds.Markers.g.cs
namespace SceneBlueprintUser.Generated
{
    public static partial class UMarkerTypeIds
    {
        /// <summary>刷怪点</summary>
        public const string SpawnPoint  = "SpawnPoint";
        /// <summary>触发区域</summary>
        public const string TriggerZone = "TriggerZone";
    }
}
```

**`UMarkers.{Name}.g.cs`**（Runtime）—— `SceneMarker` partial 骨架：

```csharp
// Generated/UMarkers.Markers.g.cs
using UnityEngine;
using SceneBlueprint.Runtime.Markers;

namespace SceneBlueprintUser.Generated
{
    [AddComponentMenu("SceneBlueprintUser/SpawnPoint Marker")]
    public partial class SpawnPointMarker : SceneMarker
    {
        public override string MarkerTypeId => UMarkerTypeIds.SpawnPoint;
        // gizmo: sphere(0.4)  — 在对应的 Editor Gizmo Drawer 中实现
    }

    [AddComponentMenu("SceneBlueprintUser/TriggerZone Marker")]
    public partial class TriggerZoneMarker : SceneMarker
    {
        public override string MarkerTypeId => UMarkerTypeIds.TriggerZone;
        // gizmo: wire_box  — 在对应的 Editor Gizmo Drawer 中实现
    }
}
```

**`Editor/UMarkerDefs.{Name}.g.cs`**（Editor-only）—— `IMarkerDefinitionProvider` 实现，被 `MarkerDefinitionRegistry.AutoDiscover` 自动发现：

```csharp
// Generated/Editor/UMarkerDefs.Markers.g.cs
using SceneBlueprint.Editor.Markers.Definitions;
using SceneBlueprint.Runtime.Markers;
using SceneBlueprintUser.Generated;

namespace SceneBlueprintUser.Generated.Editor
{
    [MarkerDef(UMarkerTypeIds.SpawnPoint)]
    public class SpawnPointMarkerDef : IMarkerDefinitionProvider
    {
        public MarkerDefinition Define() => new MarkerDefinition
        {
            TypeId        = UMarkerTypeIds.SpawnPoint,
            DisplayName   = "刷怪点",
            ComponentType = typeof(SpawnPointMarker),
            DefaultSpacing = 2f,
        };
    }
}
```

---

## 4. 生成管道实现

### 4.1 整体流程

使用 **`ScriptedImporter`** 注册 `.sbdef` 扩展名，`.sbdef` 保存时自动触发代码生成。

```
.sbdef 文件（保存/导入）
    ↓
SbdefAssetImporter : ScriptedImporter
    ↓
SbdefLexer   → Token[]
    ↓
SbdefParser  → SbdefAst
    ↓
┌─────────────────┬──────────────────┬─────────────────┐
SbdefActionEmitter ✅  SbdefDefEmitter ✅       SbdefMarkerEmitter ✅
→ UAT.*.g.cs         → UActionPortIds.*.g.cs  → UMarkerTypeIds.*.g.cs
                      → ActionDefs.*.g.cs       → UMarkers.*.g.cs
                                                 → Editor/UMarkerDefs.*.g.cs
    ↓
File.WriteAllText()（仅内容变化时写入，避免无效触发）
    ↓
AssetDatabase.Refresh()
```

> `SbdefDefEmitter` 共享同一次 AST 遍历，生成 `UActionPortIds` 和 `ActionDefs`。  
> `SbdefMarkerEmitter` 生成的 `Editor/UMarkerDefs.*.g.cs` 路由到 `Generated/Editor/` 子目录，属 Editor-only 程序集。

### 4.2 AST 结构（C#）

```csharp
record SbdefFile(List<SbdefStatement> Statements);

abstract record SbdefStatement;

record ActionDecl(
    string         TypeId,
    ActionMeta     Meta,
    List<PortDecl> Ports
) : SbdefStatement;

record ActionMeta(
    string? DisplayName,   // "摄像机震动"
    string? Category,      // "VFX"
    string? Description,
    string? ThemeColor,    // "0.2 0.6 1.0" 原始字符串
    string? Duration       // "instant" | "duration"
);

record PortDecl(
    string  TypeName,      // "float" / "int" / "string" ...
    string  Name,          // "Duration"
    string? Default,       // "0.5"
    string? Label,         // "时长(秒)"
    string? Min,           // "0.1"
    string? Max            // "5.0"
);

record MarkerDecl(
    string  Name,
    string? Label,       // 如 "刷怪点"
    string? GizmoShape,  // "sphere" | "wire_sphere" | "box" | "wire_box"
    string? GizmoParam   // 如 "0.4"（对应 sphere(0.4) 的已解析数字）
) : SbdefStatement;
```

### 4.3 文件放置约定

**决策：`.sbdef` 文件只放在用户项目，框架包不使用 DSL**

- 框架内置 Action（`Flow.Join`、`Flow.Filter`）在 C# 里定义，**不迁移**到 `.sbdef`
- 游戏特定 Action / Marker 定义放在 `SceneBlueprintUser/Definitions/`
- 生成文件提交到 git（CI 无需额外 codegen 步骤）

```
SceneBlueprintUser/
  Definitions/
    vfx.sbdef                        ← 用户手写（含 displayName/category/themeColor/duration/label/min/max）
    spawn.sbdef
    trigger.sbdef
    markers.sbdef                    ← v0.3 新增，含 marker 声明
  Generated/                         ← 自动生成，提交到 git
    SceneBlueprintUser.Generated.asmdef   ← Runtime，引用 SceneBlueprint.Core + .Runtime
    UAT.Vfx.g.cs                     ← v0.1 ✅
    UAT.Spawn.g.cs                   ← v0.1 ✅
    UAT.Trigger.g.cs                 ← v0.1 ✅
    UActionPortIds.Vfx.g.cs          ← v0.2 ✅
    UActionPortIds.Spawn.g.cs        ← v0.2 ✅
    UActionPortIds.Trigger.g.cs      ← v0.2 ✅
    ActionDefs.Vfx.g.cs              ← v0.2 ✅
    ActionDefs.Spawn.g.cs            ← v0.2 ✅
    ActionDefs.Trigger.g.cs          ← v0.2 ✅
    UMarkerTypeIds.Markers.g.cs      ← v0.3 ✅
    UMarkers.Markers.g.cs            ← v0.3 ✅
    Editor/                          ← Editor-only 子目录
      SceneBlueprintUser.Generated.Editor.asmdef  ← Editor-only，引用 SceneBlueprint.Editor
      UMarkerDefs.Markers.g.cs       ← v0.3 ✅
```

| 类型 | 源文件 | 生成文件 | 版本 |
|---|---|---|---|
| Action 类型常量 | `*.sbdef` | `Generated/UAT.*.g.cs` | v0.1 ✅ |
| Port PropertyKey | `*.sbdef` | `Generated/UActionPortIds.*.g.cs` | v0.2 ✅ |
| ActionDefinitionProvider | `*.sbdef` | `Generated/ActionDefs.*.g.cs` | v0.2 ✅ |
| Marker 类型 ID 常量 | `*.sbdef` | `Generated/UMarkerTypeIds.*.g.cs` | v0.3 ✅ |
| Marker 组件骨架 | `*.sbdef` | `Generated/UMarkers.*.g.cs` | v0.3 ✅ |
| MarkerDefinitionProvider | `*.sbdef` | `Generated/Editor/UMarkerDefs.*.g.cs` | v0.3 ✅ |

---

## 5. 与现有系统的兼容策略

### 5.1 迁移现状（v0.3 完成后）

- 框架包 `AT` 已移除游戏特定嵌套类（Spawn/Trigger/Vfx），改为非 `partial` ✅
- 用户层 `UAT` 由 `.sbdef` 生成，存放在 `SceneBlueprintUser/Generated/` ✅
- `UActionPortIds` 和 `ActionDefs` 由 v0.2 自动生成 ✅
- 框架层 `ActionPortIds.cs` 游戏特定条目已删除（`SpawnPreset`/`SpawnWave`/`TriggerEnterArea`/`VFXCameraShake`/`VFXScreenFlash`/`VFXShowWarning`）✅
- 手写 Def 文件（`CameraShakeDef.cs`/`ShowWarningDef.cs`/`ScreenFlashDef.cs`/`SpawnWaveDef.cs`/`SpawnPresetDef.cs`/`TriggerEnterAreaDef.cs`）已删除 ✅
- Systems 中的 `ActionPortIds.*` 引用已全部迁移为 `UActionPortIds.*` ✅

### 5.2 Action 注册（IActionDefinitionProvider）

**v0.2 已实现**：`.sbdef` 生成完整 `IActionDefinitionProvider`，已与手写 Def 文件共存（`AutoDiscover` 自动跳过重复 TypeId）。

**v0.2 收尾已完成** ✅：
1. codegen 已运行，`ActionDefs.*.g.cs` 编译无误
2. 手写 Def 文件（共 6 个）已删除
3. Systems 中的 `ActionPortIds.*` 已全部替换为 `UActionPortIds.*`，旧 `using SceneBlueprint.Core.Generated` 已移除
4. `ActionPortIds.cs` 游戏特定条目已删除，仅保留框架内置条目（Blackboard/Flow）

### 5.3 Marker 注册（IMarkerDefinitionProvider）

**v0.3 已实现**：`markers.sbdef` 生成 `UMarkerDefs.*.g.cs`，放入 `Generated/Editor/` Editor-only 程序集。`MarkerDefinitionRegistry.AutoDiscover()` 通过 `AppDomain.CurrentDomain.GetAssemblies()` 全局扫描，自动发现新类。

### 5.4 System 不生成

System 中包含业务逻辑（如何响应 Port 值），永远手写。DSL 只管“有什么端口”，不管“端口怎么用”。

---

## 6. 迭代路线

| 版本 | 范围 | 关键交付物 | 状态 |
|---|---|---|---|
| **v0.1** | `action` 关键字，生成 `UAT` 类型常量；用户层 Generated/ 与框架 AT 隔离 | Lexer、Parser、SbdefActionEmitter、SbdefAssetImporter | ✅ 完成 |
| **v0.2** | action 元数据 + port label/min/max；生成 `UActionPortIds`（PropertyKey<T>）+ `ActionDefs`（IActionDefinitionProvider）| SbdefDefEmitter、asmdef 更新 | ✅ 完成 |
| **v0.3** | `marker` 关键字完整解析；生成 `UMarkerTypeIds`/`UMarkers`（Runtime）+ `UMarkerDefs`（Editor-only）；新增 `Generated/Editor/` asmdef | SbdefMarkerEmitter、markers.sbdef | ✅ 完成 |
| **v0.4** | `flow` 关键字支持额外流控端口；IDE .sbdef 语法高亮（TextMate grammar）| `FlowPortDecl`、`SbdefDefEmitter` 扩展、`Documentation~/vscode-sbdef/` | ✅ 完成 |
| **v0.5** | 错误诊断：解析错误显示在 Unity Console，定位到行号 | `SbdefDiagnostic`、`SbdefCodeGen` 错误处理升级 | ✅ 完成 |

---

## 7. 已决策事项

1. **生成物提交 git** ✅  
   生成文件纳入版本控制，CI 直接编译无需额外 codegen 步骤。

2. **`.sbdef` 只在用户层，框架包不使用 DSL** ✅  
   框架内置 Action 保持 C# 手写不变。用户 Action 的 `.sbdef` 放在 `SceneBlueprintUser/Definitions/`。

3. **生成物落在用户项目（方案 B）** ✅  
   生成文件输出到 `SceneBlueprintUser/Generated/`，不修改框架包。类名用 `UAT`/`UActionPortIds` 而非扩展框架的 `AT`/`ActionPortIds`。

4. **port 默认值写入 IActionDefinitionProvider** ✅  
   v0.2 生成 `ActionDefs.*.g.cs` 时，`= 0.5` 写入 `Prop.Float("duration", ..., defaultValue: 0.5f)`。

5. **类型 ID 大小写转换规则** ✅  
   DSL 里的类型 ID 字符串**原样保留**为运行时常量值，类名使用 PascalCase 转换：
   ```
   DSL 写法:   VFX.CameraShake
   常量值:     "VFX.CameraShake"    ← 原样，运行时匹配用
   UAT 类名:   UAT.Vfx.CameraShake  ← VFX → Vfx（全大写缩写首字母大写其余小写）
   PortIds 类名: UActionPortIds.VFXCameraShake ← 点移除，保留原始大小写
   ```
   实现：Emitter 中用 `ToPascalSegment(string s)` 统一处理。

6. **System 永远手写** ✅  
   System 包含响应端口值的业务逻辑，DSL 不生成 System。System 通过 `UActionPortIds.*` 访问 `PropertyKey<T>`。

7. **Marker Editor 生成物放 Editor-only 子目录** ✅（v0.3）  
   `UMarkerDefs.*.g.cs`（`IMarkerDefinitionProvider` 实现）需要引用 `SceneBlueprint.Editor`，因此输出到 `Generated/Editor/`，由独立的 `SceneBlueprintUser.Generated.Editor.asmdef`（`includePlatforms: Editor`）承载，不污染 Runtime 程序集。

8. **多 `.sbdef` 文件的 Marker 产物用 sourceName 区分** ✅（v0.3）  
   生成文件名格式为 `UMarkerTypeIds.{Name}.g.cs`，利用 `partial class` 合并跨文件的类型 ID 常量，避免多 `.sbdef` 文件互相覆盖。

9. **GizmoShape 作注释，不生成 Gizmo 绘制代码** ✅（v0.3）  
   `gizmo sphere(0.4)` 只生成代码注释（`// gizmo: sphere(0.4)`），实际 Gizmo 绘制由用户在 `partial` 文件或 Editor Gizmo Drawer 中自行实现，DSL 不硬绑定渲染方式。

10. **`flow` 端口生成 `const string`，不是 `PropertyKey<T>`** ✅（v0.4）
    `flow OnWaveStart label "波次开始"` 生成 `public const string OnWaveStart = "onWaveStart";`，
    不生成 `PropertyKey<T>`（流控端口不携带数据）。对应 `ActionDefs` 中自动生成 `Port.Out(...)` 条目。

11. **`SbdefDiagnostic` 统一错误输出格式** ✅（v0.5）
    所有解析/生成异常均通过 `SbdefDiagnostic.LogException` 输出，格式为
    `[.sbdef] 文件名(行号): 错误信息`，与 Unity 编译错误格式一致。VS Code 语法高亮文件放在 `Documentation~/vscode-sbdef/`。

---

## 附录：参考对比

| | Framesync `.qtn` | SceneBlueprint `.sbdef` |
|---|---|---|
| 用途 | 游戏状态 Component 定义 | Action 类型 / 端口 / Marker 声明 |
| 生成物 | struct + 序列化 + 访问器 | `UAT`（类型常量）+ `UActionPortIds`（PropertyKey）+ `ActionDefs`（IActionDefinitionProvider）+ Marker 骨架 |
| 复杂度 | 高（F# 实现，支持泛型、继承）| 低（纯 C#，仅需常量 + Provider 生成）|
| 业务逻辑 | System 手写 | System 手写 |
| 生成目标 | 框架包 Generated/ | 用户项目 SceneBlueprintUser/Generated/ |
| 触发机制 | `ScriptedImporter` + 内嵌 `AssetPostprocessor` | 同左（参考 `FSDSLAssetImporter.cs`）|
