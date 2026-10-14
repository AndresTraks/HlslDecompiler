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

    // A register per column, or per row when packed row_major.
    private bool IsRowMajor => TypeInfo.ParameterClass == ParameterClass.MatrixRows;
    /// <summary>How many components the register holding one row or column has.</summary>
    public int MatrixRowWidth => IsRowMajor ? TypeInfo.Columns : TypeInfo.Rows;
    /// <summary>The row or column one register of the matrix holds, named.</summary>
    public string MatrixRow(int row)
    {
        return RegisterState.MatrixRegisterName(TypeInfo, Name, row);
    }
    // A float packed after a float3 starts at .w; swizzles are rebased onto it.
    public int ComponentBase => StartOffset % 4;
}
