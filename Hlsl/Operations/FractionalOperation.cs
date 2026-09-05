namespace HlslDecompiler.Hlsl;

public class FractionalOperation : ConsumerOperation
{
    public FractionalOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "frc";

    // HLSL spells the bytecode mnemonic frc as frac.
    public override string HlslFunction => "frac";
}
