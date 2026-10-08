namespace HlslDecompiler.Hlsl;

/// <summary>What a register component is declared to hold - see
/// <see cref="RegisterState.GetDeclaredType"/>.</summary>
public enum DeclaredType
{
    Unknown,
    Float,
    Int,
    Uint,
    Bool,
}
