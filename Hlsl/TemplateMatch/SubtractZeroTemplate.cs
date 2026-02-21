namespace HlslDecompiler.Hlsl.TemplateMatch;

public class SubtractZeroTemplate : NodeTemplate<SubtractOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is SubtractOperation subtract &&
            ((ConstantMatcher.IsZero(subtract.Minuend) && !ConstantMatcher.IsZero(subtract.Subtrahend))
            || (!ConstantMatcher.IsZero(subtract.Minuend) && ConstantMatcher.IsZero(subtract.Subtrahend)));
    }

    public override HlslTreeNode Reduce(SubtractOperation node)
    {
        if (ConstantMatcher.IsZero(node.Minuend))
        {
            return new NegateOperation(node.Subtrahend);
        }
        return node.Minuend;
    }
}
