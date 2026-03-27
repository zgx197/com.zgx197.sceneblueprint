# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] - 2026-03-04

### Fixed
- **MarkdownRenderer**：移除标题渲染中的 `<size>` 标签，改用 `<b><color>` + `■` 前缀区分层级，消除 `wordWrap+richText` 混合字号导致的 IMGUI Layout/Repaint 高度不一致（无限 Repaint 死循环根因）
- **EmbeddingService**：将 `EmbedBatchAsync` 的 `batchSize` 从 20 改为 6，修复 DashScope text-embedding-v4 API `batch size ≤ 10` 的限制导致重建索引失败的问题
- **AIChatPanel**：快捷问题按钮改用 `EditorApplication.delayCall` 延迟执行 `QuickAsk`，修复 `BeginArea + ScrollView` 嵌套中 `GUILayout.Button` 触发的 `"Should not grab hot control"` 警告

### Added
- **MarkdownRenderer**：新增表格渲染支持——自动识别表头行（下一行为 `|---|---|` 分隔行时加粗+青色高亮），数据行用 `│` 分隔，分隔行渲染为 `─────` 装饰线
- **MarkdownRenderer**：新增引用块渲染支持（`> text` → 青绿色 `│` 前缀）
- **MarkdownRenderer**：代码块显示语言标签（如 `[csharp]`），流式输出时自动补充未闭合代码块的 `</color>` 关闭标签
- **AIChatPanel**：Settings 面板新增 Embedding 模型选择器（`DrawEmbeddingModelSelector`）及独立 API Key 配置输入框，对话模型与 Embedding 模型分开管理
- **AIChatPanel**：对话模型 API Key 输入框由 `PasswordField` 改为 `TextField`，便于调试确认 Key 内容

## [0.1.0] - 2026-02-23

### Added
- Initial package release extracted from UnityProjectDigong
- `SceneBlueprint.Contract` — shared data structures (`SceneBlueprintData`)
- `SceneBlueprint.Core` — `ActionDefinition`, `ActionRegistry`, `IActionDefinitionProvider`, port/property system
- `SceneBlueprint.Domain` — domain marker and action domain model
- `SceneBlueprint.SpatialAbstraction` — spatial abstraction layer
- `SceneBlueprint.Application` — compile / validate / export pipeline
- `SceneBlueprint.Runtime` — `BlueprintAsset`, `BlueprintRuntimeSettings`, Markers, Interpreter
- `SceneBlueprint.Editor` — `SceneBlueprintWindow` workbench, `SceneBlueprintProfile`, `ActionRegistry` auto-discovery via `TypeCache`
- `SceneBlueprint.Adapters.Unity2D` / `SceneBlueprint.Adapters.Unity3D` — platform adapters
- Dependency on `com.zgx197.nodegraph >= 0.1.0`
