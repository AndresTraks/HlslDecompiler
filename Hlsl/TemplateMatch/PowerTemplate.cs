namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers pow(x, y) from `exp2(log2(x) * y)`. Only the bare multiply:
/// `exp2(log2(x) * a + b)` is left as is.
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
