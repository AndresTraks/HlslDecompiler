namespace HlslDecompiler.Hlsl;

public class SaturateOperation : ConsumerOperation
{
    public SaturateOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "saturate";
}
