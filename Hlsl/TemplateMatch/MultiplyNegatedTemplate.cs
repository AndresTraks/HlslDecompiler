namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// `k * -x` as `-k * x`, so output does not depend on which mad operand fxc
/// put the sign on.
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
