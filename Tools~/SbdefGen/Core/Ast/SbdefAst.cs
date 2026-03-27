#nullable enable
using System.Collections.Generic;

namespace SbdefGen.Core.Ast;

internal record SbdefFile(List<SbdefStatement> Statements);

internal abstract record SbdefStatement;

/// <summary>顶层 codegen 选项声明：codegen contracts_only</summary>
internal record CodeGenDecl(
    CodeGenOptionKind Option
) : SbdefStatement;

internal enum CodeGenOptionKind
{
    ContractsOnly,
}

/// <summary>action 块内的元数据关键字（displayName / category / description / themeColor / duration）</summary>
internal record ActionMeta(
    string? DisplayName,
    string? Category,
    string? Description,
    string? ThemeColor,  // "r g b" 原始字符串，例如 "0.2 0.6 1.0"
    string? Duration     // "instant" | "duration" | "passive"
);

/// <summary>flow OnWaveStart label "波次开始" — 流控端口声明（flow=输出，inflow=输入）</summary>
internal record FlowPortDecl(string PortName, string? Label, bool IsInput = false);

/// <summary>action VFX.CameraShake { displayName "..." port float Duration = 0.5 label "时长" min 0.1 max 5.0 }</summary>
internal record ActionDecl(
    string TypeId,
    ActionMeta Meta,
    List<PortDecl> Ports,
    List<FlowPortDecl> FlowPorts,
    List<RequireDecl>? Requirements = null,
    List<FieldDecl>? Fields = null
) : SbdefStatement;

/// <summary>marker Point { label "点标记" gizmo sphere(0.3) group "刷怪" use_annotations SpawnConfig }</summary>
internal record MarkerDecl(
    string  Name,
    string? Label,
    string? GizmoShape,   // "sphere" | "wire_sphere" | "box" | "wire_box"
    string? GizmoParam,   // e.g. "0.3" for sphere(0.3)
    string? BaseType,     // e.g. "Area" for extends Area
    List<AnnotationBlockDecl>? Annotations,      // 嵌套式 Annotation（阶段3，已废弃）
    List<EditorToolDecl>? EditorTools,
    List<string>? UsedAnnotations,               // 阶段4：声明式关联（use_annotations SpawnConfig, WavePool）
    string? Group                                // 菜单分组名称，如 "刷怪" / "触发" / "摄像机"
) : SbdefStatement;

/// <summary>顶层公共枚举声明：enum MonsterTag { Normal label "普通怪" ... }</summary>
internal record EnumDecl(
    string Name,
    List<EnumValueDecl> Values
) : SbdefStatement;

/// <summary>枚举值声明：Normal = 0 label "普通怪"</summary>
internal record EnumValueDecl(
    string Name,
    string? Label,
    int? ExplicitValue = null
);

/// <summary>顶层独立 annotation 声明（阶段4）：annotation SpawnConfig { ... }</summary>
internal record AnnotationDecl(
    string Name,
    string? DisplayName,
    List<FieldDecl> Fields
) : SbdefStatement;

/// <summary>annotation WavePool { field list SupplyEntries { ... } }</summary>
internal record AnnotationBlockDecl(
    string Name,
    string? DisplayName,
    List<FieldDecl> Fields
);

/// <summary>field string MonsterId label "怪物ID" default("") tooltip("...")</summary>
internal record FieldDecl(
    string TypeName,      // "string" / "int" / "float" / "bool" / "enum" / "list" / "sceneref"
    string Name,
    string? Label,
    string? DefaultValue,
    string? Min,
    string? Max,
    string? Tooltip,
    string? ShowIf,
    List<string>? EnumValues,       // 内联枚举值（field enum Name { A, B }）
    List<FieldDecl>? NestedFields,  // 如果是 list 类型
    string? EnumRef = null,         // 引用公共枚举名（field enum(MonsterTag) Tag）
    string? SceneRefType = null,    // sceneref(Area) → "Area"
    string? Summary = null,         // list 字段的摘要格式，如 "波次: {count} 波"
    // ── 绑定约束修饰符（仅 sceneref 类型有效） ──
    bool IsRequired = false,                  // required 修饰符
    bool IsExclusive = false,                 // exclusive 修饰符
    List<string>? RequiredAnnotations = null   // annotation("...") 修饰符列表
);

/// <summary>editor_tool PointGenerator { strategy enum {Random, Circle} count int default(4) }</summary>
internal record EditorToolDecl(
    string Name,
    string? DisplayName,
    List<ToolParameterDecl> Parameters,
    string? AutoAddAnnotation
);

/// <summary>strategy enum {Random, Circle} 或 count int default(4) range(1, 50)</summary>
internal record ToolParameterDecl(
    string Name,
    string TypeName,      // "enum" / "int" / "float" / "bool" / "string"
    string? DefaultValue,
    string? Min,
    string? Max,
    List<string>? EnumValues
);

internal record PortDecl(
    string  TypeName,
    string  PortName,
    string? DefaultValue,
    string? Label,
    string? Min,
    string? Max,
    string? SceneRefType    = null,       // sceneref(Area) → "Area"
    string? DataDirection   = null,       // "out"/"in" for outport/inport，null 表示普通属性
    string? Summary         = null,       // list port 摘要格式，如 "波次: {count} 波"
    List<FieldDecl>? NestedPortFields = null  // list port 嵌套字段
);

/// <summary>
/// signal Combat.Monster.Died label "怪物死亡" { param string EntityId label "实体ID" }
/// 信号标签声明——codegen 生成 USignalTags 常量类 + SignalPayloads 工厂类
/// </summary>
internal record SignalDecl(
    string TagPath,            // 信号路径（如 "Combat.Monster.Died"）
    string? Label,
    string? Description,
    List<SignalParamDecl>? Params   // 载荷参数列表（可选）
) : SbdefStatement;

/// <summary>信号载荷参数：param string EntityId label "实体ID"</summary>
internal record SignalParamDecl(
    string TypeName,   // "string" / "int" / "float" / "bool"
    string Name,
    string? Label
);

/// <summary>
/// tagdimension CombatRole { displayName "战斗角色" exclusive values { Frontline label "前锋" ... } }
/// Tag 维度声明——codegen 生成 UTagDimensions 常量类 + TagDimensionDefs 元数据注册
/// </summary>
internal record TagDimensionDecl(
    string Name,                            // 维度 ID（如 "CombatRole"）
    string? DisplayName,                    // 维度显示名（如 "战斗角色"）
    bool IsExclusive,                       // true = 单选（exclusive），false = 多选（multiple）
    List<TagDimensionValueDecl> Values      // 候选值列表
) : SbdefStatement;

/// <summary>Tag 维度候选值声明：Frontline label "前锋"</summary>
internal record TagDimensionValueDecl(
    string Name,                            // 值名称（如 "Frontline"）
    string? Label                           // 显示标签（如 "前锋"）
);

/// <summary>require Area SpawnArea label "刷怪区域" required</summary>
internal record RequireDecl(
    string  MarkerType,   // "Area" / "Point"
    string  PortName,     // 对应的 sceneref port 名称（用作 bindingKey camelCase）
    string? Label,
    bool    IsRequired
);
