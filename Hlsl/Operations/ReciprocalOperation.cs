namespace HlslDecompiler.Hlsl;

public class ReciprocalOperation : ConsumerOperation
{
    public ReciprocalOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "rcp";
}
