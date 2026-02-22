# SceneBlueprint

面向 Unity 的场景级蓝图编辑框架。提供可视化节点图编辑器用于设计场景行为，并内置以 `.sbdef` DSL 驱动的代码生成管线，自动生成 Action 与 Marker 的类型常量和注册类。

## 功能亮点

- **可视化节点图编辑器** — 拖拽式蓝图编辑，设计场景事件流程
- **Action 系统** — 强类型端口（`PropertyKey<T>`）与流控端口，节点行为由 System 手写实现
- **Marker 系统** — 在场景中放置语义标记（刷怪点、触发区域、相机目标等），支持 Gizmo 预览
- **`.sbdef` DSL** — Action/Marker 定义的唯一数据源（SSOT），保存时自动触发 C# 代码生成
- **运行时解释器** — 基于帧的执行引擎，在运行时解释蓝图图数据

## 运行环境

- Unity 2021.3+
- [`com.zgx197.nodegraph`](https://github.com/zgx197/com.zgx197.nodegraph) 0.1.0+

## 安装

在项目的 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.zgx197.sceneblueprint": "file:../../com.zgx197.sceneblueprint",
    "com.zgx197.nodegraph": "file:../../com.zgx197.nodegraph"
  }
}
```

## DSL 快速入门

在用户工程中创建 `.sbdef` 文件（如 `Assets/Extensions/SceneBlueprintUser/Definitions/`）：

```
action VFX.CameraShake {
    displayName "镜头震动"
    category    "VFX"
    duration    instant

    port float Duration = 0.3 label "时长" min 0.1 max 5.0
    port float Intensity = 1.0 label "强度" min 0.0 max 3.0
}

action Spawn.Wave {
    displayName "波次刷怪"
    category    "Spawn"
    duration    duration

    port string SpawnArea  = "" label "刷新区域"
    port string Waves      = "" label "波次配置"
    flow OnWaveStart label "波次开始"
}

marker SpawnPoint {
    label "刷怪点"
    gizmo sphere(0.4)
}
```

保存文件后，Unity 的 ScriptedImporter 自动触发代码生成：

| 生成文件 | 内容 |
|---|---|
| `UAT.*.g.cs` | Action 类型 ID 常量（`UAT.VFXCameraShake` 等）|
| `UActionPortIds.*.g.cs` | 端口 Key 常量（`PropertyKey<float>` / `const string`）|
| `ActionDefs.*.g.cs` | `IActionDefinitionProvider` 实现，自动注册到 ActionRegistry |
| `UMarkerTypeIds.*.g.cs` | Marker 类型 ID 常量 |
| `UMarkers.*.g.cs` | `SceneMarker` partial 骨架 |
| `Generated/Editor/UMarkerDefs.*.g.cs` | `IMarkerDefinitionProvider` 实现（Editor-only）|

## 语法高亮（VS Code / Windsurf）

`Documentation~/vscode-sbdef/` 目录中包含 `.sbdef` 的 TextMate 语法高亮扩展，安装方式见 [`Documentation~/vscode-sbdef/README.md`](Documentation~/vscode-sbdef/README.md)。

## 架构

```
.sbdef 文件
    └─ SbdefAssetImporter（ScriptedImporter）
           └─ SbdefCodeGen.Run()
                  ├─ SbdefLexer → SbdefParser → SbdefAst
                  ├─ SbdefActionEmitter  → UAT.*.g.cs
                  ├─ SbdefDefEmitter     → UActionPortIds.*.g.cs + ActionDefs.*.g.cs
                  └─ SbdefMarkerEmitter  → UMarkerTypeIds / UMarkers / UMarkerDefs
```

## 文档

完整设计文档见 [`Documentation~/SbdefDSL-Design.md`](Documentation~/SbdefDSL-Design.md)。