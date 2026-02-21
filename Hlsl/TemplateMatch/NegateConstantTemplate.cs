namespace HlslDecompiler.Hlsl.TemplateMatch;

public class NegateConstantTemplate : NodeTemplate<NegateOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is NegateOperation negate && ConstantMatcher.IsConstant(negate.Value);
    }

    public override HlslTreeNode Reduce(NegateOperation node)
    {
        return new ConstantNode(-(node.Value as ConstantNode).Value);
    }
}
