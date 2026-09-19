namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts `log` back together.
///
/// The bytecode logarithm is base 2, so fxc writes `log(x)` as
/// `log2(x) * 0.69314718` - the result times ln 2. The multiply is the outer node
/// here, where the exponential's is the inner one.
/// </summary>
public class NaturalLogarithmTemplate : NodeTemplate<MultiplyOperation>
{
    // ln 2, as fxc emits it.
    private const float LogarithmOfTwo = 0.693147182f;

    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply && TryGetLog(multiply) != null;
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return new NaturalLogarithmOperation(TryGetLog(node).Value);
    }

    private static LogOperation TryGetLog(MultiplyOperation multiply)
    {
        if (IsScale(multiply.Factor1))
        {
            return multiply.Factor2 as LogOperation;
        }
        return IsScale(multiply.Factor2) ? multiply.Factor1 as LogOperation : null;
    }

    private static bool IsScale(HlslTreeNode node)
    {
        return node is ConstantNode constant && constant.Value == LogarithmOfTwo;
    }
}
