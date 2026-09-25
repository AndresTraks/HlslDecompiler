using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// One phase of a hull shader: the slice of bytecode it is, shared declarations and
/// all, and the AST parsed from it. The slice is kept beside the AST because the
/// instruction writer works through instructions rather than statements, and because
/// what an immediate in it means is decided by the phase it sits in.
/// </summary>
public record HullPhase(ShaderModel Shader, HlslAst Ast);

/// <summary>
/// The two functions a hull shader is written as, parsed from the phases its
/// bytecode runs together. The control point phase is null where fxc dropped it: a
/// shader whose control points come out as they went in has no work to do per point,
/// and the bytecode then says nothing about that half at all.
/// </summary>
public class HullShaderAst
{
    public HullPhase ControlPoint { get; }
    public HullPhase PatchConstant { get; }

    public HullShaderAst(HullPhase controlPoint, HullPhase patchConstant)
    {
        ControlPoint = controlPoint;
        PatchConstant = patchConstant;
    }
}
