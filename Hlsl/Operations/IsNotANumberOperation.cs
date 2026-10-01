namespace HlslDecompiler.Hlsl;

/// <summary>
/// isnan(x). fxc writes it as the one test that tells a NaN from every other
/// float: a NaN is the only value that is not equal to itself, so `ne r0.x,
/// r0.x, r0.x` asks the question and needs no constant to ask it with.
/// </summary>
public class IsNotANumberOperation : ConsumerOperation
{
    public IsNotANumberOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "isnan";
}
