namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Folds a cmp over a cmp of two constants into the inner test:
/// `-(f >= 0 ? 1 : -1) >= 0 ? b : a` is `f >= 0 ? a : b`. NaN takes the same branch.
/// </summary>
public class CompareSelectedConstantTemplate : NodeTemplate<CompareOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is CompareOperation compare && Outcomes(compare.Value) is (bool ge, bool lt) && ge != lt;
    }

    public override HlslTreeNode Reduce(CompareOperation node)
    {
        (bool ge, _) = Outcomes(node.Value).Value;
        CompareOperation inner = Inner(node.Value);
        return ge
            ? new CompareOperation(inner.Value, node.GreaterEqualValue, node.LessValue)
            : new CompareOperation(inner.Value, node.LessValue, node.GreaterEqualValue);
    }

    // Whether the outer cmp's value is at least zero where the inner cmp's value
    // was, and where it was not.
    private static (bool, bool)? Outcomes(HlslTreeNode value)
    {
        CompareOperation inner = Inner(value);
        if (inner == null
            || inner.GreaterEqualValue is not ConstantNode ge
            || inner.LessValue is not ConstantNode lt)
        {
            return null;
        }
        float sign = value is NegateOperation ? -1 : 1;
        return (sign * ge.Value >= 0, sign * lt.Value >= 0);
    }

    private static CompareOperation Inner(HlslTreeNode value)
    {
        return value switch
        {
            CompareOperation compare => compare,
            NegateOperation { Value: CompareOperation compare } => compare,
            _ => null,
        };
    }
}
