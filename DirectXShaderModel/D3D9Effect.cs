using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel;

// fx_2_0 effect structure. Unpublished; names are D3DX9's, layout follows Wine's
// d3dx9_36/effect.c.

public enum D3DXParameterType
{
    Void,
    Bool,
    Int,
    Float,
    String,
    Texture,
    Texture1D,
    Texture2D,
    Texture3D,
    TextureCube,
    Sampler,
    Sampler1D,
    Sampler2D,
    Sampler3D,
    SamplerCube,
    PixelShader,
    VertexShader,
    PixelFragment,
    VertexFragment,
    Unsupported,
}

public enum D3DXParameterClass
{
    Scalar,
    Vector,
    MatrixRows,
    MatrixColumns,
    Object,
    Struct,
}

/// <summary>
/// A parameter, annotation, struct member or state value: a D3DX type, and raw
/// data, objects or sampler states per element.
/// </summary>
public sealed class D3D9EffectValue
{
    public string Name { get; init; }
    public string Semantic { get; init; }
    public D3DXParameterType Type { get; init; }
    public D3DXParameterClass Class { get; init; }
    /// <summary>The array length, or 0 for a value that is not an array.</summary>
    public int Elements { get; init; }
    public int Rows { get; init; }
    public int Columns { get; init; }
    /// <summary>Types only; the values are in the struct's data.</summary>
    public IReadOnlyList<D3D9EffectValue> Members { get; init; } = [];
    public uint Flags { get; init; }
    public IReadOnlyList<D3D9EffectValue> Annotations { get; init; } = [];

    public byte[] Data { get; init; }
    /// <summary>One per element; a texture's has no data.</summary>
    public IReadOnlyList<D3D9EffectObject> Objects { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<D3D9EffectState>> SamplerStates { get; init; } = [];

    public bool IsSampler => Type is >= D3DXParameterType.Sampler and <= D3DXParameterType.SamplerCube;
    public bool IsShader => Type is D3DXParameterType.PixelShader or D3DXParameterType.VertexShader;
    public bool IsTexture => Type is >= D3DXParameterType.Texture and <= D3DXParameterType.TextureCube;

    public int ElementSize => Class == D3DXParameterClass.Struct
        ? SumMembers()
        : Class == D3DXParameterClass.Object ? 0 : 4 * Rows * Columns;

    private int SumMembers()
    {
        int size = 0;
        foreach (D3D9EffectValue member in Members)
        {
            size += member.ElementSize * System.Math.Max(member.Elements, 1);
        }
        return size;
    }
}

public sealed class D3D9EffectObject
{
    public int Id { get; init; }
    public byte[] Data { get; set; }
}

public enum D3D9StateKind
{
    /// <summary>The value written in the state, or NULL for an object state.</summary>
    Constant,
    /// <summary>A shader compiled in the pass.</summary>
    Shader,
    Expression,
    /// <summary>A parameter, named: `PixelShader = (named)` or `Texture = &lt;tex&gt;`.</summary>
    Parameter,
    /// <summary>An element of a parameter array, picked by an expression: `(shaders[i])`.</summary>
    ArraySelector,
}

/// <summary>
/// A state set in a pass or sampler. Operation indexes
/// <see cref="D3D9EffectStates"/>; Index is the stage, sampler or light.
/// </summary>
public sealed class D3D9EffectState
{
    public int Operation { get; init; }
    public int Index { get; init; }
    public D3D9EffectValue Value { get; init; }
    public D3D9StateKind Kind { get; set; }
    public byte[] Shader { get; set; }
    /// <summary>The program of an expression, or the index of an array selector.</summary>
    public byte[] Expression { get; set; }
    public string ParameterName { get; set; }
}

public sealed record D3D9EffectPass(string Name, IReadOnlyList<D3D9EffectValue> Annotations, IReadOnlyList<D3D9EffectState> States);

public sealed record D3D9EffectTechnique(string Name, IReadOnlyList<D3D9EffectValue> Annotations, IReadOnlyList<D3D9EffectPass> Passes);

public sealed class D3D9Effect
{
    public IReadOnlyList<D3D9EffectValue> Parameters { get; init; }
    public IReadOnlyList<D3D9EffectTechnique> Techniques { get; init; }
}
