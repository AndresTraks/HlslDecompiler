namespace HlslDecompiler.Hlsl;

/// <summary>
/// isfinite(x). A float is finite unless its exponent is all ones, which is what
/// both an infinity and a NaN have, so fxc keeps the exponent field alone and
/// asks whether it is anything else.
/// </summary>
public class IsFiniteOperation : ConsumerOperation
{
    public IsFiniteOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "isfinite";
}
