namespace HlslDecompiler.Hlsl;

// bfrev: an integer's bits in the opposite order.
public class ReverseBitsOperation : ConsumerOperation
{
    public ReverseBitsOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "bfrev";

    public override string HlslFunction => "reversebits";
}
