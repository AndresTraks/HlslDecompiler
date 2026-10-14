namespace HlslDecompiler.Hlsl;

/// <summary>
/// HLSL's base-e `log`.
/// </summary>
public class NaturalLogarithmOperation : ConsumerOperation
{
    public NaturalLogarithmOperation(HlslTreeNode value)
    {
        AddInput(value);
    }

    public override string Mnemonic => "log";
}
