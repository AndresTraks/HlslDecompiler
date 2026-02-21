namespace HlslDecompiler.Hlsl.TemplateMatch;

public class NegateNegateTemplate : NodeTemplate<NegateOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is NegateOperation negate && negate.Value is NegateOperation;
    }

    public override HlslTreeNode Reduce(NegateOperation node)
    {
        return node.Value;
    }
}
