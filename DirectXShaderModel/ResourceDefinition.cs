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

    public override string ToString()
    {
        return $"{ShaderInputType} {Name}";
    }
}
