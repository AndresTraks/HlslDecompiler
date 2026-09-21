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

    // A matrix takes a register per column, or per row where the constant table
    // says it was packed by row - the same rule a matrix outside a struct follows,
    // and one copy of it.
    private bool IsRowMajor => TypeInfo.ParameterClass == ParameterClass.MatrixRows;
    /// <summary>How many components the register holding one row or column has.</summary>
    public int MatrixRowWidth => IsRowMajor ? TypeInfo.Columns : TypeInfo.Rows;
    /// <summary>The row or column one register of the matrix holds, named.</summary>
    public string MatrixRow(int row)
    {
        return RegisterState.MatrixRegisterName(TypeInfo, Name, row);
    }
    // The component of its register the member starts at: a float packed after a
    // float3 sits at .w, and a swizzle naming it is rebased onto the member.
    public int ComponentBase => StartOffset % 4;
}
