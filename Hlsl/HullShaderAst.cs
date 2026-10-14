using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// One hull shader phase: its bytecode slice (with shared declarations), kept for
/// the instruction writer, and its AST.
/// </summary>
public record HullPhase(ShaderModel Shader, HlslAst Ast);

/// <summary>
/// A hull shader's two functions. ControlPoint is null where fxc dropped a
/// pass-through control point phase.
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
