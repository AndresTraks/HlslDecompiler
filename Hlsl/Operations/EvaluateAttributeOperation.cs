namespace HlslDecompiler.Hlsl;

/// <summary>Where an attribute is evaluated.</summary>
public enum EvaluateAttributeAt
{
    /// <summary>One sample of the pixel, named by an index.</summary>
    Sample,
    /// <summary>An offset from the pixel centre, snapped to the sixteenth of a
    /// pixel the hardware works in.</summary>
    Snapped,
    /// <summary>The centroid of the covered part of the pixel, which takes no
    /// operand to say so.</summary>
    Centroid,
}

/// <summary>
/// An attribute evaluated somewhere other than where the pixel shader is being
/// run. All three read an input register that has already been interpolated and
/// interpolate it again elsewhere, so the value is the input's; the two that take
/// a place have it in the operands after it.
/// </summary>
public class EvaluateAttributeOperation : Operation
{
    public EvaluateAttributeOperation(HlslTreeNode value, HlslTreeNode[] at, EvaluateAttributeAt place)
    {
        AddInput(value);
        foreach (HlslTreeNode component in at)
        {
            AddInput(component);
        }
        Place = place;
    }

    public HlslTreeNode Value => Inputs[0];

    public EvaluateAttributeAt Place { get; }

    public override string Mnemonic => Place switch
    {
        EvaluateAttributeAt.Snapped => "eval_snapped",
        EvaluateAttributeAt.Centroid => "eval_centroid",
        _ => "eval_sample_index",
    };

    public string HlslName => Place switch
    {
        EvaluateAttributeAt.Snapped => "EvaluateAttributeSnapped",
        EvaluateAttributeAt.Centroid => "EvaluateAttributeCentroid",
        _ => "EvaluateAttributeAtSample",
    };
}
