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

    // ld_raw: the address is a byte offset into a buffer with no elements, and
    // reads out as Load, Load2, Load3 or Load4 rather than a subscript.
    public bool IsRaw { get; init; }

    // The offset within the element, from the instruction rather than from the node
    // the operand became: what that node is depends on how the graph was reduced,
    // and this is a fact about the load.
    public int ElementByteOffset { get; init; }

    // Whether what it reads is an integer, from the element type the reflection
    // data gives the buffer - or from the member the byte offset falls in, where
    // the element is a struct. Null where nothing says, which leaves the value to
    // be typed by its readers.
    public bool? IsIntegerElement { get; init; }

    public override string Mnemonic => IsRaw ? "ld_raw" : "ld_structured";
}
