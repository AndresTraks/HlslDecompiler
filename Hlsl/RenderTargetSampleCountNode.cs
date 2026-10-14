namespace HlslDecompiler.Hlsl;

/// <summary>
/// sampleinfo on the rasterizer: GetRenderTargetSampleCount(), a standalone
/// scalar rather than a GetDimensions component.
/// </summary>
public class RenderTargetSampleCountNode : HlslTreeNode, IHasComponentIndex
{
    public RenderTargetSampleCountNode(int componentIndex)
    {
        ComponentIndex = componentIndex;
    }

    public int ComponentIndex { get; }
}
