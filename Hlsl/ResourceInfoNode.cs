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

    // Which measurement this is: the resource operand's swizzle picks it, the
    // same way a sample's picks the channel.
    public int InfoComponent => Resource.RegisterComponentKey.ComponentIndex;

    // The variable the writer named this into. Set when the call statement is
    // hoisted, and read wherever the node itself is still the root of an output.
    public TempVariableNode NamedAs { get; set; }

    public override string ToString()
    {
        return $"resinfo({Resource}, {MipLevel}).{"xyzw"[ComponentIndex]}";
    }
}
