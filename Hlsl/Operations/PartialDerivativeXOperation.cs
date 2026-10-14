namespace HlslDecompiler.Hlsl;

/// <summary>
/// SM5 coarse (per quad) or fine (per pixel) derivative; SM4 leaves it unspecified.
/// </summary>
public enum DerivativePrecision
{
    Unspecified,
    Coarse,
    Fine,
}

public class PartialDerivativeXOperation : ConsumerOperation
{
    public PartialDerivativeXOperation(HlslTreeNode value,
        DerivativePrecision precision = DerivativePrecision.Unspecified)
    {
        AddInput(value);
        Precision = precision;
    }

    public DerivativePrecision Precision { get; }

    public override string Mnemonic => Precision switch
    {
        DerivativePrecision.Coarse => "ddx_coarse",
        DerivativePrecision.Fine => "ddx_fine",
        _ => "ddx",
    };
}
