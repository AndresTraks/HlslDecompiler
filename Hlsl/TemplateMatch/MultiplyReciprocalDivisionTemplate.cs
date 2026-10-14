using System.Linq;

namespace HlslDecompiler.Hlsl.TemplateMatch;

// a * (1 / b) or a * rcp(b) is a / b (SM3 has no divide). An rcp only when every
// reader multiplies by it; one also read as a value would stay beside the divide.
public class MultiplyReciprocalDivisionTemplate : NodeTemplate<MultiplyOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply
            && (IsReciprocal(multiply.Factor1) || IsReciprocal(multiply.Factor2));
    }

    private static bool IsReciprocal(HlslTreeNode node)
    {
        return node switch
        {
            DivisionOperation division => ConstantMatcher.IsOne(division.Dividend),
            ReciprocalOperation => node.Outputs.All(reader => reader is MultiplyOperation),
            _ => false,
        };
    }

    private static HlslTreeNode Divisor(HlslTreeNode reciprocal)
    {
        return reciprocal is DivisionOperation division
            ? division.Divisor
            : ((ReciprocalOperation)reciprocal).Value;
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return IsReciprocal(node.Factor1)
            ? new DivisionOperation(node.Factor2, Divisor(node.Factor1))
            : new DivisionOperation(node.Factor1, Divisor(node.Factor2));
    }
}
