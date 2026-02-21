namespace HlslDecompiler.Hlsl;

public class LogOperation : ConsumerOperation
{
    public LogOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "log";
}
