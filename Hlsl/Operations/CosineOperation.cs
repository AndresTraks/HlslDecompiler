namespace HlslDecompiler.Hlsl;

public class CosineOperation : ConsumerOperation
{
    public CosineOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "cos";
}
