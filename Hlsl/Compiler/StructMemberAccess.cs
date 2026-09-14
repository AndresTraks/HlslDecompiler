using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// A member of a struct element named in full - `instances[id].world` - with its
/// type and where it starts within the element, counted in floats.
/// </summary>
public sealed record StructMemberAccess(string Name, ShaderTypeInfo TypeInfo, int StartOffset)
{
    public bool IsMatrix => TypeInfo.Rows > 1 && TypeInfo.Columns > 1;
    public int Width => TypeInfo.Rows * TypeInfo.Columns;
    // The component of its register the member starts at: a float packed after a
    // float3 sits at .w, and a swizzle naming it is rebased onto the member.
    public int ComponentBase => StartOffset % 4;
}
