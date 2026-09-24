namespace HlslDecompiler.Hlsl;

/// <summary>
/// sampleinfo over the rasterizer rather than a resource: how many samples the
/// render target being drawn to has. HLSL asks with GetRenderTargetSampleCount,
/// which is a function of its own rather than a method on anything, and answers
/// one number that every component of the destination is written with - so this
/// is a value in its own right and not a component of a GetDimensions call, the
/// way the sample count of a resource is.
/// </summary>
public class RenderTargetSampleCountNode : HlslTreeNode, IHasComponentIndex
{
    public RenderTargetSampleCountNode(int componentIndex)
    {
        ComponentIndex = componentIndex;
    }

    public int ComponentIndex { get; }
}
