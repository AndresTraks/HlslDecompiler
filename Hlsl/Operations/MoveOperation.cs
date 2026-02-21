namespace HlslDecompiler.Hlsl;

public class MoveOperation : ConsumerOperation
{
    public MoveOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "mov";

    public override string ToString()
    {
        return Value.ToString();
    }
}
