namespace HlslDecompiler.Hlsl;

/// <summary>
/// bfi: a run of bits of one value put into another in place of the bits that were
/// there. Which bits is a width and an offset. HLSL has no intrinsic for it, so it
/// is written as the masks it means.
/// </summary>
public class BitFieldInsertOperation : Operation
{
    public BitFieldInsertOperation(HlslTreeNode width, HlslTreeNode offset, HlslTreeNode insert, HlslTreeNode value)
    {
        AddInput(width);
        AddInput(offset);
        AddInput(insert);
        AddInput(value);
    }

    public HlslTreeNode Width => Inputs[0];
    public HlslTreeNode Offset => Inputs[1];
    public HlslTreeNode Insert => Inputs[2];
    public HlslTreeNode Value => Inputs[3];

    public override string Mnemonic => "bfi";
}
