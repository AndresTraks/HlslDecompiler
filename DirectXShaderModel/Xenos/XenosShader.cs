using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel.Xenos;

/// <summary>
/// An Xbox 360 shader as XNA Game Studio 3.1 (and the XDK) store it: a
/// big-endian container around the Xenos GPU's microcode, with a D3D9-style
/// constant table, the literal constants the compiler placed in high float
/// registers, and the declarations that bind registers to semantics.
/// </summary>
public sealed class XenosShader
{
    public ShaderType Type { get; }
    public ConstantTable Constants { get; }

    /// <summary>The microcode, one entry per dword: control flow pairs, then three-dword instructions.</summary>
    public IReadOnlyList<uint> Microcode { get; }

    /// <summary>The literal constants' raw bits, four per register.</summary>
    public IReadOnlyList<uint> Literals { get; }

    /// <summary>The float register the first literal is in: c252 down, below the stage's 256.</summary>
    public int LiteralRegister { get; init; }

    /// <summary>
    /// Integer constants the compiler set itself, such as a constant loop's:
    /// the register in the unified file (a pixel shader's start at 16) and its
    /// value, a loop constant as count | start &lt;&lt; 8 | step &lt;&lt; 16.
    /// </summary>
    public IReadOnlyList<(int Register, uint Value)> IntegerLiterals { get; init; } = [];

    public XenosProgramConfig Config { get; init; } = new();

    /// <summary>A pixel shader's interpolated inputs, or a vertex shader's outputs.</summary>
    public IReadOnlyList<XenosDeclaration> Declarations { get; init; } = [];

    /// <summary>A vertex shader's vfetch instructions that read a semantic, which the runtime patches.</summary>
    public IReadOnlyList<XenosVertexInput> VertexInputs { get; init; } = [];

    /// <summary>The register component a vertex shader's vertex index arrives in, if it fetches.</summary>
    public XenosIndexDeclaration? Index { get; init; }

    public string Stage => Type == ShaderType.Vertex ? "vs" : "ps";

    public XenosShader(ShaderType type, ConstantTable constants, IReadOnlyList<uint> microcode, IReadOnlyList<uint> literals)
    {
        Type = type;
        Constants = constants;
        Microcode = microcode;
        Literals = literals;
    }

    public XenosShader With(XenosProgramConfig config, IReadOnlyList<XenosDeclaration> declarations,
        IReadOnlyList<XenosVertexInput> vertexInputs, XenosIndexDeclaration? index) =>
        new(Type, Constants, Microcode, Literals)
        {
            LiteralRegister = LiteralRegister,
            IntegerLiterals = IntegerLiterals,
            Config = config,
            Declarations = declarations,
            VertexInputs = vertexInputs,
            Index = index,
        };
}

/// <summary>
/// The program header fields the assembler takes as config options rather
/// than working out from the microcode: Vs* or Ps* by the stage.
/// </summary>
public sealed record XenosProgramConfig
{
    public int MaxRegister { get; init; }
    public bool Resource { get; init; }
    public bool GenIndex { get; init; }

    /// <summary>The register vPos, vFace and the point sprite coordinates arrive in, in a pixel shader.</summary>
    public int? ParamGenRegister { get; init; }
    public bool GenScreenXY { get; init; }

    /// <summary>A vertex shader's VsExportMode: what it writes to oPts.</summary>
    public int ExportMode { get; init; }
}

/// <summary>A D3DDECLUSAGE semantic on a register: r# in a pixel shader, o# in a vertex shader.</summary>
public readonly record struct XenosDeclaration(DeclUsage Usage, int UsageIndex, int Register, int Mask)
{
    /// <summary>A pixel shader input sampled at the centroid, as the compiler makes every COLOR.</summary>
    public bool IsCentroid { get; init; }
}

/// <summary>The vfetch at an instruction address reads this semantic.</summary>
public readonly record struct XenosVertexInput(DeclUsage Usage, int UsageIndex, int Address);

public readonly record struct XenosIndexDeclaration(int Register, int Component);
