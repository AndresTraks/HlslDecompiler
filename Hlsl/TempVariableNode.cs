using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

public class TempVariableNode : HlslTreeNode, IHasComponentIndex
{
    public int? DeclarationIndex { get; set; }
    public int ComponentIndex { get; set; }
    public int? VariableSize { get; set; }

    public bool IsInteger { get; set; }

    // HLSL takes signedness of `<` and `>>` from the operand type, so values used
    // only unsigned are declared uint.
    public bool IsUnsigned { get; set; }

    public string IntegerTypeName => IsUnsigned ? "uint" : "int";

    // A float's bits: float readers reinterpret rather than convert.
    public bool IsBits { get; set; }

    public bool IsDouble { get; set; }

    // Every write is a D3D9 `_pp` result.
    public bool IsHalf { get; set; }

    // Holds only comparisons read only by conditions. An int would normalise the
    // mask to 0 or 1, an extra `and`. Affects only the declaration; IsInteger still
    // decides the casts.
    public bool IsBool { get; set; }

    // Assigned by [precise] instructions: declared precise, so fxc keeps the
    // operations as they are (no mul/add fused into a mad).
    public bool IsPrecise { get; set; }

    public string Qualifier => IsPrecise ? "precise " : "";

    // Holds a whole struct element, as Consume() returns one.
    public RegisterKey StructuredElementOf { get; set; }

    public string TypeName => IsBool
        ? "bool"
        : IsInteger
        ? IntegerTypeName
        : IsDouble ? "double" : IsHalf ? "half" : "float";

    public override string ToString()
    {
        string index = DeclarationIndex?.ToString() ?? string.Empty;
        return $"t{index}.{"xyzw"[ComponentIndex]}";
    }
}
