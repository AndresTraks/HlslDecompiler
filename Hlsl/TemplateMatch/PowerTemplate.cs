namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts pow back together.
///
/// There is no power instruction, so fxc writes `pow(x, y)` as
/// `exp2(log2(x) * y)` - which is what HLSL defines it to be, and what it
/// compiles back to. Left as the two instructions it reads as a trick rather
/// than as the one thing it is.
///
/// Only the bare multiply. `exp2(log2(x) * a + b)` is a pow scaled by an
/// exponential, which is two operations and says so; colour_grade has one and
/// keeps it.
/// </summary>
public class PowerTemplate : NodeTemplate<ExponentialOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is ExponentialOperation exponential && TryGetLog(exponential) != null;
    }

    public override HlslTreeNode Reduce(ExponentialOperation node)
    {
        var multiply = (MultiplyOperation)node.Value;
        LogOperation log = TryGetLog(node);
        HlslTreeNode power = ReferenceEquals(multiply.Factor1, log)
            ? multiply.Factor2
            : multiply.Factor1;
        return new PowerOperation(log.Value, power);
    }

    private static LogOperation TryGetLog(ExponentialOperation exponential)
    {
        if (exponential.Value is not MultiplyOperation multiply)
        {
            return null;
        }
        return multiply.Factor1 as LogOperation ?? multiply.Factor2 as LogOperation;
    }
}
