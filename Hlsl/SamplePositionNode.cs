namespace HlslDecompiler.Hlsl;

/// <summary>
/// samplepos, as one component of GetSamplePosition's float2 (chosen by the
/// resource operand's swizzle).
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
