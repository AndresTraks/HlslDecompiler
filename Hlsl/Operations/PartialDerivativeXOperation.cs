namespace HlslDecompiler.Hlsl;

/// <summary>
/// How exactly the derivative is taken. Shader model 5 spells out what shader
/// model 4 left to the hardware: a coarse derivative is one value per 2x2 quad
/// and a fine one is per pixel, and they are different instructions.
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
