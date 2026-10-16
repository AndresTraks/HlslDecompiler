namespace HlslDecompiler.Hlsl;

public class LoadStructuredNode : Operation
{
    public LoadStructuredNode(HlslTreeNode address, HlslTreeNode byteOffset, HlslTreeNode value)
    {
        AddInput(address);
        AddInput(byteOffset);
        AddInput(value);
    }

    public HlslTreeNode Address => Inputs[0];
    public HlslTreeNode ByteOffset => Inputs[1];
    public HlslTreeNode Value => Inputs[2];

    // ld_raw: the address is a byte offset; written as Load/Load2/Load3/Load4.
    public bool IsRaw { get; init; }

    // Raw groupshared: HLSL has no raw groupshared, so it is written as a
    // subscript with an element address, but still reads bits.
    public bool IsGroupShared { get; init; }

    // From the instruction, not the operand node, which depends on reduction.
    public int ElementByteOffset { get; init; }

    // Where the member is looked up, taken while a computed offset's constant part
    // is still in sight: a variable later holds `(j << 2) + 4` whole.
    public int MemberByteOffset { get; init; }

    // From reflection (the element, or the struct member at the offset); null if
    // unknown, leaving the value to be typed by its readers.
    public bool? IsIntegerElement { get; init; }

    // From the same member: atomics and comparisons pick signedness from the
    // type, and an atomic's operand bypasses the node graph's readers.
    public bool? IsUnsignedElement { get; init; }

    public override string Mnemonic => IsRaw ? "ld_raw" : "ld_structured";
}
