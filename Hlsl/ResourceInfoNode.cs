using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

// One component of resinfo: width, height, depth or array size, mip count.
// GetDimensions returns through out parameters, so the result must be named.
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

    // sampleinfo rather than resinfo; both are one GetDimensions call.
    public bool IsSampleCount { get; init; }

    /// <summary>
    /// bufinfo: one number, no mip level. Structured buffers report element count
    /// and stride, byte address buffers their size in bytes.
    /// </summary>
    public bool IsBuffer { get; init; }

    /// <summary>True for a structured buffer only.</summary>
    public bool ReportsStride { get; init; }

    /// <summary>
    /// Out parameter index of the sample count: 3 rather than 2 on an array, after
    /// the element count.
    /// </summary>
    public int SampleCountComponent { get; init; } = 2;

    // The operand swizzle picks the measurement, except for the sample count whose
    // swizzle is .x.
    public int InfoComponent =>
        IsBuffer ? 0
        : IsSampleCount ? SampleCountComponent
        : Resource.RegisterComponentKey.ComponentIndex;

    // Set when the call statement is hoisted.
    public TempVariableNode NamedAs { get; set; }

    public override string ToString()
    {
        return $"resinfo({Resource}, {MipLevel}).{"xyzw"[ComponentIndex]}";
    }
}
