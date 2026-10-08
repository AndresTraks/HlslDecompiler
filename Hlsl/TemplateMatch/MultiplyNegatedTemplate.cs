namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// A constant times a negated value is the negated constant times the value:
/// `0.01 * -length(d)` is `-0.01 * length(d)`. fxc puts the sign on whichever
/// operand of the mad it likes, so the text followed it from one compile to the
/// next; with the sign on the constant, it says the same either way.
/// </summary>
public class MultiplyNegatedTemplate : NodeTemplate<MultiplyOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply &&
            ((multiply.Factor1 is ConstantNode && multiply.Factor2 is NegateOperation) ||
            (multiply.Factor2 is ConstantNode && multiply.Factor1 is NegateOperation));
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return node.Factor1 is ConstantNode constant
            ? new MultiplyOperation(constant.Negated(), ((NegateOperation)node.Factor2).Value)
            : new MultiplyOperation(((NegateOperation)node.Factor1).Value, ((ConstantNode)node.Factor2).Negated());
    }
}
