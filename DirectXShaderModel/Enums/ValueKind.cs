namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// What an instruction makes of a value: an integer, a float, or the bits
/// themselves - a comparison writes a mask, the bitwise operators combine them,
/// and a branch tests them. Unknown for the instructions that carry a value
/// through without looking at it, mov and movc.
/// </summary>
public enum ValueKind
{
    Unknown,
    Integer,
    Float,
    Bits,
}
