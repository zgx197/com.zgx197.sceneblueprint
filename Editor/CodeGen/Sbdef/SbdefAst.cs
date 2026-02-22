#nullable enable
using System.Collections.Generic;

namespace SceneBlueprint.Editor.CodeGen.Sbdef
{
    internal record SbdefFile(List<SbdefStatement> Statements);

    internal abstract record SbdefStatement;

    /// <summary>action VFX.CameraShake { port float Duration = 0.5 }</summary>
    internal record ActionDecl(string TypeId, List<PortDecl> Ports) : SbdefStatement;

    /// <summary>marker Point { label "点标记" } — v0.1 解析但不生成代码</summary>
    internal record MarkerDecl(string Name) : SbdefStatement;

    internal record PortDecl(string TypeName, string PortName, string? DefaultValue);
}
