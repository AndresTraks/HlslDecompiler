namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// min(max(x, low), high) is what HLSL defines clamp as, so writing it back as
/// clamp says the same thing in the words the shader used.
/// </summary>
public class ClampTemplate : NodeTemplate<MinimumOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MinimumOperation minimum && GetMaximum(minimum) != null;
    }

    public override HlslTreeNode Reduce(MinimumOperation node)
    {
        MaximumOperation maximum = GetMaximum(node);
        HlslTreeNode high = ReferenceEquals(node.Value1, maximum) ? node.Value2 : node.Value1;
        return new ClampOperation(maximum.Value1, maximum.Value2, high);
    }

    private static MaximumOperation GetMaximum(MinimumOperation minimum)
    {
        return minimum.Value1 as MaximumOperation ?? minimum.Value2 as MaximumOperation;
    }
}
