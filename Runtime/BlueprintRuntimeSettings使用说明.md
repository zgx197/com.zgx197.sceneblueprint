# BlueprintRuntimeSettings 使用说明

## 快速开始

### 方法 1：使用菜单（推荐）
1. 在 Unity 菜单栏点击 `SceneBlueprint → 打开运行时设置`
2. 如果配置文件不存在，会自动创建并打开
3. 在 Inspector 中修改配置项

### 方法 2：手动创建
1. 在 Unity Project 窗口中，右键点击 `Assets/Extensions/SceneBlueprint/Resources` 目录（如果没有则先创建）
2. 选择 `Create → SceneBlueprint → Runtime Settings`
3. 将文件命名为 `SceneBlueprintRuntimeSettings`（必须是这个名字）

## 配置文件位置

- **路径**：`Assets/Extensions/SceneBlueprint/Resources/SceneBlueprintRuntimeSettings.asset`
- **加载方式**：通过 `Resources.Load("SceneBlueprint/SceneBlueprintRuntimeSettings")` 加载
- **注意**：必须放在 Resources 目录下才能被运行时加载

## 配置项说明

### 逻辑帧配置

#### Target Tick Rate（目标逻辑帧率）
- **默认值**：10
- **含义**：每秒执行多少个逻辑 Tick
- **示例**：
  - 10 = 每秒 10 个 Tick（适合回合制、策略游戏）
  - 30 = 每秒 30 个 Tick（适合动作游戏）
  - 60 = 每秒 60 个 Tick（适合快节奏游戏）

#### Ticks Per Frame（每帧 Tick 数）
- **默认值**：0（自动模式）
- **含义**：
  - 0 = 自动模式，根据 Target Tick Rate 和实际帧率动态计算
  - >0 = 手动模式，每个 Unity 渲染帧固定执行指定数量的 Tick
- **推荐**：使用自动模式（0），让系统根据实际帧率自动调整

### 测试配置

#### Auto Run In Test Scene（测试场景自动执行）
- **默认值**：true
- **含义**：测试场景启动时是否自动加载并执行蓝图
- **用途**：快速测试时设为 true，调试时可设为 false 手动控制

#### Max Ticks Limit（最大 Tick 限制）
- **默认值**：1000
- **含义**：编辑器测试窗口中"加载并执行"的最大 Tick 数，防止死循环
- **建议**：根据蓝图复杂度调整，简单蓝图可设为 100-500

#### Batch Tick Count（批量 Tick 数）
- **默认值**：10
- **含义**：编辑器测试窗口中"执行 N Ticks"按钮的默认值
- **用途**：调试时快速跳过多个 Tick

### 调试配置

#### Enable Detailed Logs（启用详细日志）
- **默认值**：true
- **含义**：是否输出 BlueprintLoader、TransitionSystem 等的详细日志
- **建议**：开发阶段设为 true，发布前设为 false

#### Log System Execution（记录 System 执行）
- **默认值**：false
- **含义**：是否记录每个 System 的执行信息（用于性能分析）
- **用途**：性能优化时临时开启

### 性能配置

#### Show Performance Stats（显示性能统计）
- **默认值**：false
- **含义**：在测试场景中显示性能统计信息
- **用途**：性能分析时开启

## 使用示例

### 场景 1：快速测试（推荐配置）
```
Target Tick Rate: 10
Ticks Per Frame: 0 (自动)
Auto Run In Test Scene: true
Enable Detailed Logs: true
```

### 场景 2：性能分析
```
Target Tick Rate: 60
Ticks Per Frame: 0 (自动)
Log System Execution: true
Show Performance Stats: true
```

### 场景 3：慢速调试
```
Target Tick Rate: 1
Ticks Per Frame: 1 (手动)
Auto Run In Test Scene: false
Enable Detailed Logs: true
```

## 代码访问

```csharp
// 获取配置实例
var settings = BlueprintRuntimeSettings.Instance;

// 读取配置
int tickRate = settings.TargetTickRate;
bool autoRun = settings.AutoRunInTestScene;

// 计算每帧 Tick 数
int ticksPerFrame = settings.CalculateTicksPerFrame(Application.targetFrameRate);
```

## 注意事项

1. **配置文件必须放在 `Assets/Extensions/SceneBlueprint/Resources/` 目录下**
2. **文件名必须是 `SceneBlueprintRuntimeSettings`**（不含扩展名）
3. 如果未找到配置文件，系统会使用默认配置并输出警告
4. 修改配置后无需重启 Unity，但需要重新加载蓝图才能生效
5. 使用菜单 `SceneBlueprint → 打开运行时设置` 可以快速访问配置文件
