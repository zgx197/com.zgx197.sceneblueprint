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
    internal record ActionDecl(string TypeId, ActionMeta Meta, List<PortDecl> Ports, List<FlowPortDecl> FlowPorts) : SbdefStatement;

    /// <summary>marker Point { label "点标记" gizmo sphere(0.3) }</summary>
    internal record MarkerDecl(
        string  Name,
        string? Label,
        string? GizmoShape,   // "sphere" | "wire_sphere" | "box" | "wire_box"
        string? GizmoParam    // e.g. "0.3" for sphere(0.3)
    ) : SbdefStatement;

    internal record PortDecl(
        string  TypeName,
        string  PortName,
        string? DefaultValue,
        string? Label,
        string? Min,
        string? Max
    );
}
