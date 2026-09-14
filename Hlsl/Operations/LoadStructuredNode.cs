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

    public override string Mnemonic => IsRaw ? "ld_raw" : "ld_structured";
}
