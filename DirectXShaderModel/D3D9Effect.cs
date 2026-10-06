using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel;

// What an fx_2_0 effect holds besides its shaders. Microsoft never published the
// layout; the names here are D3DX9's own (D3DXPARAMETER_TYPE and so on), and the
// layout follows Wine's reading of it in d3dx9_36/effect.c.

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
/// A typed value: a parameter, an annotation, a member of a struct, or the value a
/// state is set to. The type is a D3DX type and class with its dimensions; the
/// value is the raw data of a numeric one, the object of each element of an object
/// one, or the states of each element of a sampler.
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
    /// <summary>A struct's members, as types: their values are in the struct's data.</summary>
    public IReadOnlyList<D3D9EffectValue> Members { get; init; } = [];
    public uint Flags { get; init; }
    public IReadOnlyList<D3D9EffectValue> Annotations { get; init; } = [];

    /// <summary>A numeric value's data, every element and member in order.</summary>
    public byte[] Data { get; init; }
    /// <summary>An object value's objects, one per element: a string, a shader, or nothing for a texture.</summary>
    public IReadOnlyList<D3D9EffectObject> Objects { get; init; } = [];
    /// <summary>A sampler's states, one list per element.</summary>
    public IReadOnlyList<IReadOnlyList<D3D9EffectState>> SamplerStates { get; init; } = [];

    public bool IsSampler => Type is >= D3DXParameterType.Sampler and <= D3DXParameterType.SamplerCube;
    public bool IsShader => Type is D3DXParameterType.PixelShader or D3DXParameterType.VertexShader;
    public bool IsTexture => Type is >= D3DXParameterType.Texture and <= D3DXParameterType.TextureCube;

    /// <summary>The size of one element's data, which a struct's members add up to.</summary>
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

/// <summary>
/// The data an object is given in the effect: a string's text, a shader's
/// bytecode, or none at all.
/// </summary>
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
    /// <summary>An expression the effect evaluates.</summary>
    Expression,
    /// <summary>A parameter, named: `PixelShader = (named)` or `Texture = &lt;tex&gt;`.</summary>
    Parameter,
    /// <summary>An element of a parameter array, picked by an expression: `(shaders[i])`.</summary>
    ArraySelector,
}

/// <summary>
/// One state set in a pass or a sampler: which (an index into D3DX's table of
/// states, see <see cref="D3D9EffectStates"/>), at which index - the stage, the
/// sampler, the light - and to what.
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
