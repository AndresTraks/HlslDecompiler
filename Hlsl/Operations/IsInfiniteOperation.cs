namespace HlslDecompiler.Hlsl;

/// <summary>
/// isinf(x). An infinity is an exponent of all ones over a zero fraction, and
/// fxc asks for it by masking the sign away and comparing what is left with the
/// one bit pattern that means it.
/// </summary>
public class IsInfiniteOperation : ConsumerOperation
{
    public IsInfiniteOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "isinf";
}
