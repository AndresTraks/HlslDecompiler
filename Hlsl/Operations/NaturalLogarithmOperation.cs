namespace HlslDecompiler.Hlsl;

/// <summary>
/// HLSL's `log`, which is base e. As with the exponential, fxc has only the base 2
/// instruction and scales its result by ln 2.
/// </summary>
public class NaturalLogarithmOperation : ConsumerOperation
{
    public NaturalLogarithmOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "log";
}
