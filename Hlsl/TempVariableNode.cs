using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

public class TempVariableNode : HlslTreeNode, IHasComponentIndex
{
    public int? DeclarationIndex { get; set; }
    public int ComponentIndex { get; set; }
    public int? VariableSize { get; set; }

    // Declared as an integer when the register it stands for only ever holds one.
    // Bitwise operators need it, and a loop counter reads better for it.
    public bool IsInteger { get; set; }

    // Whether that integer is a float's bits rather than a number, which decides
    // what a float reader of it gets: a reinterpretation, not a conversion.
    public bool IsBits { get; set; }

    public override string ToString()
    {
        string index = DeclarationIndex?.ToString() ?? string.Empty;
        return $"t{index}.{"xyzw"[ComponentIndex]}";
    }
}
