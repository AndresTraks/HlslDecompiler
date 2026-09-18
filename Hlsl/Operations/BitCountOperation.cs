namespace HlslDecompiler.Hlsl;

// countbits: how many bits of an integer are set.
public class BitCountOperation : ConsumerOperation
{
    public BitCountOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "countbits";
}
