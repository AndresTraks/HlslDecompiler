namespace HlslDecompiler.Hlsl;

/// <summary>
/// isnan(x), which fxc writes as `x != x`.
/// </summary>
public class IsNotANumberOperation : ConsumerOperation
{
    public IsNotANumberOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "isnan";
}
