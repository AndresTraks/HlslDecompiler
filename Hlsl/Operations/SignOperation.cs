namespace HlslDecompiler.Hlsl;

// The sign of a value: -1, 0 or 1. The hardware has no such instruction, so fxc
// writes it as the difference of two comparisons against zero.
public class SignOperation : ConsumerOperation
{
    public SignOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "sign";
}
