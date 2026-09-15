namespace HlslDecompiler.Hlsl.TemplateMatch;

// a * (1 / b) is a / b, whichever side the reciprocal is on: `mul r0, r0, r2.x`
// with r2.x an rsq is the normalize it came from only once it is a division.
public class MultiplyReciprocalDivisionTemplate : NodeTemplate<MultiplyOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply
            && (IsReciprocal(multiply.Factor1) || IsReciprocal(multiply.Factor2));
    }

    private static bool IsReciprocal(HlslTreeNode node)
    {
        return node is DivisionOperation division && ConstantMatcher.IsOne(division.Dividend);
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return IsReciprocal(node.Factor1)
            ? new DivisionOperation(node.Factor2, ((DivisionOperation)node.Factor1).Divisor)
            : new DivisionOperation(node.Factor1, ((DivisionOperation)node.Factor2).Divisor);
    }
}
