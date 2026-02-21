namespace HlslDecompiler.Hlsl;

public class ExponentialOperation : ConsumerOperation
{
    public ExponentialOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "exp";
}
