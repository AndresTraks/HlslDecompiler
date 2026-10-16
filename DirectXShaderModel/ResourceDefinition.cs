using System;

namespace HlslDecompiler.DirectXShaderModel;

public class ResourceDefinition
{
    public string Name { get; }
    public D3DShaderInputType ShaderInputType { get; }
    public D3DResourceReturnType ResourceReturnType { get; }
    public int ResourceViewDimension { get; }
    public int NumSamples { get; }
    public int BindPoint { get; }
    public int BindCount { get; }
    public D3DShaderInputFlags Flags { get; }
    public ResourceDimension Dimension { get; internal set; }

    public int SampleCount { get; internal set; }

    /// <summary>
    /// A structured buffer's element type, from reflection data; the declaration
    /// carries only a stride.
    /// </summary>
    public ShaderTypeInfo ElementType { get; internal set; }

    public ResourceDefinition(
        string name, 
        D3DShaderInputType shaderInputType, 
        D3DResourceReturnType resourceReturnType, 
        int resourceViewDimension, 
        int numSamples, 
        int bindPoint, 
        int bindCount,
        D3DShaderInputFlags flags)
    {
        Name = name;
        ShaderInputType = shaderInputType;
        ResourceReturnType = resourceReturnType;
        ResourceViewDimension = resourceViewDimension;
        NumSamples = numSamples;
        BindPoint = bindPoint;
        BindCount = bindCount;
        Flags = flags;
    }

    public int GetDimensionSize()
    {
        return Dimension switch
        {
            ResourceDimension.Buffer => 1,
            ResourceDimension.Texture1D => 1,
            ResourceDimension.Texture2D => 2,
            ResourceDimension.Texture2Dms => 2,
            ResourceDimension.Texture3D => 3,
            ResourceDimension.TextureCube => 3,
            ResourceDimension.Texture1DArray => 2,
            ResourceDimension.Texture2DArray => 3,
            ResourceDimension.Texture2DmsArray => 3,
            ResourceDimension.TextureCubeArray => 4,
            _ => throw new NotImplementedException(Dimension.ToString()),
        };
    }

    // A texel offset has no lane for the array index.
    public int GetOffsetSize()
    {
        return Dimension switch
        {
            ResourceDimension.Texture1DArray => 1,
            ResourceDimension.Texture2DArray or ResourceDimension.Texture2DmsArray => 2,
            ResourceDimension.TextureCubeArray => 3,
            _ => GetDimensionSize(),
        };
    }

    /// <summary>
    /// How many components a typed resource returns: the reflection flags hold the
    /// count less one; the bytecode declaration always says four.
    /// </summary>
    public int ReturnComponents =>
        (((int)Flags & (int)(D3DShaderInputFlags.TextureComponent0 | D3DShaderInputFlags.TextureComponent1)) >> 2) + 1;

    public string ReturnTypeName =>
        ReturnComponents > 1 ? ReturnScalarTypeName + ReturnComponents : ReturnScalarTypeName;

    public string ReturnScalarTypeName => ResourceReturnType switch
    {
        D3DResourceReturnType.SInt => "int",
        D3DResourceReturnType.UInt => "uint",
        _ => "float",
    };

    /// <summary>
    /// The HLSL type of a typed UAV. Always names its element type, as wide as
    /// reflection data declares it.
    /// </summary>
    public string ReadWriteTypeName =>
        $"RW{Dimension}<{(IsNormalisedReturnType ? NormalisedPrefix + " " : "")}{ReturnTypeName}>";

    /// <summary>
    /// The same with a scalar element, for a view an interlocked operation writes:
    /// HLSL allows those on scalar int or uint data only.
    /// </summary>
    public string ReadWriteAtomicTypeName =>
        $"RW{Dimension}<{(IsNormalisedReturnType ? NormalisedPrefix + " " : "")}{ReturnScalarTypeName}>";

    public string TypeName => Dimension switch
    {
        ResourceDimension.Buffer => $"Buffer<{ReturnTypeName}>",
        ResourceDimension.Texture2Dms => SampleCount > 0
            ? $"Texture2DMS<{ReturnTypeName}, {SampleCount}>"
            : $"Texture2DMS<{ReturnTypeName}>",
        ResourceDimension.Texture2DmsArray => SampleCount > 0
            ? $"Texture2DMSArray<{ReturnTypeName}, {SampleCount}>"
            : $"Texture2DMSArray<{ReturnTypeName}>",
        // A bare texture means float4; name the element type when it is integer,
        // normalised, or narrower than four (reflection counts declared components).
        _ => IsNormalisedReturnType ? $"{Dimension}<{NormalisedPrefix} float4>"
            : IsIntegerReturnType || ReturnComponents < 4 ? $"{Dimension}<{ReturnTypeName}>"
            : Dimension.ToString(),
    };

    public bool IsNormalisedReturnType =>
        ResourceReturnType is D3DResourceReturnType.UNorm or D3DResourceReturnType.SNorm;

    private string NormalisedPrefix =>
        ResourceReturnType == D3DResourceReturnType.UNorm ? "unorm" : "snorm";

    public bool IsIntegerReturnType => ResourceReturnType is D3DResourceReturnType.SInt or D3DResourceReturnType.UInt;

    public override string ToString()
    {
        return $"{ShaderInputType} {Name}";
    }
}
