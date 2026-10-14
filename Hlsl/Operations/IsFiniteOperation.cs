namespace HlslDecompiler.Hlsl;

/// <summary>
/// isfinite(x): exponent field not all ones.
/// </summary>
public class IsFiniteOperation : ConsumerOperation
{
    public IsFiniteOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "isfinite";
}
