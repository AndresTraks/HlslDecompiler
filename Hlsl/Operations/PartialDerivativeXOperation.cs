namespace HlslDecompiler.Hlsl;

public class PartialDerivativeXOperation : ConsumerOperation
{
    public PartialDerivativeXOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "ddx";
}
