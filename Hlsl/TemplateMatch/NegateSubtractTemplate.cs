namespace HlslDecompiler.Hlsl.TemplateMatch;

public class NegateSubtractTemplate : NodeTemplate<NegateOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is NegateOperation negate && negate.Value is SubtractOperation;
    }

    public override HlslTreeNode Reduce(NegateOperation node)
    {
        SubtractOperation subtract = node.Value as SubtractOperation;
        return new SubtractOperation(subtract.Subtrahend, subtract.Minuend);
    }
}
