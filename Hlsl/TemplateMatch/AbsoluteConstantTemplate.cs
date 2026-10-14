namespace HlslDecompiler.Hlsl.TemplateMatch;

// abs of a constant, as left by unrolled loops over sign().
public class AbsoluteConstantTemplate : NodeTemplate<AbsoluteOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is AbsoluteOperation { Value: ConstantNode };
    }

    public override HlslTreeNode Reduce(AbsoluteOperation node)
    {
        var constant = (ConstantNode)node.Value;
        return constant.Value < 0 || constant.IntegerValue < 0 ? constant.Negated() : constant;
    }
}
