namespace HlslDecompiler.Hlsl;

public class NegateOperation : ConsumerOperation
{
    public NegateOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "-";
}
