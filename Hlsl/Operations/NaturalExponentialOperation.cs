namespace HlslDecompiler.Hlsl;

/// <summary>
/// HLSL's `exp`, which is base e. There is no bytecode instruction for it - fxc
/// writes it as the base 2 exp over the value scaled by 1/ln 2 - so this node has
/// no mnemonic of its own and is only ever built by PowerTemplate's neighbours.
/// </summary>
public class NaturalExponentialOperation : ConsumerOperation
{
    public NaturalExponentialOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "exp";
}
