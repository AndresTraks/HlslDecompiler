namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts `exp` back together.
///
/// The bytecode exponential is base 2, so fxc writes `exp(x)` as
/// `exp2(x * 1.4426950)` - the value over ln 2. Written out it reads as a base
/// change someone did by hand rather than as the exponential it is.
/// </summary>
public class NaturalExponentialTemplate : NodeTemplate<ExponentialOperation>
{
    // 1 / ln 2, as fxc emits it.
    private const float InverseLogarithmOfTwo = 1.44269502f;

    public override bool Match(HlslTreeNode node)
    {
        return node is ExponentialOperation exponential && TryGetValue(exponential) != null;
    }

    public override HlslTreeNode Reduce(ExponentialOperation node)
    {
        return new NaturalExponentialOperation(TryGetValue(node));
    }

    private static HlslTreeNode TryGetValue(ExponentialOperation exponential)
    {
        if (exponential.Value is not MultiplyOperation multiply)
        {
            return null;
        }
        if (IsScale(multiply.Factor1))
        {
            return multiply.Factor2;
        }
        return IsScale(multiply.Factor2) ? multiply.Factor1 : null;
    }

    private static bool IsScale(HlslTreeNode node)
    {
        return node is ConstantNode constant && constant.Value == InverseLogarithmOfTwo;
    }
}
