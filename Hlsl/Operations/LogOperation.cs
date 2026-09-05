namespace HlslDecompiler.Hlsl;

public class LogOperation : ConsumerOperation
{
    public LogOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "log";

    // Bytecode log is base 2; HLSL log is natural.
    public override string HlslFunction => "log2";
}
