namespace HlslDecompiler.Hlsl;

// firstbit_lo, identical to HLSL firstbitlow.
public class FirstBitLowOperation : ConsumerOperation
{
    public FirstBitLowOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "firstbitlow";
}
