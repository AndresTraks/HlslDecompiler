namespace HlslDecompiler.Hlsl;

/// <summary>
/// samplepos: where in the pixel one sample of a multisampled resource sits.
/// HLSL asks with GetSamplePosition and is handed a float2 back, so the two
/// components are one call; which of them a component is comes from the resource
/// operand's swizzle, the way a sampled texel's channel does.
/// </summary>
public class SamplePositionNode : HlslTreeNode, IHasComponentIndex
{
    public SamplePositionNode(RegisterInputNode resource, HlslTreeNode sampleIndex, int componentIndex)
    {
        AddInput(resource);
        AddInput(sampleIndex);
        ComponentIndex = componentIndex;
    }

    public RegisterInputNode Resource => (RegisterInputNode)Inputs[0];
    public HlslTreeNode SampleIndex => Inputs[1];
    public int ComponentIndex { get; }
}
