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

    /// <summary>The sample count of a multisampled texture, from its declaration.</summary>
    public int SampleCount { get; internal set; }
    
    /// <summary>
    /// What one element of a structured buffer holds, when the reflection data says.
    /// dcl_resource_structured carries a stride and nothing about the type, so a
    /// StructuredBuffer&lt;uint&gt; would otherwise be written as one of float.
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
            // A buffer is addressed by one index, and Load takes just that.
            ResourceDimension.Buffer => 1,
            ResourceDimension.Texture1D => 1,
            ResourceDimension.Texture2D => 2,
            ResourceDimension.Texture2Dms => 2,
            ResourceDimension.Texture3D => 3,
            ResourceDimension.TextureCube => 3,
            // An array slice is one coordinate on top of the dimension.
            ResourceDimension.Texture1DArray => 2,
            ResourceDimension.Texture2DArray => 3,
            ResourceDimension.Texture2DmsArray => 3,
            ResourceDimension.TextureCubeArray => 4,
            _ => throw new NotImplementedException(Dimension.ToString()),
        };
    }

    /// <summary>
    /// How many components a typed resource returns: the reflection flags carry
    /// the count less one in two bits, so Buffer&lt;uint&gt; and Buffer&lt;float4&gt;
    /// can be told apart where the declaration in the bytecode says four for both.
    /// </summary>
    public int ReturnComponents =>
        (((int)Flags & (int)(D3DShaderInputFlags.TextureComponent0 | D3DShaderInputFlags.TextureComponent1)) >> 2) + 1;

    /// <summary>The HLSL element type of a typed resource: float4, uint, and so on.</summary>
    public string ReturnTypeName =>
        ReturnComponents > 1 ? ReturnScalarTypeName + ReturnComponents : ReturnScalarTypeName;

    /// <summary>What one component of a typed resource holds, without the width.</summary>
    public string ReturnScalarTypeName => ResourceReturnType switch
    {
        D3DResourceReturnType.SInt => "int",
        D3DResourceReturnType.UInt => "uint",
        _ => "float",
    };

    /// <summary>
    /// The HLSL type a typed unordered access view is declared as. The same as a
    /// texture's with RW in front, except that it always names what it holds: there
    /// is no bare RWTexture2D standing for RWTexture2D&lt;float4&gt; the way a plain
    /// Texture2D stands for its own.
    /// </summary>
    /// <remarks>
    /// As wide as it was declared. RWTexture2D&lt;float&gt; and RWTexture2D&lt;float4&gt;
    /// compile to the same dcl_uav_typed_texture2d (float,float,float,float) and the
    /// same store over an xyzw mask, but the reflection data counts the components
    /// declared, and a host reading the view's type off it - or binding a view of a
    /// one-channel format - saw four where the shader had one.
    /// </remarks>
    public string ReadWriteTypeName =>
        $"RW{Dimension}<{(IsNormalisedReturnType ? NormalisedPrefix + " " : "")}{ReturnTypeName}>";

    /// <summary>
    /// The HLSL type a typed view that an interlocked operation writes is declared
    /// as: the same with a scalar element. HLSL has no interlocked operation over
    /// more than a scalar - one over a <c>RWTexture2D&lt;uint4&gt;</c> element is
    /// refused with "interlocked operations on scalar int or uint data only" - so a
    /// view written by one holds one number, and the four the declaration in the
    /// bytecode names are that number broadcast.
    /// </summary>
    public string ReadWriteAtomicTypeName =>
        $"RW{Dimension}<{(IsNormalisedReturnType ? NormalisedPrefix + " " : "")}{ReturnScalarTypeName}>";

    /// <summary>The HLSL type a texture or buffer resource is declared as.</summary>
    public string TypeName => Dimension switch
    {
        ResourceDimension.Buffer => $"Buffer<{ReturnTypeName}>",
        // A multisampled texture always names its element type, and its sample
        // count where the shader declared one.
        ResourceDimension.Texture2Dms => SampleCount > 0
            ? $"Texture2DMS<{ReturnTypeName}, {SampleCount}>"
            : $"Texture2DMS<{ReturnTypeName}>",
        ResourceDimension.Texture2DmsArray => SampleCount > 0
            ? $"Texture2DMSArray<{ReturnTypeName}, {SampleCount}>"
            : $"Texture2DMSArray<{ReturnTypeName}>",
        // Every other texture names its element type where it holds integers,
        // which the declaration in the bytecode says. A G-buffer read with ld from
        // a Texture2D<uint4> was written as holding floats, so the bits packed into
        // it were anded as floats - X3082 - and the depth stored in it came back as
        // whatever number those bits are. A float texture names it where it is
        // narrower than four: the reflection data counts the components declared,
        // not the ones read - fxc reflects a Texture2D read for its .x alone as
        // float4, and a Texture2D<float> as float - and a depth map declared
        // Texture2D<float> came back as a bare Texture2D, a different declaration
        // to anything that reflects it. A bare Texture2D already means float4.
        // A normalised texture says so, and at four components: dropped,
        // `Texture2D<unorm float4>` came back as a plain Texture2D and the
        // declaration no longer said what the texels are.
        _ => IsNormalisedReturnType ? $"{Dimension}<{NormalisedPrefix} float4>"
            : IsIntegerReturnType || ReturnComponents < 4 ? $"{Dimension}<{ReturnTypeName}>"
            : Dimension.ToString(),
    };

    /// <summary>Whether a typed resource returns floats normalised from integers.</summary>
    public bool IsNormalisedReturnType =>
        ResourceReturnType is D3DResourceReturnType.UNorm or D3DResourceReturnType.SNorm;

    private string NormalisedPrefix =>
        ResourceReturnType == D3DResourceReturnType.UNorm ? "unorm" : "snorm";

    /// <summary>Whether a typed resource returns integers rather than floats.</summary>
    public bool IsIntegerReturnType => ResourceReturnType is D3DResourceReturnType.SInt or D3DResourceReturnType.UInt;

    public override string ToString()
    {
        return $"{ShaderInputType} {Name}";
    }
}
