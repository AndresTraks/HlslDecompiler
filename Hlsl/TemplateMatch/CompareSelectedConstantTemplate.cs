namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// A cmp whose value is another cmp choosing between two constants asks the inner
/// one's question again: which way the outer one goes is known for each way the
/// inner one went. A face sign taken as `vface >= 0 ? 1 : -1` and then tested,
/// negated, came back as `-(i.vface >= 0 ? 1 : -1) >= 0 ? b : a`, which is
/// `i.vface >= 0 ? a : b` - and fxc compiles the second to the one cmp.
///
/// A NaN fails the inner test and takes its second constant, and fails the folded
/// test the same way, so the branch it lands in is the one it landed in before.
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
