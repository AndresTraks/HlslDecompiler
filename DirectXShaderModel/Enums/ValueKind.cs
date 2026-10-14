namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// What an instruction makes of a value. Bits: masks from comparisons, bitwise
/// operators, branch tests. Unknown: mov and movc, which only carry it.
/// </summary>
public enum ValueKind
{
    Unknown,
    Integer,
    Float,
    Bits,
}
