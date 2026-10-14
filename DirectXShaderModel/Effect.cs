using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel;

// D3D10/11 effect structure, following EffectBinaryFormat.h in FX11.

public enum EffectVariableClass
{
    Numeric = 1,
    Object,
    Struct,
    Interface,
}

public enum EffectScalarType
{
    Float = 1,
    Int,
    UInt,
    Bool,
}

public enum EffectNumericLayout
{
    Scalar = 1,
    Vector,
    Matrix,
}

public enum EffectObjectType
{
    String = 1,
    Blend,
    DepthStencil,
    Rasterizer,
    PixelShader,
    VertexShader,
    GeometryShader,
    GeometryShaderSO,
    Texture,
    Texture1D,
    Texture1DArray,
    Texture2D,
    Texture2DArray,
    Texture2DMS,
    Texture2DMSArray,
    Texture3D,
    TextureCube,
    ConstantBuffer,
    RenderTargetView,
    DepthStencilView,
    Sampler,
    Buffer,
    TextureCubeArray,
    PixelShader5 = 25,
    VertexShader5,
    GeometryShader5,
    ComputeShader5,
    HullShader5,
    DomainShader5,
    RWTexture1D,
    RWTexture1DArray,
    RWTexture2D,
    RWTexture2DArray,
    RWTexture3D,
    RWBuffer,
    ByteAddressBuffer,
    RWByteAddressBuffer,
    StructuredBuffer,
    RWStructuredBuffer,
    RWStructuredBufferAlloc,
    RWStructuredBufferConsume,
    AppendStructuredBuffer,
    ConsumeStructuredBuffer,
}

public sealed class EffectType
{
    public string Name { get; init; }
    public EffectVariableClass Class { get; init; }
    /// <summary>The array length, or 0 for a variable that is not an array.</summary>
    public int Elements { get; init; }
    public int TotalSize { get; init; }
    public int Stride { get; init; }
    public int PackedSize { get; init; }

    public EffectNumericLayout Layout { get; init; }
    public EffectScalarType ScalarType { get; init; }
    public int Rows { get; init; }
    public int Columns { get; init; }
    public bool IsColumnMajor { get; init; }

    public EffectObjectType ObjectType { get; init; }

    public IReadOnlyList<EffectMember> Members { get; init; } = [];

    public bool IsShader => ObjectType is EffectObjectType.VertexShader or EffectObjectType.PixelShader
        or EffectObjectType.GeometryShader or EffectObjectType.GeometryShaderSO
        or EffectObjectType.VertexShader5 or EffectObjectType.PixelShader5 or EffectObjectType.GeometryShader5
        or EffectObjectType.HullShader5 or EffectObjectType.DomainShader5 or EffectObjectType.ComputeShader5;

    public bool IsStateBlock => ObjectType is EffectObjectType.Blend or EffectObjectType.DepthStencil
        or EffectObjectType.Rasterizer or EffectObjectType.Sampler;
}

public sealed record EffectMember(string Name, string Semantic, int Offset, EffectType Type);

/// <summary>Value is packed, without register padding.</summary>
public sealed record EffectAnnotation(string Name, EffectType Type, IReadOnlyList<string> Strings, byte[] Value);

public sealed class EffectConstantBuffer
{
    public string Name { get; init; }
    public int Size { get; init; }
    public bool IsTextureBuffer { get; init; }
    /// <summary>The b register it was declared at, or -1.</summary>
    public int ExplicitBindPoint { get; init; }
    public IReadOnlyList<EffectAnnotation> Annotations { get; init; }
    public IReadOnlyList<EffectNumericVariable> Variables { get; init; }
}

public sealed class EffectNumericVariable
{
    public string Name { get; init; }
    public EffectType Type { get; init; }
    public string Semantic { get; init; }
    public int Offset { get; init; }
    /// <summary>The initializer, packed as an annotation's value is, or null.</summary>
    public byte[] DefaultValue { get; init; }
    public bool HasExplicitBindPoint { get; init; }
    public IReadOnlyList<EffectAnnotation> Annotations { get; init; }
}

/// <summary>Bytecode is null for a NULL shader.</summary>
public sealed class EffectShader
{
    public byte[] Bytecode { get; init; }
    public IReadOnlyList<string> StreamOutDeclarations { get; init; } = [];
    public int RasterizedStream { get; init; }
    public bool HasStreamOut { get; init; }
}

public sealed class EffectObjectVariable
{
    public string Name { get; init; }
    public EffectType Type { get; init; }
    public string Semantic { get; init; }
    public int ExplicitBindPoint { get; init; }
    public IReadOnlyList<EffectAnnotation> Annotations { get; init; }
    /// <summary>A state object's assignments, per element.</summary>
    public IReadOnlyList<IReadOnlyList<EffectAssignment>> Blocks { get; init; } = [];
    public IReadOnlyList<EffectShader> Shaders { get; init; } = [];
    public IReadOnlyList<string> Strings { get; init; } = [];
}

public enum EffectAssignmentKind
{
    Constant = 1,
    Variable,
    ConstantIndex,
    VariableIndex,
    ExpressionIndex,
    Expression,
    InlineShader,
    InlineShader5,
}

public sealed record EffectConstant(EffectScalarType Type, uint Bits);

/// <summary>A state set in a state object or pass; State indexes <see cref="EffectStates"/>.</summary>
public sealed class EffectAssignment
{
    public int State { get; init; }
    public int Index { get; init; }
    public EffectAssignmentKind Kind { get; init; }
    public IReadOnlyList<EffectConstant> Constants { get; init; } = [];
    /// <summary>The variable, or the array, assigned from.</summary>
    public string VariableName { get; init; }
    public int ArrayIndex { get; init; }
    public string IndexVariableName { get; init; }
    public byte[] Expression { get; init; }
    public EffectShader Shader { get; init; }
}

public sealed record EffectPass(string Name, IReadOnlyList<EffectAnnotation> Annotations, IReadOnlyList<EffectAssignment> Assignments);

public sealed record EffectTechnique(string Name, IReadOnlyList<EffectAnnotation> Annotations, IReadOnlyList<EffectPass> Passes);

/// <summary>An fxgroup, or the one unnamed group of an effect without groups.</summary>
public sealed record EffectGroup(string Name, IReadOnlyList<EffectAnnotation> Annotations, IReadOnlyList<EffectTechnique> Techniques);

public sealed class Effect
{
    public uint Tag { get; init; }
    public IReadOnlyList<EffectConstantBuffer> ConstantBuffers { get; init; }
    public IReadOnlyList<EffectObjectVariable> ObjectVariables { get; init; }
    public IReadOnlyList<EffectGroup> Groups { get; init; }

    public bool IsFx5 => Tag == EffectReader.Fx50;

    public string Profile => Tag switch
    {
        EffectReader.Fx40 => "fx_4_0",
        EffectReader.Fx41 => "fx_4_1",
        _ => "fx_5_0",
    };
}
