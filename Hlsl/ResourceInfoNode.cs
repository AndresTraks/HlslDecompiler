using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

// One component of what resinfo reports about a resource at a mip level: x is the
// width, y the height, z the depth or array size, w the mip count. GetDimensions
// hands these back through out parameters rather than as a value, so the writer
// has to name the result before anything can read a component of it.
public class ResourceInfoNode : HlslTreeNode, IHasComponentIndex
{
    public ResourceInfoNode(RegisterInputNode resource, HlslTreeNode mipLevel, int componentIndex,
        D3D10ResInfoReturnType returnType)
    {
        AddInput(resource);
        AddInput(mipLevel);
        ComponentIndex = componentIndex;
        ReturnType = returnType;
    }

    public RegisterInputNode Resource => (RegisterInputNode)Inputs[0];
    public HlslTreeNode MipLevel => Inputs[1];
    public int ComponentIndex { get; }
    public D3D10ResInfoReturnType ReturnType { get; }

    // Whether this is a sample count from sampleinfo rather than a dimension from
    // resinfo. The two are separate instructions and one GetDimensions call.
    public bool IsSampleCount { get; init; }

    /// <summary>
    /// Whether this is bufinfo rather than resinfo: how many elements a buffer
    /// holds, which is one number and has no mip level to ask it at. HLSL asks with
    /// the same GetDimensions, in the overload the buffer takes - a structured one
    /// reports its element count and its stride, a byte address one its size in
    /// bytes.
    /// </summary>
    public bool IsBuffer { get; init; }

    /// <summary>Whether that buffer reports its stride beside its element count,
    /// which a structured one does and a byte address or a typed one does not.
    /// </summary>
    public bool ReportsStride { get; init; }

    /// <summary>
    /// Which out parameter the sample count is. A multisampled texture reports
    /// width, height and the count; an array of them reports the element count
    /// between, so the sample count is the fourth rather than the third.
    /// </summary>
    public int SampleCountComponent { get; init; } = 2;

    // Which measurement this is: the resource operand's swizzle picks it, the
    // same way a sample's picks the channel. Except for the sample count, whose
    // swizzle is .x like a width's - it is the third out parameter of the overload
    // a multisampled texture takes, so it answers z.
    public int InfoComponent =>
        IsBuffer ? 0
        : IsSampleCount ? SampleCountComponent
        : Resource.RegisterComponentKey.ComponentIndex;

    // The variable the writer named this into. Set when the call statement is
    // hoisted, and read wherever the node itself is still the root of an output.
    public TempVariableNode NamedAs { get; set; }

    public override string ToString()
    {
        return $"resinfo({Resource}, {MipLevel}).{"xyzw"[ComponentIndex]}";
    }
}
