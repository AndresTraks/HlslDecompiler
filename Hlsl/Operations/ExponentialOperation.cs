namespace HlslDecompiler.Hlsl;

public class ExponentialOperation : ConsumerOperation
{
    public ExponentialOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "exp";

    // Bytecode exp is base 2; HLSL exp is base e.
    public override string HlslFunction => "exp2";
}
