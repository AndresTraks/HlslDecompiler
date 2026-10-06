namespace HlslDecompiler.Hlsl;

public class ArcSineOperation : ConsumerOperation
{
    public ArcSineOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "asin";
}
