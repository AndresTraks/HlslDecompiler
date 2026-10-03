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

    // And whether that integer is unsigned. HLSL reads `a < b` and `a >> b` as
    // signed or not from the type of the operands, so a value the bytecode only
    // ever uses unsigned has to be declared uint or be cast at every use.
    public bool IsUnsigned { get; set; }

    public string IntegerTypeName => IsUnsigned ? "uint" : "int";

    // Whether that integer is a float's bits rather than a number, which decides
    // what a float reader of it gets: a reinterpretation, not a conversion.
    public bool IsBits { get; set; }

    // Declared double where the register it stands for holds one. Left as a float
    // the variable threw the precision away at the assignment, and every double the
    // value went on to be used in was computed in floats from there on.
    public bool IsDouble { get; set; }

    // Declared half where every write of it is a partial precision result - a D3D9
    // `_pp` destination. The declaration then says the precision the writes were
    // asking for, and the cast each of them carried comes off.
    public bool IsHalf { get; set; }

    // Declared bool where it only ever holds a comparison and only conditions read
    // it. A comparison is a mask - all ones or all zeroes - and an int variable
    // given one is normalised to 0 or 1 on the way in, which is an `and` the
    // bytecode did not have: a double compare feeding a select costs that
    // instruction and nothing else. A bool keeps the mask, because HLSL never asks
    // what a bool's bits are.
    //
    // Not the same question as IsInteger, which stays as it was: what the value is
    // made of decides the casts around it, and only the declaration changes here.
    public bool IsBool { get; set; }

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
