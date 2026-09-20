namespace HlslDecompiler.Hlsl;

/// <summary>
/// An attribute evaluated somewhere other than where the pixel shader is being
/// run: at one sample of the pixel, or at an offset snapped to the sixteenth of
/// a pixel the hardware works in. Both read an input register that has already
/// been interpolated and interpolate it again elsewhere, so the value is the
/// input's and the place is the second operand.
/// </summary>
public class EvaluateAttributeOperation : Operation
{
    public EvaluateAttributeOperation(HlslTreeNode value, HlslTreeNode[] at, bool isSnapped)
    {
        AddInput(value);
        foreach (HlslTreeNode component in at)
        {
            AddInput(component);
        }
        IsSnapped = isSnapped;
    }

    public HlslTreeNode Value => Inputs[0];

    /// <summary>Whether this is the snapped form, whose place is an offset in two
    /// components rather than a sample index in one.</summary>
    public bool IsSnapped { get; }

    public override string Mnemonic => IsSnapped ? "eval_snapped" : "eval_sample_index";

    public string HlslName =>
        IsSnapped ? "EvaluateAttributeSnapped" : "EvaluateAttributeAtSample";
}
