namespace HlslDecompiler.Hlsl;

public class ArcTangentOperation : ConsumerOperation
{
    public ArcTangentOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "atan";
}
