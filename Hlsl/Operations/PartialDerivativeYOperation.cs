namespace HlslDecompiler.Hlsl;

public class PartialDerivativeYOperation : ConsumerOperation
{
    public PartialDerivativeYOperation(HlslTreeNode value,
        DerivativePrecision precision = DerivativePrecision.Unspecified)
    {
        AddInput(value);
        Precision = precision;
    }

    public DerivativePrecision Precision { get; }

    public override string Mnemonic => Precision switch
    {
        DerivativePrecision.Coarse => "ddy_coarse",
        DerivativePrecision.Fine => "ddy_fine",
        _ => "ddy",
    };
}
