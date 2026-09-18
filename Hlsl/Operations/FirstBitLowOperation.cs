namespace HlslDecompiler.Hlsl;

// firstbit_lo: the position of the lowest set bit, counted from the lowest, or
// 0xffffffff where none is set. HLSL spells it firstbitlow and counts the same
// way, so the two are the one thing.
public class FirstBitLowOperation : ConsumerOperation
{
    public FirstBitLowOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "firstbitlow";
}
