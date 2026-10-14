namespace HlslDecompiler.Hlsl;

/// <summary>
/// isinf(x): `(x &amp; 0x7fffffff) == 0x7f800000`.
/// </summary>
public class IsInfiniteOperation : ConsumerOperation
{
    public IsInfiniteOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "isinf";
}
