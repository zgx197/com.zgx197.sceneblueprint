# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
