namespace HlslDecompiler.Hlsl;

/// <summary>
/// HLSL's base-e `exp`; no instruction of its own, built only by templates.
/// </summary>
public class NaturalExponentialOperation : ConsumerOperation
{
    public NaturalExponentialOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "exp";
}
