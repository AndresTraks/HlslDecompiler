namespace HlslDecompiler.Hlsl;

public class ArcCosineOperation : ConsumerOperation
{
    public ArcCosineOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "acos";
}
