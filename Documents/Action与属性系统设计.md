# Action 与属性系统设计

> 版本：v1.0  
> 日期：2026-02-12  
> 状态：设计阶段  
> 父文档：[场景蓝图系统总体设计](场景蓝图系统总体设计.md)
> doc_status: frozen  
> last_reviewed: 2026-02-15

---

## 1. 概述

Action 与属性系统是 SceneBlueprint SDK（Layer 2）的核心子系统，负责：
- **定义**行动类型（ActionDefinition）
- **注册和管理**行动类型（ActionRegistry）
- **声明**行动属性（PropertyDefinition）
- **存储**属性值（PropertyBag）
- **自动生成** Inspector 属性面板（InspectorGenerator）
- **自动生成**节点内容摘要（ContentRenderer）

核心目标：**新增行动类型只需注册一条 ActionDefinition，零框架修改。**

---

## 2. ActionDefinition（行动定义）

ActionDefinition 是行动类型的元数据描述，用数据声明一种行动"长什么样、有哪些属性"。

### 2.1 数据结构

```csharp
public class ActionDefinition
{
    // ─── 元数据 ───
    public string TypeId;           // 全局唯一，如 "Combat.Spawn", "Presentation.Camera"
    public string DisplayName;      // 编辑器中显示的名称，如 "刷怪", "摄像机控制"
    public string Category;         // 分类，如 "Combat", "Presentation", "Flow"
    public string Description;      // 描述文本
    public Color ThemeColor;        // 节点主题色
    public string Icon;             // 图标标识（可选）

    // ─── 端口声明 ───
    public PortDefinition[] Ports;

    // ─── 属性声明 ───
    public PropertyDefinition[] Properties;

    // ─── 行为标记 ───
    public ActionDuration Duration; // Instant / Duration / Passive
}

public enum ActionDuration
{
    Instant,   // 瞬时行动，执行后立即完成
    Duration,  // 持续行动，有运行状态
    Passive    // 被动行动，条件满足时响应
}
```

### 2.2 PortDefinition（端口定义）

```csharp
public class PortDefinition
{
    public string Id;               // 端口唯一 ID，如 "in", "out", "onComplete"
    public string DisplayName;      // 显示名，如 "输入", "输出", "完成时"
    public PortDirection Direction;  // In / Out
    public PortCapacity Capacity;   // Single / Multiple
}

// 便捷工厂方法
public static class Port
{
    public static PortDefinition FlowIn(string id, string name = "")
        => new PortDefinition { Id = id, DisplayName = name, Direction = PortDirection.In, Capacity = PortCapacity.Multiple };

    public static PortDefinition FlowOut(string id, string name = "")
        => new PortDefinition { Id = id, DisplayName = name, Direction = PortDirection.Out, Capacity = PortCapacity.Single };
}
```

### 2.3 注册示例

```csharp
[ActionType("Combat.Spawn")]
public class SpawnActionDef : IActionDefinitionProvider
{
    public ActionDefinition Define() => new ActionDefinition
    {
        TypeId = "Combat.Spawn",
        DisplayName = "刷怪",
        Category = "Combat",
        ThemeColor = new Color(0.2f, 0.7f, 0.3f),
        Duration = ActionDuration.Duration,
        Ports = new[]
        {
            Port.FlowIn("in"),
            Port.FlowOut("out"),
            Port.FlowOut("onWaveComplete", "波次完成"),
            Port.FlowOut("onAllComplete", "全部完成")
        },
        Properties = new[]
        {
            Prop.AssetRef("template", "怪物模板", typeof(MonsterGroupTemplate)),
            Prop.Enum<TempoType>("tempoType", "节奏类型"),
            Prop.Float("interval", "刷怪间隔", defaultValue: 2f, min: 0.1f, max: 30f,
                        visibleWhen: "tempoType == Interval"),
            Prop.Int("totalWaves", "总波数", defaultValue: 3, min: 1, max: 50,
                      visibleWhen: "tempoType != Instant"),
            Prop.Int("monstersPerWave", "每波数量", defaultValue: 5, min: 1, max: 20),
            Prop.Int("maxAlive", "最大存活数", defaultValue: 10, min: 1, max: 50,
                      category: "约束"),
            Prop.SceneBinding("spawnArea", "刷怪区域", BindingType.Area)
        }
    };
}

[ActionType("Combat.PlacePreset")]
public class PlacePresetActionDef : IActionDefinitionProvider
{
    public ActionDefinition Define() => new ActionDefinition
    {
        TypeId = "Combat.PlacePreset",
        DisplayName = "放置预设怪",
        Category = "Combat",
        ThemeColor = new Color(0.3f, 0.6f, 0.4f),
        Duration = ActionDuration.Instant,
        Ports = new[]
        {
            Port.FlowIn("in"),
            Port.FlowOut("out")
        },
        Properties = new[]
        {
            Prop.AssetRef("template", "怪物模板", typeof(MonsterGroupTemplate)),
            Prop.SceneBinding("presetPoints", "预设点组", BindingType.Transform)
        }
    };
}

[ActionType("Presentation.Camera")]
public class CameraActionDef : IActionDefinitionProvider
{
    public ActionDefinition Define() => new ActionDefinition
    {
        TypeId = "Presentation.Camera",
        DisplayName = "摄像机控制",
        Category = "Presentation",
        ThemeColor = new Color(0.4f, 0.5f, 0.9f),
        Duration = ActionDuration.Duration,
        Ports = new[]
        {
            Port.FlowIn("in"),
            Port.FlowOut("out")
        },
        Properties = new[]
        {
            Prop.Enum<CameraActionType>("action", "摄像机动作"),
            Prop.Float("duration", "持续时间", defaultValue: 1.5f, min: 0.1f, max: 30f),
            Prop.SceneBinding("target", "目标", BindingType.Transform,
                              visibleWhen: "action == LookAt || action == Follow")
        }
    };
}
```

---

## 3. PropertyDefinition（属性定义）

PropertyDefinition 声明一个行动拥有的可编辑字段。这是 Inspector 自动生成和数据序列化的基础。

### 3.1 数据结构

```csharp
public class PropertyDefinition
{
    // ─── 基础 ───
    public string Key;              // 属性键名，如 "interval", "template"
    public string DisplayName;      // 显示名，如 "刷怪间隔"
    public PropertyType Type;       // 属性类型

    // ─── 默认值 ───
    public object DefaultValue;

    // ─── UI 提示 ───
    public string Tooltip;          // 悬停提示
    public string Category;         // Inspector 中的分组（如 "约束", "节奏"）
    public int Order;               // 排列顺序

    // ─── 约束 ───
    public float? Min;              // 数值最小值
    public float? Max;              // 数值最大值
    public string[] EnumOptions;    // 枚举选项（Enum 类型时）
    public Type AssetFilter;        // 资产引用类型过滤（AssetRef 类型时）
    public BindingType? BindingType;// 场景绑定类型（SceneBinding 类型时）

    // ─── 条件可见性 ───
    public string VisibleWhen;      // 条件表达式，如 "tempoType == Interval"

    // ─── AI Director 支持（Phase 2+） ───
    public bool DirectorControllable;  // 是否允许 AI Director 调整
    public float DirectorInfluence;    // AI 调整权限 0~1（0=完全固定，1=完全由AI决定）
}

public enum PropertyType
{
    Float,
    Int,
    Bool,
    String,
    Enum,
    AssetRef,       // Unity 资产引用（MonsterGroupTemplate 等）
    Vector2,
    Vector3,
    Color,
    Tag,            // GameplayTag
    SceneBinding    // 场景对象绑定
}

public enum BindingType
{
    Transform,      // 位置/朝向
    Area,           // 多边形区域
    Path,           // 路径
    Collider        // 碰撞器/触发器
}
```

### 3.2 便捷工厂方法

```csharp
public static class Prop
{
    public static PropertyDefinition Float(string key, string name,
        float defaultValue = 0f, float? min = null, float? max = null,
        string category = null, string visibleWhen = null)
    {
        return new PropertyDefinition
        {
            Key = key, DisplayName = name, Type = PropertyType.Float,
            DefaultValue = defaultValue, Min = min, Max = max,
            Category = category, VisibleWhen = visibleWhen
        };
    }

    public static PropertyDefinition Int(string key, string name,
        int defaultValue = 0, int? min = null, int? max = null,
        string category = null, string visibleWhen = null)
    {
        return new PropertyDefinition
        {
            Key = key, DisplayName = name, Type = PropertyType.Int,
            DefaultValue = defaultValue, Min = min, Max = max,
            Category = category, VisibleWhen = visibleWhen
        };
    }

    public static PropertyDefinition Bool(string key, string name,
        bool defaultValue = false, string visibleWhen = null)
    {
        return new PropertyDefinition
        {
            Key = key, DisplayName = name, Type = PropertyType.Bool,
            DefaultValue = defaultValue, VisibleWhen = visibleWhen
        };
    }

    public static PropertyDefinition String(string key, string name,
        string defaultValue = "", string visibleWhen = null)
    {
        return new PropertyDefinition
        {
            Key = key, DisplayName = name, Type = PropertyType.String,
            DefaultValue = defaultValue, VisibleWhen = visibleWhen
        };
    }

    public static PropertyDefinition Enum<T>(string key, string name,
        string visibleWhen = null) where T : System.Enum
    {
        return new PropertyDefinition
        {
            Key = key, DisplayName = name, Type = PropertyType.Enum,
            DefaultValue = default(T),
            EnumOptions = System.Enum.GetNames(typeof(T)),
            VisibleWhen = visibleWhen
        };
    }

    public static PropertyDefinition AssetRef(string key, string name,
        Type assetType, string visibleWhen = null)
    {
        return new PropertyDefinition
        {
            Key = key, DisplayName = name, Type = PropertyType.AssetRef,
            AssetFilter = assetType, VisibleWhen = visibleWhen
        };
    }

    public static PropertyDefinition SceneBinding(string key, string name,
        BindingType bindingType, string visibleWhen = null)
    {
        return new PropertyDefinition
        {
            Key = key, DisplayName = name, Type = PropertyType.SceneBinding,
            BindingType = bindingType, VisibleWhen = visibleWhen
        };
    }

    public static PropertyDefinition Tag(string key, string name,
        string visibleWhen = null)
    {
        return new PropertyDefinition
        {
            Key = key, DisplayName = name, Type = PropertyType.Tag,
            VisibleWhen = visibleWhen
        };
    }
}
```

---

## 4. ActionRegistry（行动注册表）

### 4.1 接口

```csharp
public interface IActionRegistry
{
    /// <summary>注册一个行动定义</summary>
    void Register(ActionDefinition definition);

    /// <summary>通过 TypeId 获取行动定义</summary>
    ActionDefinition Get(string typeId);

    /// <summary>尝试获取</summary>
    bool TryGet(string typeId, out ActionDefinition definition);

    /// <summary>获取某个分类下的所有行动</summary>
    IReadOnlyList<ActionDefinition> GetByCategory(string category);

    /// <summary>获取所有已注册行动</summary>
    IReadOnlyList<ActionDefinition> GetAll();

    /// <summary>获取所有分类名</summary>
    IReadOnlyList<string> GetCategories();
}
```

### 4.2 自动发现与注册

```csharp
public class ActionRegistry : IActionRegistry
{
    private readonly Dictionary<string, ActionDefinition> _definitions = new();
    private readonly Dictionary<string, List<ActionDefinition>> _byCategory = new();

    /// <summary>
    /// 通过反射扫描所有标注了 [ActionType] 的类，自动注册。
    /// 在编辑器启动时调用一次。
    /// </summary>
    public void AutoDiscover()
    {
        var providerType = typeof(IActionDefinitionProvider);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (providerType.IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var attr = type.GetCustomAttribute<ActionTypeAttribute>();
                    if (attr != null)
                    {
                        var provider = (IActionDefinitionProvider)Activator.CreateInstance(type);
                        Register(provider.Define());
                    }
                }
            }
        }
    }

    // ... Register, Get, GetByCategory 等实现
}
```

### 4.3 标注属性

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ActionTypeAttribute : Attribute
{
    public string TypeId { get; }
    public ActionTypeAttribute(string typeId) { TypeId = typeId; }
}

public interface IActionDefinitionProvider
{
    ActionDefinition Define();
}
```

---

## 5. PropertyBag（属性存储）

节点的属性值存储在 PropertyBag 中，而非引用外部 ScriptableObject。

### 5.1 数据结构

```csharp
public class PropertyBag
{
    private readonly Dictionary<string, object> _values = new();

    public void Set(string key, object value) => _values[key] = value;
    public T Get<T>(string key, T defaultValue = default) 
        => _values.TryGetValue(key, out var v) ? (T)v : defaultValue;
    public bool Has(string key) => _values.ContainsKey(key);
    public void Remove(string key) => _values.Remove(key);

    public IReadOnlyDictionary<string, object> All => _values;
}
```

### 5.2 与 NodeGraph 的集成

```csharp
// Node.UserData 存储 ActionNodeData
public class ActionNodeData
{
    public string ActionTypeId;     // 指向 ActionDefinition.TypeId
    public PropertyBag Properties;  // 属性值

    public ActionNodeData(string typeId)
    {
        ActionTypeId = typeId;
        Properties = new PropertyBag();
    }
}
```

当创建节点时，根据 ActionDefinition 初始化默认值：

```csharp
public static ActionNodeData CreateFromDefinition(ActionDefinition def)
{
    var data = new ActionNodeData(def.TypeId);
    foreach (var prop in def.Properties)
    {
        if (prop.DefaultValue != null)
            data.Properties.Set(prop.Key, prop.DefaultValue);
    }
    return data;
}
```

### 5.3 序列化

PropertyBag 序列化为 JSON 键值对，存储在 NodeGraph 的 Node.UserData 中：

```json
{
  "actionTypeId": "Combat.Spawn",
  "properties": {
    "template": "elite_group_01",
    "tempoType": "Interval",
    "interval": 2.0,
    "totalWaves": 3,
    "monstersPerWave": 5,
    "maxAlive": 10
  }
}
```

---

## 6. Inspector 自动生成（InspectorGenerator）

### 6.1 设计原则

- 根据 ActionDefinition.Properties 自动生成属性编辑面板
- 支持 `VisibleWhen` 条件联动（属性 A 的值决定属性 B 是否显示）
- 按 `Category` 分组，用 Foldout 折叠
- 按 `Order` 排序

### 6.2 渲染流程

```
InspectorGenerator.Draw(ActionDefinition def, PropertyBag bag)
  │
  ├─ 按 Category 分组 PropertyDefinition[]
  │
  ├─ 对每个分组：
  │   ├─ 绘制 Foldout 标题
  │   └─ 对每个属性：
  │       ├─ 评估 VisibleWhen → 是否显示
  │       └─ 根据 PropertyType 选择控件：
  │           ├─ Float → EditorGUILayout.Slider (if min/max) or FloatField
  │           ├─ Int   → IntSlider or IntField
  │           ├─ Bool  → Toggle
  │           ├─ String → TextField
  │           ├─ Enum  → Popup
  │           ├─ AssetRef → ObjectField (filtered by AssetFilter)
  │           ├─ Vector2/3 → VectorField
  │           ├─ Color → ColorField
  │           ├─ Tag   → TagDropdown
  │           └─ SceneBinding → SceneObjectPicker
  │
  └─ 返回是否有值变更（用于标记脏状态）
```

### 6.3 VisibleWhen 条件评估

简单的表达式解析器，支持基本比较：

```
"tempoType == Interval"      → bag.Get("tempoType") == "Interval"
"tempoType != Instant"       → bag.Get("tempoType") != "Instant"
"totalWaves > 1"             → bag.Get<int>("totalWaves") > 1
"action == LookAt || action == Follow"  → OR 逻辑
```

Phase 1 只支持 `==`、`!=`、`>`、`<`、`||`、`&&`，足够覆盖常见联动需求。

---

## 7. 节点内容渲染（ContentRenderer）

### 7.1 自动摘要

根据 ActionDefinition 自动生成节点内摘要文本，替代手写 INodeContentRenderer：

```
默认摘要规则：
  1. 优先显示 AssetRef 属性的资产名
  2. 显示 Enum 属性的当前值
  3. 显示数值属性的关键参数

示例（Spawn 节点）：
  ┌────────────────────────────┐
  │ 🟢 刷怪                    │  ← ActionDefinition.DisplayName
  │ ─────────────────────────  │
  │ 模板: elite_group_01       │  ← AssetRef 属性
  │ 节奏: 间隔 2s × 3波        │  ← Enum + Float + Int
  │ 每波: 5 只                 │  ← Int
  └────────────────────────────┘
```

### 7.2 自定义渲染器（可选）

如果自动摘要不够用，可以为特定 TypeId 注册自定义渲染器：

```csharp
public interface IActionContentRenderer
{
    void DrawContent(Rect area, ActionNodeData data, ActionDefinition def);
}

// 注册
registry.RegisterContentRenderer("Combat.BossPhase", new BossPhaseContentRenderer());
```

---

## 8. 搜索窗集成

从端口拖拽连线或右键菜单创建节点时，弹出搜索窗列出所有可用行动类型：

```
搜索窗内容来自 ActionRegistry：
  ┌─────────────────────────────────┐
  │ 🔍 搜索行动类型...              │
  │ ─────────────────────────────── │
  │ ▸ Combat                        │
  │   · 刷怪 (Spawn)                │
  │   · 放置预设怪 (PlacePreset)     │
  │   · 行进间刷怪 (PathSpawn)       │
  │   · Boss阶段 (BossPhase)        │
  │ ▸ Presentation                   │
  │   · 摄像机控制 (Camera)          │
  │   · 视觉特效 (VFX)              │
  │ ▸ Flow                           │
  │   · 延迟 (Delay)                 │
  │   · 条件分支 (Branch)            │
  │   · ...                          │
  └─────────────────────────────────┘
```

按 Category 分组，支持模糊搜索 DisplayName 和 TypeId。

---

## 9. 与 NodeGraph 的映射关系

| SceneBlueprint 概念 | NodeGraph 概念 | 说明 |
|---------------------|---------------|------|
| ActionDefinition | NodeTypeDef | 行动类型 → 节点类型 |
| ActionDefinition.Ports | NodeTypeDef.Ports | 端口声明 |
| ActionNodeData | Node.UserData | 节点数据 |
| PropertyBag | ActionNodeData.Properties | 属性存储 |
| Transition + Condition | Edge + Edge.UserData | 连线 + 条件 |
| ActionRegistry | INodeTypeRegistry | 类型注册表 |

---

## 10. 实施步骤与测试

### 测试基础设施

```
测试目录：Assets/Extensions/SceneBlueprint/Tests/
测试程序集：SceneBlueprint.Tests.asmdef（引用 SceneBlueprint + NUnit）
运行方式：Unity Editor → Test Runner → EditMode
冒烟测试：通过 [MenuItem("SceneBlueprint/Tests/...")] 提供一键验证
```

---

### Step 1：数据类定义（0.5d）

**实现内容：**
- `ActionDefinition`、`PropertyDefinition`、`PortDefinition` 数据类
- `ActionDuration` 枚举、`PropertyType` 枚举、`BindingType` 枚举
- `PortDirection`、`PortCapacity` 枚举

**测试用例：**

```csharp
[Test]
public void ActionDefinition_Create_HasCorrectFields()
{
    var def = new ActionDefinition
    {
        TypeId = "Combat.Spawn",
        DisplayName = "刷怪",
        Category = "Combat",
        Duration = ActionDuration.Duration,
        Ports = new[] { Port.FlowIn("in"), Port.FlowOut("out") },
        Properties = new[] { Prop.Int("count", "数量", defaultValue: 5) }
    };

    Assert.AreEqual("Combat.Spawn", def.TypeId);
    Assert.AreEqual(2, def.Ports.Length);
    Assert.AreEqual(1, def.Properties.Length);
    Assert.AreEqual(5, def.Properties[0].DefaultValue);
}

[Test]
public void PortDefinition_FlowIn_HasCorrectDirection()
{
    var port = Port.FlowIn("in", "输入");
    Assert.AreEqual(PortDirection.In, port.Direction);
    Assert.AreEqual(PortCapacity.Multiple, port.Capacity);
}
```

**通过标准：** 数据类可正常构造，字段赋值和读取无误。

---

### Step 2：ActionRegistry（0.5d）

**实现内容：**
- `IActionRegistry` 接口
- `ActionRegistry` 实现（Register / Get / GetByCategory / GetAll）
- `ActionTypeAttribute` 标注
- `IActionDefinitionProvider` 接口
- `AutoDiscover()` 反射扫描

**测试用例：**

```csharp
[Test]
public void Registry_Register_CanRetrieveByTypeId()
{
    var registry = new ActionRegistry();
    var def = new ActionDefinition { TypeId = "Test.Action", Category = "Test" };
    registry.Register(def);

    Assert.IsTrue(registry.TryGet("Test.Action", out var result));
    Assert.AreEqual("Test.Action", result.TypeId);
}

[Test]
public void Registry_GetByCategory_ReturnsCorrectGroup()
{
    var registry = new ActionRegistry();
    registry.Register(new ActionDefinition { TypeId = "A.1", Category = "A" });
    registry.Register(new ActionDefinition { TypeId = "A.2", Category = "A" });
    registry.Register(new ActionDefinition { TypeId = "B.1", Category = "B" });

    var groupA = registry.GetByCategory("A");
    Assert.AreEqual(2, groupA.Count);
}

[Test]
public void Registry_AutoDiscover_FindsAnnotatedProviders()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();

    // 至少能发现 Flow.Start（内置行动，Step 8 注册后启用）
    // Step 2 阶段：先手动注册一个测试用 Provider 验证机制
    Assert.IsTrue(registry.GetAll().Count >= 0); // 占位，Step 8 后改为 > 0
}
```

**冒烟测试：**

```csharp
[MenuItem("SceneBlueprint/Tests/Step2 - Registry")]
static void SmokeTest_Registry()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();
    foreach (var def in registry.GetAll())
        Debug.Log($"[{def.Category}] {def.TypeId} - {def.DisplayName} ({def.Properties?.Length ?? 0} props)");
    Debug.Log($"共发现 {registry.GetAll().Count} 个行动类型，{registry.GetCategories().Count} 个分类");
}
```

**通过标准：** 注册/查找/分类过滤均正确，AutoDiscover 机制可用。

---

### Step 3：PropertyBag + ActionNodeData（0.5d）

**实现内容：**
- `PropertyBag`（Set / Get\<T\> / Has / Remove / All）
- `ActionNodeData`（ActionTypeId + PropertyBag）
- `ActionNodeData.CreateFromDefinition()` 默认值初始化
- PropertyBag JSON 序列化/反序列化

**测试用例：**

```csharp
[Test]
public void PropertyBag_SetGet_AllTypes()
{
    var bag = new PropertyBag();
    bag.Set("f", 3.14f);
    bag.Set("i", 42);
    bag.Set("b", true);
    bag.Set("s", "hello");

    Assert.AreEqual(3.14f, bag.Get<float>("f"));
    Assert.AreEqual(42, bag.Get<int>("i"));
    Assert.AreEqual(true, bag.Get<bool>("b"));
    Assert.AreEqual("hello", bag.Get<string>("s"));
}

[Test]
public void PropertyBag_GetMissing_ReturnsDefault()
{
    var bag = new PropertyBag();
    Assert.AreEqual(0f, bag.Get<float>("missing"));
    Assert.AreEqual("fallback", bag.Get<string>("missing", "fallback"));
}

[Test]
public void ActionNodeData_CreateFromDefinition_AppliesDefaults()
{
    var def = new ActionDefinition
    {
        TypeId = "Test.X",
        Properties = new[]
        {
            Prop.Int("count", "数量", defaultValue: 5),
            Prop.Float("speed", "速度", defaultValue: 1.5f)
        }
    };
    var data = ActionNodeData.CreateFromDefinition(def);

    Assert.AreEqual("Test.X", data.ActionTypeId);
    Assert.AreEqual(5, data.Properties.Get<int>("count"));
    Assert.AreEqual(1.5f, data.Properties.Get<float>("speed"));
}

[Test]
public void PropertyBag_JsonRoundTrip()
{
    var original = new PropertyBag();
    original.Set("name", "elite");
    original.Set("count", 5);
    original.Set("rate", 2.5f);
    original.Set("active", true);

    string json = PropertyBagSerializer.ToJson(original);
    var restored = PropertyBagSerializer.FromJson(json);

    Assert.AreEqual("elite", restored.Get<string>("name"));
    Assert.AreEqual(5, restored.Get<int>("count"));
    Assert.AreEqual(2.5f, restored.Get<float>("rate"));
    Assert.AreEqual(true, restored.Get<bool>("active"));
}
```

**通过标准：** 所有类型存取正确，JSON 往返无损。

---

### Step 4：Prop 便捷工厂（0.5d）

**实现内容：**
- `Prop` 静态类所有工厂方法（Float / Int / Bool / String / Enum / AssetRef / SceneBinding / Tag）

**测试用例：**

```csharp
[Test]
public void Prop_Float_SetsAllFields()
{
    var p = Prop.Float("interval", "间隔", defaultValue: 2f, min: 0.1f, max: 30f, category: "节奏");

    Assert.AreEqual("interval", p.Key);
    Assert.AreEqual(PropertyType.Float, p.Type);
    Assert.AreEqual(2f, p.DefaultValue);
    Assert.AreEqual(0.1f, p.Min);
    Assert.AreEqual(30f, p.Max);
    Assert.AreEqual("节奏", p.Category);
}

[Test]
public void Prop_Enum_ExtractsOptions()
{
    var p = Prop.Enum<ActionDuration>("duration", "持续类型");

    Assert.AreEqual(PropertyType.Enum, p.Type);
    Assert.Contains("Instant", p.EnumOptions);
    Assert.Contains("Duration", p.EnumOptions);
}

[Test]
public void Prop_SceneBinding_SetsBindingType()
{
    var p = Prop.SceneBinding("area", "区域", BindingType.Area);
    Assert.AreEqual(BindingType.Area, p.BindingType);
}
```

**通过标准：** 每个工厂方法生成的 PropertyDefinition 字段均正确。

---

### Step 5：InspectorGenerator（1d）

**实现内容：**
- `InspectorGenerator.Draw(ActionDefinition, PropertyBag)` → 返回是否有变更
- 每种 PropertyType 对应的 IMGUI 控件映射
- Category 分组 + Foldout

**测试用例：**

```csharp
[Test]
public void InspectorGenerator_GroupsByCategory()
{
    var def = new ActionDefinition
    {
        Properties = new[]
        {
            Prop.Int("a", "A", category: "基础"),
            Prop.Int("b", "B", category: "基础"),
            Prop.Float("c", "C", category: "高级"),
        }
    };

    var groups = InspectorGenerator.GroupProperties(def.Properties);
    Assert.AreEqual(2, groups.Count);             // "基础" 和 "高级"
    Assert.AreEqual(2, groups["基础"].Count);      // a, b
    Assert.AreEqual(1, groups["高级"].Count);      // c
}
```

**冒烟测试：** 在编辑器窗口中选中一个 Spawn 节点，确认 Inspector 面板显示所有属性控件，修改值后节点摘要更新。

**通过标准：** 所有 PropertyType 都有对应控件，分组正确，值修改能回写 PropertyBag。

---

### Step 6：VisibleWhen 条件评估器（0.5d）

**实现内容：**
- `VisibleWhenEvaluator.Evaluate(string expression, PropertyBag bag) → bool`
- 支持 `==`、`!=`、`>`、`<`、`||`、`&&`

**测试用例：**

```csharp
[Test]
public void VisibleWhen_EqualEnum_True()
{
    var bag = new PropertyBag();
    bag.Set("mode", "Interval");
    Assert.IsTrue(VisibleWhenEvaluator.Evaluate("mode == Interval", bag));
}

[Test]
public void VisibleWhen_NotEqual_True()
{
    var bag = new PropertyBag();
    bag.Set("mode", "Burst");
    Assert.IsTrue(VisibleWhenEvaluator.Evaluate("mode != Interval", bag));
}

[Test]
public void VisibleWhen_NumericCompare()
{
    var bag = new PropertyBag();
    bag.Set("waves", 3);
    Assert.IsTrue(VisibleWhenEvaluator.Evaluate("waves > 1", bag));
    Assert.IsFalse(VisibleWhenEvaluator.Evaluate("waves < 1", bag));
}

[Test]
public void VisibleWhen_Or()
{
    var bag = new PropertyBag();
    bag.Set("action", "Follow");
    Assert.IsTrue(VisibleWhenEvaluator.Evaluate("action == LookAt || action == Follow", bag));
}

[Test]
public void VisibleWhen_NullExpression_ReturnsTrue()
{
    var bag = new PropertyBag();
    Assert.IsTrue(VisibleWhenEvaluator.Evaluate(null, bag));
    Assert.IsTrue(VisibleWhenEvaluator.Evaluate("", bag));
}
```

**通过标准：** 所有操作符正确评估，空表达式返回 true（始终可见）。

---

### Step 7：自动摘要 ContentRenderer（0.5d）

**实现内容：**
- `ActionContentSummary.Generate(ActionDefinition, PropertyBag) → string[]`
- 摘要规则：AssetRef 名 → Enum 当前值 → 关键数值参数

**测试用例：**

```csharp
[Test]
public void ContentSummary_ShowsAssetRefFirst()
{
    var def = new ActionDefinition
    {
        Properties = new[]
        {
            Prop.AssetRef("template", "模板", typeof(object)),
            Prop.Int("count", "数量")
        }
    };
    var bag = new PropertyBag();
    bag.Set("template", "elite_group_01");
    bag.Set("count", 5);

    var lines = ActionContentSummary.Generate(def, bag);
    Assert.IsTrue(lines[0].Contains("elite_group_01")); // AssetRef 优先
}
```

**通过标准：** 摘要包含关键属性信息，顺序合理。

---

### Step 8：Flow 域内置行动注册（0.5d）

**实现内容：**
- `FlowStartDef`（Flow.Start）、`FlowEndDef`（Flow.End）
- `FlowDelayDef`（Flow.Delay）— 属性：duration
- `FlowBranchDef`（Flow.Branch）— 端口：true / false
- `FlowJoinDef`（Flow.Join）— 多输入汇合

**测试用例：**

```csharp
[Test]
public void FlowActions_AllRegistered()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();

    Assert.IsTrue(registry.TryGet("Flow.Start", out _));
    Assert.IsTrue(registry.TryGet("Flow.End", out _));
    Assert.IsTrue(registry.TryGet("Flow.Delay", out _));
    Assert.IsTrue(registry.TryGet("Flow.Branch", out _));
    Assert.IsTrue(registry.TryGet("Flow.Join", out _));
}

[Test]
public void FlowDelay_HasDurationProperty()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();
    var def = registry.Get("Flow.Delay");

    Assert.IsTrue(def.Properties.Any(p => p.Key == "duration" && p.Type == PropertyType.Float));
}

[Test]
public void FlowBranch_HasTrueFalsePorts()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();
    var def = registry.Get("Flow.Branch");

    Assert.IsTrue(def.Ports.Any(p => p.Id == "true" && p.Direction == PortDirection.Out));
    Assert.IsTrue(def.Ports.Any(p => p.Id == "false" && p.Direction == PortDirection.Out));
}
```

**通过标准：** 5 个 Flow 行动全部可通过 AutoDiscover 发现，端口和属性声明正确。

---

### Step 9：Combat 域行动注册 + 端到端验证（1d）

**实现内容：**
- `SpawnActionDef`（Combat.Spawn）
- `PlacePresetActionDef`（Combat.PlacePreset）
- 端到端流程：创建图 → 添加节点 → 设置属性 → 序列化 → 反序列化 → 断言一致

**测试用例：**

```csharp
[Test]
public void CombatSpawn_FullDefinition()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();
    var def = registry.Get("Combat.Spawn");

    Assert.AreEqual("Combat", def.Category);
    Assert.AreEqual(ActionDuration.Duration, def.Duration);
    Assert.IsTrue(def.Ports.Any(p => p.Id == "onWaveComplete"));
    Assert.IsTrue(def.Ports.Any(p => p.Id == "onAllComplete"));
    Assert.IsTrue(def.Properties.Any(p => p.Key == "template"));
    Assert.IsTrue(def.Properties.Any(p => p.Key == "monstersPerWave"));
}

[Test]
public void EndToEnd_CreateGraph_SetProperties_Serialize_Roundtrip()
{
    // 1. 初始化
    var registry = new ActionRegistry();
    registry.AutoDiscover();

    // 2. 创建图并添加节点
    var graph = new Graph();
    var startDef = registry.Get("Flow.Start");
    var spawnDef = registry.Get("Combat.Spawn");

    var startNode = graph.AddNode(/* ... */);
    startNode.UserData = ActionNodeData.CreateFromDefinition(startDef);

    var spawnNode = graph.AddNode(/* ... */);
    var spawnData = ActionNodeData.CreateFromDefinition(spawnDef);
    spawnData.Properties.Set("monstersPerWave", 8);
    spawnNode.UserData = spawnData;

    // 3. 连线
    graph.AddEdge(startNode, "out", spawnNode, "in");

    // 4. 序列化 → 反序列化
    string json = GraphSerializer.Serialize(graph);
    var restored = GraphSerializer.Deserialize(json);

    // 5. 断言
    var restoredSpawn = restored.Nodes[1].UserData as ActionNodeData;
    Assert.AreEqual("Combat.Spawn", restoredSpawn.ActionTypeId);
    Assert.AreEqual(8, restoredSpawn.Properties.Get<int>("monstersPerWave"));
}
```

**冒烟测试：**

```csharp
[MenuItem("SceneBlueprint/Tests/Step9 - E2E")]
static void SmokeTest_EndToEnd()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();

    Debug.Log($"=== 端到端验证 ===");
    Debug.Log($"已注册行动类型: {registry.GetAll().Count}");
    foreach (var cat in registry.GetCategories())
    {
        var actions = registry.GetByCategory(cat);
        Debug.Log($"  [{cat}] {actions.Count} 个: {string.Join(", ", actions.Select(a => a.DisplayName))}");
    }

    // 创建 SpawnAction 数据并往返序列化
    var spawnDef = registry.Get("Combat.Spawn");
    var data = ActionNodeData.CreateFromDefinition(spawnDef);
    data.Properties.Set("monstersPerWave", 8);
    data.Properties.Set("template", "elite_group_01");

    string json = PropertyBagSerializer.ToJson(data.Properties);
    var restored = PropertyBagSerializer.FromJson(json);

    bool pass = restored.Get<int>("monstersPerWave") == 8
             && restored.Get<string>("template") == "elite_group_01";
    Debug.Log($"序列化往返测试: {(pass ? "✅ PASS" : "❌ FAIL")}");
}
```

**通过标准：** 完整的创建→编辑→序列化→反序列化流程无损。

---

### Step 10：搜索窗集成（1d）

**实现内容：**
- 搜索窗数据源从 ActionRegistry 读取
- 按 Category 分组显示
- 模糊搜索 DisplayName 和 TypeId
- 选中后创建对应节点

**测试用例：**

```csharp
[Test]
public void SearchModel_FilterByKeyword()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();

    var model = new ActionSearchModel(registry);
    var results = model.Search("刷怪");

    Assert.IsTrue(results.Any(r => r.TypeId == "Combat.Spawn"));
}

[Test]
public void SearchModel_FilterByCategory()
{
    var registry = new ActionRegistry();
    registry.AutoDiscover();

    var model = new ActionSearchModel(registry);
    var combatActions = model.GetByCategory("Combat");

    Assert.IsTrue(combatActions.Count >= 2); // Spawn + PlacePreset
}
```

**冒烟测试：** 在编辑器中拖拽连线或右键→弹出搜索窗→输入"刷"→显示 Spawn→选中创建节点。

**通过标准：** 搜索窗正确展示所有 Action 类型，模糊搜索过滤正常，选中后节点创建成功。

---

### Phase 1 整体通过标准

```
全部 Step 1~10 单元测试通过（Unity Test Runner 绿色）
Step 2 冒烟测试：Console 打印所有已注册行动
Step 9 冒烟测试：端到端序列化往返 ✅ PASS
编辑器冒烟测试：可打开蓝图编辑器 → 创建节点 → 连线 → Inspector 编辑属性
```

---

## 11. 相关文档

- [场景蓝图系统总体设计](场景蓝图系统总体设计.md)
- [数据导出与运行时契约](数据导出与运行时契约.md)
- [AI Director设计](_archive/AI%20Director设计.md)
