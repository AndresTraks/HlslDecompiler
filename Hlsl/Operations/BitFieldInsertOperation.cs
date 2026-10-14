namespace HlslDecompiler.Hlsl;

/// <summary>
/// bfi: insert width bits at offset. No HLSL intrinsic, so written as masks.
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
