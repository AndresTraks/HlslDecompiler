namespace HlslDecompiler.Hlsl.TemplateMatch;

public class MultiplyOneTemplate : NodeTemplate<MultiplyOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply &&
            (ConstantMatcher.IsOne(multiply.Factor1) || ConstantMatcher.IsOne(multiply.Factor2));
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return ConstantMatcher.IsOne(node.Factor1)
            ? node.Factor2
            : node.Factor1;
    }
}
