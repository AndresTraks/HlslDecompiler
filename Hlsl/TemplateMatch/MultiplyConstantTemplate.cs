namespace HlslDecompiler.Hlsl.TemplateMatch;

public class MultiplyConstantTemplate : NodeTemplate<MultiplyOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply
            && !ConstantMatcher.IsConstant(multiply.Factor1)
            && ConstantMatcher.IsConstant(multiply.Factor2);
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return new MultiplyOperation(node.Factor2, node.Factor1);
    }
}
