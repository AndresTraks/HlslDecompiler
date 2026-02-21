namespace HlslDecompiler.Hlsl;

public class SquareRootOperation : ConsumerOperation
{
    public SquareRootOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "sqrt";
}
