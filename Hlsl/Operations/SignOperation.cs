namespace HlslDecompiler.Hlsl;

// sign(x); no instruction, recovered by SignTemplate.
public class SignOperation : ConsumerOperation
{
    public SignOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "sign";
}
