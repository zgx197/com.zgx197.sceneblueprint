#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Editor.CodeGen.Sbdef
{
    internal record SbdefFile(List<SbdefStatement> Statements);

    internal abstract record SbdefStatement;

    /// <summary>action 块内的元数据关键字（displayName / category / description / themeColor / duration）</summary>
    internal record ActionMeta(
        string? DisplayName,
        string? Category,
        string? Description,
        string? ThemeColor,  // "r g b" 原始字符串，例如 "0.2 0.6 1.0"
        string? Duration     // "instant" | "duration" | "passive"
    );

    /// <summary>flow OnWaveStart label "波次开始" — 额外流控输出端口（不携带数据）</summary>
    internal record FlowPortDecl(string PortName, string? Label);

    /// <summary>action VFX.CameraShake { displayName "..." port float Duration = 0.5 label "时长" min 0.1 max 5.0 }</summary>
    internal record ActionDecl(
        string TypeId,
        ActionMeta Meta,
        List<PortDecl> Ports,
        List<FlowPortDecl> FlowPorts,
        List<RequireDecl>? Requirements = null
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
    
    /// <summary>顶层独立 annotation 声明（阶段4）：annotation SpawnConfig { ... }</summary>
    internal record AnnotationDecl(
        string Name,
        string? DisplayName,
        List<FieldDecl> Fields
    ) : SbdefStatement;

    /// <summary>annotation WavePool { field list MonsterEntry { ... } }</summary>
    internal record AnnotationBlockDecl(
        string Name,
        string? DisplayName,
        List<FieldDecl> Fields
    );

    /// <summary>field string MonsterId label "怪物ID" default("") tooltip("...")</summary>
    internal record FieldDecl(
        string TypeName,      // "string" / "int" / "float" / "bool" / "enum" / "list"
        string Name,
        string? Label,
        string? DefaultValue,
        string? Min,
        string? Max,
        string? Tooltip,
        string? ShowIf,
        List<string>? EnumValues,       // 如果是 enum 类型
        List<FieldDecl>? NestedFields   // 如果是 list 类型
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

    /// <summary>require Area SpawnArea label "刷怪区域" required</summary>
    internal record RequireDecl(
        string  MarkerType,   // "Area" / "Point"
        string  PortName,     // 对应的 sceneref port 名称（用作 bindingKey camelCase）
        string? Label,
        bool    IsRequired
    );
}
